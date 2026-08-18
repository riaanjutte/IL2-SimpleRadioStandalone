using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using MahApps.Metro.Controls;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for ServerSettingsWindow.xaml
    /// </summary>
    public partial class ServerSettingsWindow : MetroWindow
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly DispatcherTimer _updateTimer;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public ServerSettingsWindow()
        {
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);

            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(1)};
            _updateTimer.Tick += UpdateUI;
            _updateTimer.Start();

            UpdateUI(null, null);
        }

        private void UpdateUI(object sender, EventArgs e)
        {
            var settings = _serverSettings;

            try
            {
                SpectatorAudio.Content = settings.GetSettingAsBool(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED)
                    ? LocalizationManager.Get("DISABLED")
                    : LocalizationManager.Get("ENABLED");

                CoalitionSecurity.Content = settings.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY)
                    ? LocalizationManager.Get("ON")
                    : LocalizationManager.Get("OFF");

                RealRadio.Content = settings.GetSettingAsBool(ServerSettingsKeys.IRL_RADIO_TX) ? LocalizationManager.Get("ON") : LocalizationManager.Get("OFF");

                TunedClientCount.Content = settings.GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT) ? LocalizationManager.Get("ON") : LocalizationManager.Get("OFF");

                ShowTransmitterName.Content = settings.GetSettingAsBool(ServerSettingsKeys.SHOW_TRANSMITTER_NAME) ? LocalizationManager.Get("ON") : LocalizationManager.Get("OFF");

                ServerVersion.Content = SRSClientSyncHandler.ServerVersion;

                SecondRadio.Content = settings.GetSettingAsBool(ServerSettingsKeys.SECOND_RADIO_ENABLED)
                    ? LocalizationManager.Get("ON")
                    : LocalizationManager.Get("OFF");

                ChannelLimit.Content = settings.GetSetting(ServerSettingsKeys.CHANNEL_LIMIT);

                var channelNames = settings.GetChannelNames();
                ChannelNames.Content = channelNames.Count == 0
                    ? "-"
                    : channelNames.Count + " configured";
                ChannelNames.ToolTip = channelNames.Count == 0
                    ? null
                    : string.Join(", ", channelNames.OrderBy(pair => pair.Key)
                        .Select(pair => pair.Key + ": " + pair.Value));

                SquadChannelLabels.Content = settings.GetOptionalSettingAsBool(
                    ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS)
                    ? LocalizationManager.Get("ON")
                    : LocalizationManager.Get("OFF");

                RadioCollisionEffects.Content =
                    settings.GetSettingAsBool(ServerSettingsKeys.RADIO_COLLISION_EFFECTS)
                        ? LocalizationManager.Get("ON")
                        : LocalizationManager.Get("OFF");
            }
            catch (IndexOutOfRangeException ex)
            {
                Logger.Warn("Missing Server Option - Connected to old server");
            }
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            _updateTimer.Stop();
        }
    }
}
