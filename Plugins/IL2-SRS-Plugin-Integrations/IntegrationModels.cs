using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    public enum RetryMode
    {
        /// <summary>A failed run is not retried.</summary>
        None,

        /// <summary>Retried every <see cref="IntegrationDefinition.FixedRetryDelay"/>.</summary>
        Fixed,

        /// <summary>Retried after 10 s, 20 s, 40 s … up to 10 minutes.</summary>
        Exponential
    }

    public static class ConditionOperator
    {
        public const string Is = "is";
        public const string IsNot = "is_not";
        public const string Contains = "contains";
        public const string NotContains = "not_contains";
        public const string GreaterThan = "gt";
        public const string LessThan = "lt";
        public const string In = "in";
        public const string Exists = "exists";
        public const string NotExists = "not_exists";

        public static readonly IReadOnlyList<(string Value, string Label)> All = new[]
        {
            (Is, "is"), (IsNot, "is not"), (Contains, "contains"), (NotContains, "does not contain"),
            (GreaterThan, "greater than"), (LessThan, "less than"), (In, "is one of (comma-separated)"),
            (Exists, "has a value"), (NotExists, "has no value")
        };

        public static bool NeedsValue(string op) => op != Exists && op != NotExists;
    }

    /// <summary>A filter on one field of the event data, e.g. <c>client.coalitionName is Red</c>.</summary>
    public sealed record IntegrationCondition(string Field, string Operator, string Value);

    /// <summary>A rule: when an event of one of the <see cref="TriggerTypes"/> matches the conditions, run the action.</summary>
    public sealed class IntegrationDefinition
    {
        public const int MaxRetriesLimit = 10;
        public static readonly TimeSpan FixedRetryDelay = TimeSpan.FromSeconds(30);

        public long Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public bool Enabled { get; init; } = true;
        /// <summary>The event types that run this integration, e.g. server.started and server.stopped.</summary>
        public IReadOnlyList<string> TriggerTypes { get; init; } = Array.Empty<string>();

        public IReadOnlyList<IntegrationCondition> Conditions { get; init; } = Array.Empty<IntegrationCondition>();
        public string ActionType { get; init; }

        /// <summary>
        /// The action's settings. Values of the action's secret fields are only filled in when the integration
        /// is loaded to run; otherwise they are null and <see cref="SecretHints"/> describes the stored value.
        /// </summary>
        public JsonObject ActionConfig { get; init; } = new JsonObject();

        /// <summary>For each stored secret field: a hint such as "ends in 0Jdl".</summary>
        public IReadOnlyDictionary<string, string> SecretHints { get; init; } = new Dictionary<string, string>();

        /// <summary>Stored secret fields that can't be decrypted (the keys that protected them are gone).</summary>
        public IReadOnlyCollection<string> UnreadableSecrets { get; init; } = Array.Empty<string>();

        public RetryMode RetryMode { get; init; } = RetryMode.None;
        public int MaxRetries { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }

        /// <summary>The trigger events this server knows, in the order they were added.</summary>
        public IReadOnlyList<SrsEventType> Triggers(IEventTypeRegistry eventTypes) =>
            TriggerTypes.Select(eventTypes.Find).Where(t => t != null).ToList();

        public bool IsTriggeredBy(string eventType) => TriggerTypes.Contains(eventType, StringComparer.Ordinal);

        /// <summary>Delay before retry number <paramref name="attempt"/> (1 = the first retry).</summary>
        public TimeSpan RetryDelay(int attempt)
        {
            return RetryMode == RetryMode.Exponential
                ? TimeSpan.FromSeconds(Math.Min(600, 10 * Math.Pow(2, Math.Max(0, attempt - 1))))
                : FixedRetryDelay;
        }
    }

    public static class ExecutionStatus
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Succeeded = "succeeded";
        public const string Failed = "failed";
        public const string Skipped = "skipped";

        public static readonly IReadOnlyList<string> All = new[] { Queued, Running, Succeeded, Failed, Skipped };

        public static bool IsFinished(string status) => status is Succeeded or Failed or Skipped;
    }

    /// <summary>One run of an integration for one event (the outbox row).</summary>
    public sealed class IntegrationExecution
    {
        public long Id { get; init; }
        public long IntegrationId { get; init; }
        public string IntegrationName { get; init; }
        public string ActionType { get; init; }
        public string EventType { get; init; }
        public string EventMessage { get; init; }

        /// <summary>The event data (JSON) the action receives: <c>event.*</c> plus the event payload.</summary>
        public string Data { get; init; }

        public string Status { get; init; }
        public int Attempts { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? NextAttemptAtUtc { get; init; }
        public DateTime? FinishedAtUtc { get; init; }
        public string LastError { get; init; }

        /// <summary>Short description of what the action did, e.g. "Discord accepted the message".</summary>
        public string Result { get; init; }
    }

    /// <summary>What an action reports after running.</summary>
    public sealed class IntegrationResult
    {
        private IntegrationResult(string status, string message, TimeSpan? retryAfter, bool countsAsAttempt = true)
        {
            Status = status;
            Message = message;
            RetryAfter = retryAfter;
            CountsAsAttempt = countsAsAttempt;
        }

        public string Status { get; }
        public string Message { get; }
        public TimeSpan? RetryAfter { get; }
        public bool IsSuccess => Status == ExecutionStatus.Succeeded;

        /// <summary>False for <see cref="Defer"/>: the run waits without using up a retry.</summary>
        public bool CountsAsAttempt { get; }

        public static IntegrationResult Success(string message = null) =>
            new(ExecutionStatus.Succeeded, message, null);

        /// <summary>A temporary failure: retried if the integration allows it (after <paramref name="after"/> if given).</summary>
        public static IntegrationResult Retry(string error, TimeSpan? after = null) =>
            new(ExecutionStatus.Queued, error, after);

        /// <summary>
        /// Not run yet because of a rate limit: run again after <paramref name="after"/>, even when the integration
        /// does not retry, without counting an attempt.
        /// </summary>
        public static IntegrationResult Defer(string reason, TimeSpan after) =>
            new(ExecutionStatus.Queued, reason, after, countsAsAttempt: false);

        /// <summary>A failure that retrying will not fix, such as a rejected webhook URL.</summary>
        public static IntegrationResult Fail(string error) => new(ExecutionStatus.Failed, error, null);
    }

    /// <summary>Everything an action needs to run once.</summary>
    public sealed class IntegrationExecutionContext
    {
        /// <summary>The execution id; 0 for a test run from the admin UI.</summary>
        public long ExecutionId { get; init; }

        public IntegrationDefinition Integration { get; init; }

        /// <summary>The action settings with secrets decrypted.</summary>
        public JsonObject Config { get; init; }

        public SrsEventType EventType { get; init; }

        /// <summary>The event data: <c>event.*</c> plus the payload fields of <see cref="EventType"/>.</summary>
        public JsonElement Data { get; init; }

        /// <summary>1 for the first attempt.</summary>
        public int Attempt { get; init; } = 1;

        /// <summary>
        /// Values the action keeps for this integration between runs. A test run gets its own empty state, so it
        /// never changes what real runs use.
        /// </summary>
        public IIntegrationState State { get; init; } = new MemoryIntegrationState();

        public bool IsTest => ExecutionId == 0;
    }

    /// <summary>
    /// Values an action keeps for its integration between runs, such as the Discord message it keeps updated.
    /// They are kept until the integration is deleted.
    /// </summary>
    public interface IIntegrationState
    {
        string Get(string key);
        void Set(string key, string value);
        void Remove(string key);
    }

    /// <summary>State that lasts only as long as the object (test runs).</summary>
    public sealed class MemoryIntegrationState : IIntegrationState
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string Get(string key) => _values.TryGetValue(key, out var value) ? value : null;
        public void Set(string key, string value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
    }
}
