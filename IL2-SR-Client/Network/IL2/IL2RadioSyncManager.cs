using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Overlay;
using Easy.MessageHub;
using Newtonsoft.Json;
using NLog;

/**
Keeps radio information in Sync Between IL2

**/

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2
{
    public class IL2RadioSyncManager
    {
    
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly UDPCommandHandler _udpCommandHandler; 
        private readonly IL2RadioSyncHandler il2RadioSyncHandler;

        private readonly ConnectedClientsSingleton _clients = ConnectedClientsSingleton.Instance;
        private DispatcherTimer _clearRadio;
        private bool _checkedRunningIL2WinWingConfiguration;

        public bool IsListening { get; private set; }

        public IL2RadioSyncManager()
        {
            IsListening = false;
            _udpCommandHandler = new UDPCommandHandler();
            il2RadioSyncHandler = new IL2RadioSyncHandler();

            _clearRadio = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher) { Interval = TimeSpan.FromSeconds(5) };
            _clearRadio.Tick += CheckIfRadioIsStale;
            Start();
        }

        private void CheckIfRadioIsStale(object sender, EventArgs e)
        {
            CheckRunningIL2WinWingConfiguration();

            //kept current by any UDP traffic from IL2
            if (!_clientStateSingleton.PlayerGameState.IsCurrent())
            {
                _clientStateSingleton.PlayerGameState.LastUpdate = -1;
                Logger.Info("Reset Radio state - IL2 not running");
                _clientStateSingleton.PlayerGameState.coalition = 0;
                _clientStateSingleton.PlayerGameState.unitId = 0;
                _clientStateSingleton.PlayerGameState.vehicleId = -1;

                MessageHub.Instance.Publish(new PlayerStateUpdate());
            }
        }

        private void CheckRunningIL2WinWingConfiguration()
        {
            if (_checkedRunningIL2WinWingConfiguration)
            {
                return;
            }

            try
            {
                if (IL2WinWingCompatibilityRepair.FindRunningIncompatibleConfigPaths().Count == 0)
                {
                    return;
                }

                _checkedRunningIL2WinWingConfiguration = true;
                TelemetryConfigurationWarning.ShowIL2WinWingCompatibilityOnce();
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Unable to inspect IL2WinWing telemetry configuration");
            }
        }

        public void Start()
        {
            IL2Listener();
            IsListening = true;
        }


        private void IL2Listener()
        {
            il2RadioSyncHandler.Start();
            _udpCommandHandler.Start();
             _clearRadio.Start();
        }

        public void Stop()
        {
            IsListening = false;

            _clearRadio.Stop();
            il2RadioSyncHandler.Stop();
            _udpCommandHandler.Stop();
            
        }
    }

    internal static class TelemetryConfigurationWarning
    {
        private static int _shown;

        internal static void ShowPortConflictOnce(int port)
        {
            ShowOnce(
                "Another application is using the SRS IL-2 telemetry port " + port + ".\n\n" +
                "If IL2WinWing is installed, open Help > Telemetry Diagnostics and select Configure IL2WinWing. " +
                "SRS and IL2WinWing must use separate telemetry ports.",
                "IL2-SRS telemetry port conflict");
        }

        internal static void ShowIL2WinWingCompatibilityOnce()
        {
            ShowOnce(
                "IL2WinWing is not configured for reliable operation alongside SRS.\n\n" +
                "Open Help > Telemetry Diagnostics and select Configure IL2WinWing. " +
                "SRS will back up the existing configuration before making changes.",
                "IL2WinWing compatibility issue");
        }

        private static void ShowOnce(string message, string title)
        {
            if (Interlocked.Exchange(ref _shown, 1) != 0)
            {
                return;
            }

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                MessageBox.Show(
                    message,
                    title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning)));
        }
    }
}
