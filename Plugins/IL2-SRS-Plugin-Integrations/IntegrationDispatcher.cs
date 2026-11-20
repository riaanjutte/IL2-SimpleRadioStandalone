using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Audit;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.Extensions.Hosting;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// Matches published events against enabled integrations, queues a run for each match, and works
    /// through the queue: one run at a time per integration (so a Discord channel sees events in order),
    /// up to <see cref="MaxConcurrentRuns"/> integrations at once, with retries as configured.
    /// Queued runs survive restarts; runs interrupted by a shutdown are queued again at startup.
    /// At shutdown it queues the events still waiting (e.g. server.stopped) and spends up to
    /// <see cref="ShutdownGrace"/> sending the runs that are due.
    /// </summary>
    public sealed class IntegrationDispatcher : BackgroundService
    {
        public const int MaxConcurrentRuns = 4;

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan IdleWait = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RetentionInterval = TimeSpan.FromHours(1);

        /// <summary>How long a shutdown waits for due runs; Docker kills the container 10 s after stopping it.</summary>
        internal static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(5);

        private readonly IntegrationStore _store;
        private readonly AuditRetention _retention;
        private readonly IEventTypeRegistry _eventTypes;
        private readonly IServerInfo _identity;
        private readonly Dictionary<string, IIntegrationAction> _actions;
        private readonly Channel<SrsEvent> _incoming = Channel.CreateBounded<SrsEvent>(
            new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
        private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
        private readonly ConcurrentDictionary<long, Task> _running = new();

        // The enabled integrations, read again after a change. The version keeps a list read before a change from
        // being cached after it.
        private readonly object _integrationsLock = new();
        private IReadOnlyList<IntegrationDefinition> _integrations;
        private long _integrationsVersion;
        private int _droppedEvents;

        public IntegrationDispatcher(IntegrationStore store, IEventBus events, IEnumerable<IIntegrationAction> actions,
            IEventTypeRegistry eventTypes, AuditRetention retention, IServerInfo identity = null)
        {
            _store = store;
            _eventTypes = eventTypes;
            _retention = retention;
            _identity = identity;
            _actions = IntegrationActions.ByType(actions);
            events.EventPublished += OnEventPublished;
            store.IntegrationsChanged += OnIntegrationsChanged;
            store.ExecutionsChanged += Wake;
        }

        private void OnIntegrationsChanged()
        {
            lock (_integrationsLock)
            {
                _integrationsVersion++;
                _integrations = null;
            }
        }

        private IReadOnlyList<IntegrationDefinition> EnabledIntegrations()
        {
            long version;
            lock (_integrationsLock)
            {
                if (_integrations != null)
                {
                    return _integrations;
                }

                version = _integrationsVersion;
            }

            var integrations = _store.List().Where(i => i.Enabled).ToList();
            lock (_integrationsLock)
            {
                if (_integrationsVersion == version)
                {
                    _integrations = integrations;
                }
            }

            return integrations;
        }

        private void OnEventPublished(SrsEvent srsEvent)
        {
            if (!_incoming.Writer.TryWrite(srsEvent) && Interlocked.Increment(ref _droppedEvents) == 1)
            {
                Logger.Warn("Integration event queue is full; events are not being sent to integrations.");
            }
        }

        private void Wake() => _wake.Writer.TryWrite(true);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var requeued = _store.ResetRunning();
            if (requeued > 0)
            {
                Logger.Info($"Queued {requeued} integration run(s) interrupted by the last shutdown again");
            }

            // Runs are cancelled ShutdownGrace after the stop, so the shutdown's own events can still go out.
            using var runs = new CancellationTokenSource();
            using var stopping = stoppingToken.Register(() => runs.CancelAfter(ShutdownGrace));
            await Task.WhenAll(MatchLoop(stoppingToken), DispatchLoop(stoppingToken, runs.Token));
            await SendDueRunsAsync(runs.Token);
        }

        /// <summary>
        /// At shutdown: queues the events published while stopping and runs what is due until
        /// <paramref name="deadline"/>. Runs still going then are queued again at the next start.
        /// </summary>
        internal async Task SendDueRunsAsync(CancellationToken deadline)
        {
            try
            {
                while (_incoming.Reader.TryRead(out var srsEvent))
                {
                    Match(srsEvent);
                }

                while (!deadline.IsCancellationRequested)
                {
                    StartDueRuns(deadline);
                    if (_running.IsEmpty)
                    {
                        break;
                    }

                    await Task.WhenAll(_running.Values.ToArray()).WaitAsync(deadline);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unable to send integration runs at shutdown");
            }

            await Task.WhenAll(_running.Values.ToArray());
        }

        /// <summary>Queues a run for every enabled integration whose trigger and conditions match the event.</summary>
        internal void Match(SrsEvent srsEvent)
        {
            var integrations = EnabledIntegrations();
            JsonElement? data = null;
            foreach (var integration in integrations)
            {
                if (!integration.IsTriggeredBy(srsEvent.Type))
                {
                    continue;
                }

                data ??= EventData.From(srsEvent, EventData.ServerSection(_identity));
                if (EventData.Matches(data.Value, integration.Conditions))
                {
                    _store.Enqueue(integration, srsEvent.Type, srsEvent.Message, data.Value);
                }
            }
        }

        private async Task MatchLoop(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var srsEvent in _incoming.Reader.ReadAllAsync(stoppingToken))
                {
                    Interlocked.Exchange(ref _droppedEvents, 0);
                    try
                    {
                        Match(srsEvent);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, $"Unable to queue integrations for {srsEvent.Type}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task DispatchLoop(CancellationToken stoppingToken, CancellationToken runToken)
        {
            var nextRetention = DateTime.UtcNow;
            while (!stoppingToken.IsCancellationRequested)
            {
                TimeSpan wait;
                try
                {
                    StartDueRuns(runToken);
                    if (DateTime.UtcNow >= nextRetention)
                    {
                        nextRetention = DateTime.UtcNow + RetentionInterval;
                        _store.ApplyRetention(DateTime.UtcNow, _retention.Days, _retention.MaxRows);
                    }

                    var nextDue = _store.NextDueAt();
                    wait = nextDue.HasValue ? nextDue.Value - DateTime.UtcNow : IdleWait;
                    wait = wait < TimeSpan.FromMilliseconds(50) ? TimeSpan.FromMilliseconds(50)
                        : wait > IdleWait ? IdleWait : wait;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Integration dispatcher failed; trying again shortly");
                    wait = TimeSpan.FromSeconds(5);
                }

                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(wait);
                    await _wake.Reader.WaitToReadAsync(timeout.Token);
                    _wake.Reader.TryRead(out _);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        /// <summary>Starts each integration's oldest queued run, when it is due and the integration isn't running.</summary>
        private void StartDueRuns(CancellationToken stoppingToken)
        {
            if (_running.Count >= MaxConcurrentRuns)
            {
                return;
            }

            foreach (var execution in _store.Due(DateTime.UtcNow, 100))
            {
                if (_running.Count >= MaxConcurrentRuns)
                {
                    return;
                }

                if (_running.ContainsKey(execution.IntegrationId) || !_store.TryStart(execution.Id))
                {
                    continue;
                }

                var started = new TaskCompletionSource();
                _running[execution.IntegrationId] = started.Task;
                _ = RunTracked(execution, started, stoppingToken);
            }
        }

        private async Task RunTracked(IntegrationExecution execution, TaskCompletionSource done,
            CancellationToken stoppingToken)
        {
            try
            {
                await RunAsync(execution, stoppingToken);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Integration run {execution.Id} failed unexpectedly");
                // Left running, it would hold back every later run of the integration until a restart.
                try
                {
                    _store.Finish(execution.Id, ExecutionStatus.Failed, "Failed unexpectedly: " + ex.Message, null, null);
                }
                catch (Exception finishError)
                {
                    Logger.Error(finishError, $"Unable to record integration run {execution.Id} as failed");
                }
            }
            finally
            {
                _running.TryRemove(execution.IntegrationId, out _);
                done.TrySetResult();
                Wake();
            }
        }

        internal async Task RunAsync(IntegrationExecution execution, CancellationToken stoppingToken)
        {
            var attempt = execution.Attempts + 1;
            var integration = _store.GetForExecution(execution.IntegrationId);
            if (integration == null)
            {
                _store.Finish(execution.Id, ExecutionStatus.Skipped, "Integration deleted", null, null);
                return;
            }

            if (!integration.Enabled)
            {
                _store.Finish(execution.Id, ExecutionStatus.Skipped, "Integration disabled", null, null);
                return;
            }

            if (!_actions.TryGetValue(integration.ActionType, out var action))
            {
                _store.Finish(execution.Id, ExecutionStatus.Failed,
                    $"The action \"{integration.ActionType}\" is not available on this server.", null, null);
                return;
            }

            if (integration.UnreadableSecrets.Count > 0)
            {
                // Retrying can't help: the keys that encrypted them are gone (e.g. the keys folder was replaced).
                _store.Finish(execution.Id, ExecutionStatus.Failed,
                    $"The stored {string.Join(", ", integration.UnreadableSecrets)} can't be read; enter it again on the " +
                    "integration page.", null, null);
                return;
            }

            using var data = JsonDocument.Parse(execution.Data);
            var context = new IntegrationExecutionContext
            {
                ExecutionId = execution.Id,
                Integration = integration,
                Config = integration.ActionConfig,
                EventType = _eventTypes.Find(execution.EventType),
                Data = data.RootElement,
                Attempt = attempt,
                State = _store.State(integration.Id)
            };

            var result = await IntegrationService.RunAsync(action, context, stoppingToken);
            if (stoppingToken.IsCancellationRequested && !result.IsSuccess)
            {
                // Shutting down: leave it running so it is queued again at the next start.
                return;
            }

            if (result.IsSuccess)
            {
                _store.Finish(execution.Id, ExecutionStatus.Succeeded, null, result.Message, null);
                return;
            }

            if (!result.CountsAsAttempt)
            {
                var wait = result.RetryAfter ?? TimeSpan.FromSeconds(10);
                Logger.Info($"Integration \"{integration.Name}\" waits {wait.TotalSeconds:0} s: {result.Message}");
                _store.Defer(execution.Id, result.Message, DateTime.UtcNow + wait);
                return;
            }

            var retry = result.Status == ExecutionStatus.Queued && integration.RetryMode != RetryMode.None &&
                        attempt <= integration.MaxRetries;
            if (retry)
            {
                var delay = result.RetryAfter ?? integration.RetryDelay(attempt);
                Logger.Warn($"Integration \"{integration.Name}\" failed (attempt {attempt}), retrying in " +
                            $"{delay.TotalSeconds:0} s: {result.Message}");
                _store.Finish(execution.Id, ExecutionStatus.Queued, result.Message, null, DateTime.UtcNow + delay);
            }
            else
            {
                Logger.Warn($"Integration \"{integration.Name}\" failed: {result.Message}");
                _store.Finish(execution.Id, ExecutionStatus.Failed, result.Message, null, null);
            }
        }
    }
}
