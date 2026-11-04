using System;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Microsoft.AspNetCore.Components;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Components.Shared
{
    /// <summary>
    /// Base for admin pages that show live state: re-renders when clients or the running state change
    /// (at most every 250 ms), when <see cref="QueueRefresh"/> is called, and once a second for uptime and
    /// transmit indicators.
    /// </summary>
    public abstract class LiveComponentBase : ComponentBase, IDisposable
    {
        private static readonly TimeSpan MinimumRefreshInterval = TimeSpan.FromMilliseconds(250);

        private readonly CancellationTokenSource _disposed = new CancellationTokenSource();
        private int _refreshQueued;

        [Inject] protected IServerStatusEvents StatusEvents { get; set; }
        [Inject] protected IAdminActor AdminActor { get; set; }

        /// <summary>Name recorded in the event log for changes made from the UI.</summary>
        protected string Actor => AdminActor.Name;

        /// <summary>Refresh once a second as well as on server events.</summary>
        protected virtual bool RefreshEverySecond => true;

        protected override void OnInitialized()
        {
            StatusEvents.ClientsChanged += QueueRefresh;
            StatusEvents.RunningStateChanged += QueueRefresh;

            if (RefreshEverySecond)
            {
                _ = RefreshLoop(_disposed.Token);
            }
        }

        /// <summary>Called before each live refresh renders; reload data here.</summary>
        protected virtual void OnLiveRefresh()
        {
        }

        /// <summary>Re-renders soon (at most every 250 ms); safe to call from any thread.</summary>
        protected void QueueRefresh()
        {
            if (Interlocked.Exchange(ref _refreshQueued, 1) == 1)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(MinimumRefreshInterval, _disposed.Token);
                    Interlocked.Exchange(ref _refreshQueued, 0);
                    await InvokeAsync(Refresh);
                }
                catch (OperationCanceledException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            });
        }

        private async Task RefreshLoop(CancellationToken cancellationToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            try
            {
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    await InvokeAsync(Refresh);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void Refresh()
        {
            if (_disposed.IsCancellationRequested)
            {
                return;
            }

            OnLiveRefresh();
            StateHasChanged();
        }

        public virtual void Dispose()
        {
            StatusEvents.ClientsChanged -= QueueRefresh;
            StatusEvents.RunningStateChanged -= QueueRefresh;
            _disposed.Cancel();
            _disposed.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
