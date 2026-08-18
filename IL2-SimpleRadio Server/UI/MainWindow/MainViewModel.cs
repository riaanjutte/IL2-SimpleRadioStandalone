using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ChannelNames;
using Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ClientAdmin;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.MainWindow
{
    public sealed class MainViewModel : Screen, IHandle<ServerStateMessage>
    {
        private readonly ClientAdminViewModel _clientAdminViewModel;
        private readonly IEventAggregator _eventAggregator;
        private readonly IWindowManager _windowManager;
        private readonly ServerState _serverState;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _serverStatusTimer;
        private List<SRClient> _serverClients = new List<SRClient>();
        private bool _clientAdminVisible;
        private DateTime? _serverStartedAtUtc = DateTime.UtcNow;
        private ServerHealthSnapshot _serverHealth;

        private DispatcherTimer _passwordDebounceTimer = null;

        public MainViewModel(IWindowManager windowManager, IEventAggregator eventAggregator,
            ClientAdminViewModel clientAdminViewModel, ServerState serverState)
        {
            _windowManager = windowManager;
            _eventAggregator = eventAggregator;
            _clientAdminViewModel = clientAdminViewModel;
            _serverState = serverState;
            _eventAggregator.Subscribe(this);

            _serverStatusTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            _serverStatusTimer.Tick += (sender, args) => RefreshServerHealth();
            _serverStatusTimer.Start();
            RefreshServerHealth();

            DisplayName = $"IL2-SRS Server - {UpdaterChecker.DISPLAY_VERSION} - {ListeningPort}" ;

            Logger.Info("IL2-SRS Server Running - " + UpdaterChecker.DISPLAY_VERSION);
        }

        public bool IsServerRunning { get; private set; } = true;

        public string ServerButtonText => IsServerRunning ? "Stop Server" : "Start Server";

    
        public bool IsRadioSecurityEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY).BoolValue;

        public bool IsSpectatorAudioEnabled
            => !ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED).BoolValue;

        public bool IsExportListEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED).BoolValue;

        public bool IsRealisticTxEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX).BoolValue;

        public bool IsBetaUpdatesEnabled
            => ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES).BoolValue;

        public string ServerThemeText => ServerThemeManager.CurrentTheme.ToUpperInvariant();

        public ClientAdminViewModel ClientAdmin => _clientAdminViewModel;
        public double ServerWindowWidth => ServerWindowLayout.GetWindowWidth(_clientAdminVisible);
        public double ServerWindowHeight => ServerWindowLayout.WindowHeight;
        public GridLength ClientAdminColumnWidth =>
            new GridLength(ServerWindowLayout.GetClientAdminPanelWidth(_clientAdminVisible));
        public Visibility ClientAdminVisibility => _clientAdminVisible ? Visibility.Visible : Visibility.Collapsed;
        public string ClientAdminToggleText => _clientAdminVisible ? "Close Client Admin" : "Client Admin";
        public bool HealthIsRunning => _serverHealth?.IsRunning == true;
        public string HealthStatus => _serverHealth?.Status ?? "STARTING";
        public string HealthUptime => _serverHealth?.Uptime ?? "00:00:00";
        public int HealthClients => _serverHealth?.Clients ?? 0;
        public string HealthVoiceLinks => _serverHealth?.VoiceLinks ?? "0/0";
        public int HealthRecentTransmitters => _serverHealth?.RecentTransmitters ?? 0;
        public string HealthMemory => _serverHealth?.Memory ?? "0 MB";

    

        private string _globalLobbyFrequencies =
            ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES).StringValue;

        private DispatcherTimer _globalLobbyFrequenciesDebounceTimer;

        public string GlobalLobbyFrequencies
        {
            get { return _globalLobbyFrequencies; }
            set
            {
                _globalLobbyFrequencies = value.Trim();
                if (_globalLobbyFrequenciesDebounceTimer != null)
                {
                    _globalLobbyFrequenciesDebounceTimer.Stop();
                    _globalLobbyFrequenciesDebounceTimer.Tick -= GlobalLobbyFrequenciesDebounceTimerTick;
                    _globalLobbyFrequenciesDebounceTimer = null;
                }

                _globalLobbyFrequenciesDebounceTimer = new DispatcherTimer();
                _globalLobbyFrequenciesDebounceTimer.Tick += GlobalLobbyFrequenciesDebounceTimerTick;
                _globalLobbyFrequenciesDebounceTimer.Interval = TimeSpan.FromMilliseconds(500);
                _globalLobbyFrequenciesDebounceTimer.Start();

                NotifyOfPropertyChange(() => GlobalLobbyFrequencies);
            }
        }

        public bool IsTunedCountEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT).BoolValue;

        public bool IsTransmitterNameEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME).BoolValue;
        public string ListeningPort
            => ServerSettingsStore.Instance.GetServerSetting(ServerSettingsKeys.SERVER_PORT).StringValue;

        public bool IsSecondRadioEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SECOND_RADIO_ENABLED).BoolValue;
        public bool IsRadioCollisionEffectsEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.RADIO_COLLISION_EFFECTS).BoolValue;
        public bool IsSquadChannelLabelsEnabled
            => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS).BoolValue;
        public int ChannelLimit
        {
            get => ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.CHANNEL_LIMIT).IntValue;
            set
            {
                ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.CHANNEL_LIMIT,
                    value.ToString());
                _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
            }
        }

        public void Handle(ServerStateMessage message)
        {
            var wasRunning = IsServerRunning;
            IsServerRunning = message.IsRunning;
            if (!wasRunning && IsServerRunning)
            {
                _serverStartedAtUtc = DateTime.UtcNow;
            }
            else if (!IsServerRunning)
            {
                _serverStartedAtUtc = null;
            }

            _serverClients = new List<SRClient>(message.Clients);
            NotifyOfPropertyChange(() => ServerButtonText);
            RefreshServerHealth();
        }

        public void ServerStartStop()
        {
            if (IsServerRunning)
            {
                var result = MessageBox.Show(
                    "Stop the IL2-SRS server? Connected clients will be disconnected.",
                    "Stop Server",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No);
                if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                _eventAggregator.PublishOnBackgroundThread(new StopServerMessage());
            }
            else
            {
                _eventAggregator.PublishOnBackgroundThread(new StartServerMessage());
            }
        }

        public override void CanClose(Action<bool> callback)
        {
            var message = IsServerRunning
                ? "Close IL2-SRS Server? The server will stop and connected clients will be disconnected."
                : "Close IL2-SRS Server?";
            var result = MessageBox.Show(
                message,
                "Close IL2-SRS Server",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            callback(result == MessageBoxResult.Yes);
        }

        public void EditChannelNames()
        {
            IDictionary<string, object> settings = new Dictionary<string, object>
            {
                {"Icon", new BitmapImage(new Uri("pack://application:,,,/IL2-SR-Server;component/server-10.ico"))},
                {"ResizeMode", ResizeMode.NoResize}
            };
            _windowManager.ShowDialog(new ChannelNamesViewModel(_eventAggregator), null, settings);
        }

        public void ClientAdminToggle()
        {
            _clientAdminVisible = !_clientAdminVisible;
            NotifyOfPropertyChange(() => ServerWindowWidth);
            NotifyOfPropertyChange(() => ClientAdminColumnWidth);
            NotifyOfPropertyChange(() => ClientAdminVisibility);
            NotifyOfPropertyChange(() => ClientAdminToggleText);
        }

        public void RadioSecurityToggle()
        {
            var newSetting = !IsRadioSecurityEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.COALITION_AUDIO_SECURITY, newSetting);
            NotifyOfPropertyChange(() => IsRadioSecurityEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void SpectatorAudioToggle()
        {
            var newSetting = !IsSpectatorAudioEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, !newSetting);
            NotifyOfPropertyChange(() => IsSpectatorAudioEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void ExportListToggle()
        {
            var newSetting = !IsExportListEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, newSetting);
            NotifyOfPropertyChange(() => IsExportListEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void RealRadioToggle()
        {
            var newSetting = !IsRealisticTxEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.IRL_RADIO_TX, newSetting);
            NotifyOfPropertyChange(() => IsRealisticTxEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void CheckForBetaUpdatesToggle()
        {
            var newSetting = !IsBetaUpdatesEnabled;
            ServerSettingsStore.Instance.SetServerSetting(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, newSetting);
            NotifyOfPropertyChange(() => IsBetaUpdatesEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        private void GlobalLobbyFrequenciesDebounceTimerTick(object sender, EventArgs e)
        {
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, _globalLobbyFrequencies);

            _eventAggregator.PublishOnBackgroundThread(new ServerFrequenciesChanged()
            {
                GlobalLobbyFrequencies = _globalLobbyFrequencies
            });

            _globalLobbyFrequenciesDebounceTimer.Stop();
            _globalLobbyFrequenciesDebounceTimer.Tick -= GlobalLobbyFrequenciesDebounceTimerTick;
            _globalLobbyFrequenciesDebounceTimer = null;
        }

        public void TunedCountToggle()
        {
            var newSetting = !IsTunedCountEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT, newSetting);
            NotifyOfPropertyChange(() => IsTunedCountEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }


        public void ShowTransmitterNameToggle()
        {
            var newSetting = !IsTransmitterNameEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, newSetting);
            NotifyOfPropertyChange(() => IsTransmitterNameEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }


        public void EnableSecondRadioToggle()
        {
            var newSetting = !IsSecondRadioEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SECOND_RADIO_ENABLED, newSetting);
            NotifyOfPropertyChange(() => IsSecondRadioEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void ServerThemeToggle()
        {
            var newTheme = ServerThemeManager.CurrentTheme == ServerThemeManager.DarkTheme
                ? ServerThemeManager.WhiteTheme
                : ServerThemeManager.DarkTheme;

            ServerSettingsStore.Instance.SetServerSetting(ServerSettingsKeys.SERVER_UI_THEME, newTheme);
            ServerThemeManager.Apply(newTheme);
            NotifyOfPropertyChange(() => ServerThemeText);
        }

        public void RadioCollisionEffectsToggle()
        {
            var newSetting = !IsRadioCollisionEffectsEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.RADIO_COLLISION_EFFECTS, newSetting);
            NotifyOfPropertyChange(() => IsRadioCollisionEffectsEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        public void SquadChannelLabelsToggle()
        {
            var newSetting = !IsSquadChannelLabelsEnabled;
            ServerSettingsStore.Instance.SetGeneralSetting(ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS, newSetting);
            NotifyOfPropertyChange(() => IsSquadChannelLabelsEnabled);

            _eventAggregator.PublishOnBackgroundThread(new ServerSettingsChangedMessage());
        }

        protected override void OnDeactivate(bool close)
        {
            if (close)
            {
                _serverStatusTimer.Stop();
            }

            base.OnDeactivate(close);
        }

        private void RefreshServerHealth()
        {
            long workingSetBytes;
            try
            {
                workingSetBytes = Process.GetCurrentProcess().WorkingSet64;
            }
            catch
            {
                workingSetBytes = 0;
            }

            _serverHealth = ServerHealthSnapshot.Create(IsServerRunning,
                _serverState.IsTcpListenerRunning, _serverState.IsUdpListenerRunning, _serverStartedAtUtc,
                _serverClients, workingSetBytes, DateTime.UtcNow);
            NotifyOfPropertyChange(() => HealthIsRunning);
            NotifyOfPropertyChange(() => HealthStatus);
            NotifyOfPropertyChange(() => HealthUptime);
            NotifyOfPropertyChange(() => HealthClients);
            NotifyOfPropertyChange(() => HealthVoiceLinks);
            NotifyOfPropertyChange(() => HealthRecentTransmitters);
            NotifyOfPropertyChange(() => HealthMemory);
        }

    }
}
