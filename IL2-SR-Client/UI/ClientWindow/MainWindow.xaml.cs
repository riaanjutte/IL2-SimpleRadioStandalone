using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Preferences;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Favourites;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Overlay;
using Easy.MessageHub;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.Dmo;
using NAudio.Wave;
using NLog;
using WPFCustomMessageBox;
using InputBinding = Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.InputBinding;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {

        public delegate void ToggleOverlayCallback(bool uiButton);

        private readonly AudioManager _audioManager;

        private readonly string _guid;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private AudioPreview _audioPreview;
        private SRSClientSyncHandler _client;
        private int _port = 6002;

        private Overlay.RadioOverlayWindow _radioOverlayWindow;

        private IPAddress _resolvedIp;
        private ServerSettingsWindow _serverSettingsWindow;

        private ClientListWindow _clientListWindow;
        private PilotRosterWindow _pilotRosterWindow;

        //used to debounce toggle
        private long _toggleShowHide;
        private bool _pilotRosterAutoStartedForCurrentConnection;

        private readonly DispatcherTimer _updateTimer;
        private ServerAddress _serverAddress;
        private readonly DelegateCommand _connectCommand;
        private bool _initialisingLanguagePicker;
        private bool _initialisingThemePicker;
        private bool _initialisingVuMeterStylePicker;
        private bool _initialisingOverlayOpacitySliders;
        private bool _initialisingWeatheringControls;
        private const string ThemeBakelite = "Bakelite";
        private const string ThemeGrey = "Grey";
        private const string ThemeRafGreyGreen = "RAF Grey-Green";
        private const string ThemeUsaafOliveDrab = "USAAF Olive Drab";
        private const string ThemeLuftwaffeRlm66 = "Luftwaffe RLM 66";
        private const string ThemeSovietA14SteelGrey = "Soviet A-14 Steel Grey";
        private const string ThemeIvory = "Ivory";
        private const string ThemeWhite = "White";
        private const string VuMeterStyleAuto = "Auto";
        private const string VuMeterStyleLight = "Light";
        private const string VuMeterStyleDark = "Dark";

        private sealed class VuMeterStyleOption
        {
            public VuMeterStyleOption(string value)
            {
                Value = value;
                DisplayName = LocalizationManager.Get(value);
            }

            public string Value { get; }
            public string DisplayName { get; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private const string CommunitySettingsAccepted = "Accepted";
        private const string CommunitySettingsDeclined = "Declined";
        private const string RecommendedSelectedRadioMutedVolume = "0.15";
        private const string RecommendedPttReleaseDelay = "250";
        private const string RecommendedRadio1AudioChannel = "-0.75";
        private const string RecommendedRadio2AudioChannel = "0.75";
        private const string CombatBoxRciServerHost = "srs.combatbox.net";
        private const double DefaultClientX = 200;
        private const double DefaultClientY = 200;
        private const double DefaultClientWidth = 700;
        private const double DefaultClientHeight = 650;
        private const double DefaultRadioX = 300;
        private const double DefaultRadioY = 300;
        private const double DefaultAwacsX = 300;
        private const double DefaultAwacsY = 300;
        private const double DefaultPilotRosterX = 360;
        private const double DefaultPilotRosterY = 260;
        private const double DefaultPilotRosterWidth = 560;
        private const double DefaultPilotRosterHeight = 420;
        private const double DefaultOverlayOpacity = 1.0;
        private const double DefaultWeatheringOpacity = 0.5;
        private const string WeatheringOpacityResourceKey = "WeatheringOpacity";
        private const string WeatheringExtraOpacityResourceKey = "WeatheringExtraOpacity";
        private const string OverlayWeatheringOpacityResourceKey = "OverlayWeatheringOpacity";
        private const string OverlayWeatheringExtraOpacityResourceKey = "OverlayWeatheringExtraOpacity";

        /// <remarks>Used in the XAML for DataBinding many things</remarks>
        public ClientStateSingleton ClientState { get; } = ClientStateSingleton.Instance;
        
        /// <remarks>Used in the XAML for DataBinding the connected client count</remarks>
        public ConnectedClientsSingleton Clients { get; } = ConnectedClientsSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding input audio related UI elements</remarks>
        public AudioInputSingleton AudioInput { get; } = AudioInputSingleton.Instance;

        /// <remarks>Used in the XAML for DataBinding output audio related UI elements</remarks>
        public AudioOutputSingleton AudioOutput { get; } = AudioOutputSingleton.Instance;

        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;
        private readonly IL2RadioSyncManager _il2RadioSyncManager;

        public MainWindow()
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

            _connectCommand = new DelegateCommand(Connect, () => ServerAddress != null);
            FavouriteServersViewModel = new FavouriteServersViewModel(new CsvFavouriteServerStore());

            _initialisingOverlayOpacitySliders = true;
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
            RciStatusLabel.Text = LocalizationManager.Get("RCI");
            UpdateCombatBoxFeatureVisibility(false);
            LocalizationManager.LocalizeFlowDocument(AboutFlowDocument);
            Loaded += MainWindow_Loaded;

            // Initialize ToolTip controls
            ToolTips.Init();

            // Initialize images/icons
            Images.Init();

            // Initialise sounds
            Sounds.Init();

            DataContext = this;

            var client = ClientStateSingleton.Instance;
            client.PropertyChanged += ClientState_PropertyChanged;
            UpdateConnectionStatusStrip();
            UpdateWindowButtonLabels();

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientX, DefaultClientX);
            Top = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientY, DefaultClientY);
            Width = GetClientWindowWidth(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientWidth, DefaultClientWidth));
            Height = GetClientWindowHeight(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientHeight, DefaultClientHeight));

            Title = Title + " - " + UpdaterChecker.RELEASE_TAG;
            StatusVersionLabel.Text = UpdaterChecker.RELEASE_TAG;

            CheckWindowVisibility();

            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised))
            {
                Hide();
                WindowState = WindowState.Minimized;

                Logger.Info("Started IL2-SimpleRadio Client " + UpdaterChecker.VERSION + " minimized");
            }
            else
            {
                Logger.Info("Started IL2-SimpleRadio Client " + UpdaterChecker.VERSION);
            }

            _guid = ClientStateSingleton.Instance.ShortGUID;
            Analytics.Log("Client", "Startup", _globalSettings.GetClientSetting(GlobalSettingsKeys.ClientIdLong).RawValue);

            InitSettingsScreen();

            InitSettingsProfiles();
            ReloadProfile();
            PromptForCommunityRecommendedSettingsOnce();

            InitInput();

            InitDefaultAddress();

            InitOverlayOpacitySliders();
            InitWeatheringControls();
            InitVuMeterStylePicker();
            IL2_SR_Client.App.ApplyVuMeterStyle();

            SpeakerBoost.Value = _globalSettings.GetClientSetting(GlobalSettingsKeys.SpeakerBoost).DoubleValue;

            Speaker_VU.Value = -100;
            Mic_VU.Value = -100;

            _audioManager = new AudioManager(AudioOutput.WindowsN);
            _audioManager.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float) SpeakerBoost.Value);


            if ((SpeakerBoostLabel != null) && (SpeakerBoost != null))
            {
                SpeakerBoostLabel.Content = VolumeConversionHelper.ConvertLinearDiffToDB(_audioManager.SpeakerBoost);
            }

            UpdaterChecker.CheckForUpdate(_globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates));

            InitFlowDocument();

            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(100)};
            _updateTimer.Tick += UpdatePlayerLocationAndVUMeters;
            _updateTimer.Start();

            _il2RadioSyncManager = new IL2RadioSyncManager();

            MessageHub.Instance.Subscribe<SRSAddressMessage>(AutoConnect);

        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AutoStartRadioOverlay();
            AutoStartPilotRoster();
        }

        private void PromptForCommunityRecommendedSettingsOnce()
        {
            var savedChoice = _globalSettings.GetClientSetting(GlobalSettingsKeys.CommunityRecommendedSettingsChoice).RawValue;
            if (!string.IsNullOrWhiteSpace(savedChoice))
            {
                return;
            }

            var result = ShowLocalizedYesNo(
                BuildCommunityRecommendedSettingsPrompt(),
                LocalizationManager.Get("Community Recommended Profile Settings"),
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                ApplyCommunityRecommendedProfileSettings();
                _globalSettings.SetClientSetting(GlobalSettingsKeys.CommunityRecommendedSettingsChoice, CommunitySettingsAccepted);
                ReloadProfileSettings();
                return;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.CommunityRecommendedSettingsChoice, CommunitySettingsDeclined);
        }

        private string BuildCommunityRecommendedSettingsPrompt()
        {
            return LocalizationManager.Get("Would you like to apply the community recommended profile settings to the current profile?")
                   + Environment.NewLine
                   + Environment.NewLine
                   + LocalizationManager.Get("This will set:")
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Radio Switch works as Push To Talk (PTT)") + ": "
                   + LocalizationManager.Get("ON")
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Enable Radio Voice Effect") + ": "
                   + LocalizationManager.Get("OFF")
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Enable Clipping Effect (Requires Radio effects on!)") + ": "
                   + LocalizationManager.Get("OFF")
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Enable Text to Speech (beta)") + ": "
                   + LocalizationManager.Get("ON")
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Selected Radio Muted Volume") + ": 15%"
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Push to Talk Release Delay (ms)") + ": 250"
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("First Radio Audio Channel") + ": "
                   + RecommendedRadio1AudioChannel
                   + Environment.NewLine
                   + " - " + LocalizationManager.Get("Second Radio Audio Channel") + ": "
                   + RecommendedRadio2AudioChannel
                   + Environment.NewLine
                   + Environment.NewLine
                   + LocalizationManager.Get("Select Yes to apply these settings. Select No to keep your current settings. You will not be asked again.");
        }

        private void ApplyCommunityRecommendedProfileSettings()
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioSwitchIsPTT, true);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEffects, false);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEffectsClipping, false);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.EnableTextToSpeech, true);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.SelectedRadioMutedVolume, RecommendedSelectedRadioMutedVolume);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.PTTReleaseDelay, RecommendedPttReleaseDelay);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.Radio1Channel, RecommendedRadio1AudioChannel);
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.Radio2Channel, RecommendedRadio2AudioChannel);
        }

        private void CheckWindowVisibility()
        {
            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.DisableWindowVisibilityCheck))
            {
                Logger.Info("Window visibility check is disabled, skipping");
                return;
            }

            bool mainWindowVisible = false;
            bool radioWindowVisible = false;
            bool awacsWindowVisible = false;

            int mainWindowX = ToScreenCoordinate(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientX, DefaultClientX));
            int mainWindowY = ToScreenCoordinate(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientY, DefaultClientY));
            int radioWindowX = ToScreenCoordinate(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.RadioX, DefaultRadioX));
            int radioWindowY = ToScreenCoordinate(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.RadioY, DefaultRadioY));
            int awacsWindowX = ToScreenCoordinate(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.AwacsX, DefaultAwacsX));
            int awacsWindowY = ToScreenCoordinate(_globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.AwacsY, DefaultAwacsY));
            double pilotRosterWidth = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterWidth, DefaultPilotRosterWidth);
            double pilotRosterHeight = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterHeight, DefaultPilotRosterHeight);
            double pilotRosterX = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterX, DefaultPilotRosterX);
            double pilotRosterY = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterY, DefaultPilotRosterY);

            Logger.Info($"Checking window visibility for main client window {{X={mainWindowX},Y={mainWindowY}}}");
            Logger.Info($"Checking window visibility for radio overlay {{X={radioWindowX},Y={radioWindowY}}}");
            Logger.Info($"Checking window visibility for AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}}");
            Logger.Info($"Checking window visibility for pilot roster {{X={pilotRosterX},Y={pilotRosterY},Width={pilotRosterWidth},Height={pilotRosterHeight}}}");

            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                Logger.Info($"Checking {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds} for window visibility");

                if (screen.Bounds.Contains(mainWindowX, mainWindowY))
                {
                    Logger.Info($"Main client window {{X={mainWindowX},Y={mainWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    mainWindowVisible = true;
                }
                if (screen.Bounds.Contains(radioWindowX, radioWindowY))
                {
                    Logger.Info($"Radio overlay {{X={radioWindowX},Y={radioWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    radioWindowVisible = true;
                }
                if (screen.Bounds.Contains(awacsWindowX, awacsWindowY))
                {
                    Logger.Info($"AWACS overlay {{X={awacsWindowX},Y={awacsWindowY}}} is visible on {(screen.Primary ? "primary " : "")}screen {screen.DeviceName} with bounds {screen.Bounds}");
                    awacsWindowVisible = true;
                }
            }

            bool pilotRosterVisible = IsWindowRectVisibleOnAnyScreen(
                pilotRosterX,
                pilotRosterY,
                pilotRosterWidth,
                pilotRosterHeight);

            if (!mainWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS client window is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue.",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"Main client window outside visible area of monitors, resetting position ({mainWindowX},{mainWindowY}) to defaults");

                _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, DefaultClientX);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, DefaultClientY);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientWidth, DefaultClientWidth);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientHeight, DefaultClientHeight);

                Left = DefaultClientX;
                Top = DefaultClientY;
                Width = DefaultClientWidth;
                Height = DefaultClientHeight;
            }

            if (!radioWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS radio overlay is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue.",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"Radio overlay window outside visible area of monitors, resetting position ({radioWindowX},{radioWindowY}) to defaults");

                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, DefaultRadioX);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, DefaultRadioY);

                if (_radioOverlayWindow != null)
                {
                    _radioOverlayWindow.Left = DefaultRadioX;
                    _radioOverlayWindow.Top = DefaultRadioY;
                }
            }

            if (!awacsWindowVisible)
            {
                MessageBox.Show(this,
                    "The SRS AWACS overlay is no longer visible likely due to a monitor reconfiguration.\n\nThe position will be reset to default to fix this issue",
                    "SRS window position reset",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                Logger.Warn($"AWACS overlay window outside visible area of monitors, resetting position ({awacsWindowX},{awacsWindowY}) to defaults");

                _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsX, DefaultAwacsX);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.AwacsY, DefaultAwacsY);
            }

            if (!pilotRosterVisible)
            {
                var result = ShowLocalizedYesNo(
                    "The SRS pilot roster window appears to be saved off-screen, likely due to a monitor, VR, or OpenKneeboard layout change.\n\nDo you want to move it back onto the active desktop?",
                    "Pilot roster position reset",
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ResetPilotRosterPositionToActiveDesktop(pilotRosterX, pilotRosterY, pilotRosterWidth, pilotRosterHeight);
                }
            }
        }

        private static bool IsWindowRectVisibleOnAnyScreen(double x, double y, double width, double height)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) ||
                double.IsNaN(y) || double.IsInfinity(y) ||
                double.IsNaN(width) || double.IsInfinity(width) ||
                double.IsNaN(height) || double.IsInfinity(height) ||
                width <= 0 || height <= 0)
            {
                return false;
            }

            var windowRect = new System.Drawing.Rectangle(
                ToScreenCoordinate(x),
                ToScreenCoordinate(y),
                ToScreenLength(width),
                ToScreenLength(height));

            return System.Windows.Forms.Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(windowRect));
        }

        private static int ToScreenCoordinate(double value)
        {
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value < int.MinValue)
            {
                return int.MinValue;
            }

            return (int)Math.Round(value);
        }

        private static int ToScreenLength(double value)
        {
            if (value > int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value < 1)
            {
                return 1;
            }

            return Math.Max(1, (int)Math.Round(value));
        }

        private void ResetPilotRosterPositionToActiveDesktop(double oldX, double oldY, double oldWidth, double oldHeight)
        {
            var activeScreen = GetActiveDesktopScreen();
            var width = Math.Min(Math.Max(DefaultPilotRosterWidth, oldWidth), activeScreen.WorkingArea.Width);
            var height = Math.Min(Math.Max(DefaultPilotRosterHeight, oldHeight), activeScreen.WorkingArea.Height);
            var x = activeScreen.WorkingArea.Left + (activeScreen.WorkingArea.Width - width) / 2.0;
            var y = activeScreen.WorkingArea.Top + (activeScreen.WorkingArea.Height - height) / 2.0;

            Logger.Warn($"Pilot roster window outside visible area of monitors, resetting position ({oldX},{oldY},{oldWidth},{oldHeight}) to active desktop {activeScreen.DeviceName}");

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterX, x);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterY, y);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterWidth, width);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterHeight, height);

            if (_pilotRosterWindow != null)
            {
                _pilotRosterWindow.Left = x;
                _pilotRosterWindow.Top = y;
                _pilotRosterWindow.Width = width;
                _pilotRosterWindow.Height = height;
            }
        }

        private System.Windows.Forms.Screen GetActiveDesktopScreen()
        {
            var mainWindowPoint = new System.Drawing.Point(
                (int)Math.Round(Left + Math.Max(0, ActualWidth) / 2.0),
                (int)Math.Round(Top + Math.Max(0, ActualHeight) / 2.0));

            return System.Windows.Forms.Screen.AllScreens.FirstOrDefault(screen => screen.WorkingArea.Contains(mainWindowPoint))
                   ?? System.Windows.Forms.Screen.PrimaryScreen;
        }

        private void InitFlowDocument()
        {
            //make hyperlinks work
            var hyperlinks = WPFElementHelper.GetVisuals(AboutFlowDocument).OfType<Hyperlink>();
            foreach (var link in hyperlinks)
                link.RequestNavigate += new System.Windows.Navigation.RequestNavigateEventHandler((sender, args) =>
                {
                    Process.Start(new ProcessStartInfo(args.Uri.AbsoluteUri));
                    args.Handled = true;
                });
        }

        private void InitDefaultAddress()
        {
            // legacy setting migration
            if (!string.IsNullOrEmpty(_globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer).StringValue) &&
                FavouriteServersViewModel.Addresses.Count == 0)
            {
                var oldAddress = new ServerAddress(_globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer).StringValue,
                    _globalSettings.GetClientSetting(GlobalSettingsKeys.LastServer).StringValue, true);
                FavouriteServersViewModel.Addresses.Add(oldAddress);
            }

            ServerAddress = FavouriteServersViewModel.DefaultServerAddress;
        }

        private void InitSettingsProfiles()
        {
            ControlsProfile.IsEnabled = false;
            ControlsProfile.Items.Clear();
            foreach (var profile in _globalSettings.ProfileSettingsStore.InputProfiles.Keys)
            {
                ControlsProfile.Items.Add(profile);
            } 
            ControlsProfile.IsEnabled = true;
            ControlsProfile.SelectedIndex = 0;

            CurrentProfile.Content = _globalSettings.ProfileSettingsStore.CurrentProfileName;

        }

        void ReloadProfile()
        {
            //switch profiles
            Logger.Info(ControlsProfile.SelectedValue as string + " - Profile now in use");
            _globalSettings.ProfileSettingsStore.CurrentProfileName = ControlsProfile.SelectedValue as string;

            //redraw UI
            ReloadInputBindings();
            ReloadProfileSettings();
            ReloadRadioAudioChannelSettings();

            CurrentProfile.Content = _globalSettings.ProfileSettingsStore.CurrentProfileName;
        }

        private void InitInput()
        {
            InputManager = new InputDeviceManager(this, ToggleOverlay);

            InitSettingsProfiles();

            ControlsProfile.SelectionChanged += OnProfileDropDownChanged;

            Radio1.InputName = LocalizationManager.Get("Select First Radio");
            Radio1.ControlInputBinding = InputBinding.Switch1;
            Radio1.InputDeviceManager = InputManager;

            Radio2.InputName = LocalizationManager.Get("Select Second Radio");
            Radio2.ControlInputBinding = InputBinding.Switch2;
            Radio2.InputDeviceManager = InputManager;

            PTT.InputName = LocalizationManager.Get("Push To Talk - PTT");
            PTT.ControlInputBinding = InputBinding.Ptt;
            PTT.InputDeviceManager = InputManager;

            Intercom.InputName = LocalizationManager.Get("Select Intercom");
            Intercom.ControlInputBinding = InputBinding.Intercom;
            Intercom.InputDeviceManager = InputManager;

            RadioOverlay.InputName = LocalizationManager.Get("Overlay Toggle");
            RadioOverlay.ControlInputBinding = InputBinding.OverlayToggle;
            RadioOverlay.InputDeviceManager = InputManager;

            RadioChannelUp.InputName = LocalizationManager.Get("Radio Channel Up");
            RadioChannelUp.ControlInputBinding = InputBinding.RadioChannelUp;
            RadioChannelUp.InputDeviceManager = InputManager;

            RadioChannelDown.InputName = LocalizationManager.Get("Radio Channel Down");
            RadioChannelDown.ControlInputBinding = InputBinding.RadioChannelDown;
            RadioChannelDown.InputDeviceManager = InputManager;

            RadioChannel1.InputName = LocalizationManager.Get("Radio Channel 1");
            RadioChannel1.ControlInputBinding = InputBinding.RadioChannel1;
            RadioChannel1.InputDeviceManager = InputManager;

            RadioChannel2.InputName = LocalizationManager.Get("Radio Channel 2");
            RadioChannel2.ControlInputBinding = InputBinding.RadioChannel2;
            RadioChannel2.InputDeviceManager = InputManager;

            RadioChannel3.InputName = LocalizationManager.Get("Radio Channel 3");
            RadioChannel3.ControlInputBinding = InputBinding.RadioChannel3;
            RadioChannel3.InputDeviceManager = InputManager;

            RadioChannel4.InputName = LocalizationManager.Get("Radio Channel 4");
            RadioChannel4.ControlInputBinding = InputBinding.RadioChannel4;
            RadioChannel4.InputDeviceManager = InputManager;

            RadioChannel5.InputName = LocalizationManager.Get("Radio Channel 5");
            RadioChannel5.ControlInputBinding = InputBinding.RadioChannel5;
            RadioChannel5.InputDeviceManager = InputManager;

            RadioChannel6.InputName = LocalizationManager.Get("Radio Channel 6");
            RadioChannel6.ControlInputBinding = InputBinding.RadioChannel6;
            RadioChannel6.InputDeviceManager = InputManager;

            RadioChannel7.InputName = LocalizationManager.Get("Radio Channel 7");
            RadioChannel7.ControlInputBinding = InputBinding.RadioChannel7;
            RadioChannel7.InputDeviceManager = InputManager;

            RadioChannel8.InputName = LocalizationManager.Get("Radio Channel 8");
            RadioChannel8.ControlInputBinding = InputBinding.RadioChannel8;
            RadioChannel8.InputDeviceManager = InputManager;

            RadioChannel9.InputName = LocalizationManager.Get("Radio Channel 9");
            RadioChannel9.ControlInputBinding = InputBinding.RadioChannel9;
            RadioChannel9.InputDeviceManager = InputManager;

            RadioChannel10.InputName = LocalizationManager.Get("Radio Channel 10");
            RadioChannel10.ControlInputBinding = InputBinding.RadioChannel10;
            RadioChannel10.InputDeviceManager = InputManager;

            RadioChannel11.InputName = LocalizationManager.Get("Radio Channel 11");
            RadioChannel11.ControlInputBinding = InputBinding.RadioChannel11;
            RadioChannel11.InputDeviceManager = InputManager;

            RadioChannel12.InputName = LocalizationManager.Get("Radio Channel 12");
            RadioChannel12.ControlInputBinding = InputBinding.RadioChannel12;
            RadioChannel12.InputDeviceManager = InputManager;

            NextRadio.InputName = LocalizationManager.Get("Select Next Radio / Intercom");
            NextRadio.ControlInputBinding = InputBinding.NextRadio;
            NextRadio.InputDeviceManager = InputManager;

            PreviousRadio.InputName = LocalizationManager.Get("Select Previous Radio / Intercom");
            PreviousRadio.ControlInputBinding = InputBinding.PreviousRadio;
            PreviousRadio.InputDeviceManager = InputManager;

            ReadStatus.InputName = LocalizationManager.Get("Read Status (TTS on required)");
            ReadStatus.ControlInputBinding = InputBinding.ReadStatus;
            ReadStatus.InputDeviceManager = InputManager;

            ToggleSelectedRadioMute.InputName = LocalizationManager.Get("Mute / Unmute Selected Radio");
            ToggleSelectedRadioMute.ControlInputBinding = InputBinding.ToggleSelectedRadioMute;
            ToggleSelectedRadioMute.InputDeviceManager = InputManager;

            ToggleOtherRadioMute.InputName = LocalizationManager.Get("Mute / Unmute Other Radio");
            ToggleOtherRadioMute.ControlInputBinding = InputBinding.ToggleOtherRadioMute;
            ToggleOtherRadioMute.InputDeviceManager = InputManager;

            ToggleAllRadiosMute.InputName = LocalizationManager.Get("Mute / Unmute Both Radios");
            ToggleAllRadiosMute.ControlInputBinding = InputBinding.ToggleAllRadiosMute;
            ToggleAllRadiosMute.InputDeviceManager = InputManager;

            ToggleMicrophoneMute.InputName = LocalizationManager.Get("Mute / Unmute Microphone");
            ToggleMicrophoneMute.ControlInputBinding = InputBinding.ToggleMicrophoneMute;
            ToggleMicrophoneMute.InputDeviceManager = InputManager;

            Radio1ChannelUp.InputName = LocalizationManager.Get("Radio 1 Channel Up");
            Radio1ChannelUp.ControlInputBinding = InputBinding.Radio1ChannelUp;
            Radio1ChannelUp.InputDeviceManager = InputManager;

            Radio1ChannelDown.InputName = LocalizationManager.Get("Radio 1 Channel Down");
            Radio1ChannelDown.ControlInputBinding = InputBinding.Radio1ChannelDown;
            Radio1ChannelDown.InputDeviceManager = InputManager;

            Radio2ChannelUp.InputName = LocalizationManager.Get("Radio 2 Channel Up");
            Radio2ChannelUp.ControlInputBinding = InputBinding.Radio2ChannelUp;
            Radio2ChannelUp.InputDeviceManager = InputManager;

            Radio2ChannelDown.InputName = LocalizationManager.Get("Radio 2 Channel Down");
            Radio2ChannelDown.ControlInputBinding = InputBinding.Radio2ChannelDown;
            Radio2ChannelDown.InputDeviceManager = InputManager;
        }

        private void RefreshInputBindingLabels()
        {
            Radio1.InputName = LocalizationManager.Get("Select First Radio");
            Radio2.InputName = LocalizationManager.Get("Select Second Radio");
            PTT.InputName = LocalizationManager.Get("Push To Talk - PTT");
            Intercom.InputName = LocalizationManager.Get("Select Intercom");
            RadioOverlay.InputName = LocalizationManager.Get("Overlay Toggle");
            RadioChannelUp.InputName = LocalizationManager.Get("Radio Channel Up");
            RadioChannelDown.InputName = LocalizationManager.Get("Radio Channel Down");
            RadioChannel1.InputName = LocalizationManager.Get("Radio Channel 1");
            RadioChannel2.InputName = LocalizationManager.Get("Radio Channel 2");
            RadioChannel3.InputName = LocalizationManager.Get("Radio Channel 3");
            RadioChannel4.InputName = LocalizationManager.Get("Radio Channel 4");
            RadioChannel5.InputName = LocalizationManager.Get("Radio Channel 5");
            RadioChannel6.InputName = LocalizationManager.Get("Radio Channel 6");
            RadioChannel7.InputName = LocalizationManager.Get("Radio Channel 7");
            RadioChannel8.InputName = LocalizationManager.Get("Radio Channel 8");
            RadioChannel9.InputName = LocalizationManager.Get("Radio Channel 9");
            RadioChannel10.InputName = LocalizationManager.Get("Radio Channel 10");
            RadioChannel11.InputName = LocalizationManager.Get("Radio Channel 11");
            RadioChannel12.InputName = LocalizationManager.Get("Radio Channel 12");
            NextRadio.InputName = LocalizationManager.Get("Select Next Radio / Intercom");
            PreviousRadio.InputName = LocalizationManager.Get("Select Previous Radio / Intercom");
            ReadStatus.InputName = LocalizationManager.Get("Read Status (TTS on required)");
            ToggleSelectedRadioMute.InputName = LocalizationManager.Get("Mute / Unmute Selected Radio");
            ToggleOtherRadioMute.InputName = LocalizationManager.Get("Mute / Unmute Other Radio");
            ToggleAllRadiosMute.InputName = LocalizationManager.Get("Mute / Unmute Both Radios");
            ToggleMicrophoneMute.InputName = LocalizationManager.Get("Mute / Unmute Microphone");
            Radio1ChannelUp.InputName = LocalizationManager.Get("Radio 1 Channel Up");
            Radio1ChannelDown.InputName = LocalizationManager.Get("Radio 1 Channel Down");
            Radio2ChannelUp.InputName = LocalizationManager.Get("Radio 2 Channel Up");
            Radio2ChannelDown.InputName = LocalizationManager.Get("Radio 2 Channel Down");

            ReloadInputBindings();
        }

        private void OnProfileDropDownChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ControlsProfile.IsEnabled)
                ReloadProfile();
        }

        private void ReloadInputBindings()
        {
            Radio1.LoadInputSettings();
            Radio2.LoadInputSettings();
            PTT.LoadInputSettings();
            Intercom.LoadInputSettings();
            RadioOverlay.LoadInputSettings();
            RadioChannelUp.LoadInputSettings();
            RadioChannelDown.LoadInputSettings();
            RadioChannel1.LoadInputSettings();
            RadioChannel2.LoadInputSettings();
            RadioChannel3.LoadInputSettings();
            RadioChannel4.LoadInputSettings();
            RadioChannel5.LoadInputSettings();
            RadioChannel6.LoadInputSettings();
            RadioChannel7.LoadInputSettings();
            RadioChannel8.LoadInputSettings();
            RadioChannel9.LoadInputSettings();
            RadioChannel10.LoadInputSettings();
            RadioChannel11.LoadInputSettings();
            RadioChannel12.LoadInputSettings();
            NextRadio.LoadInputSettings();
            PreviousRadio.LoadInputSettings();
            ReadStatus.LoadInputSettings();
            ToggleSelectedRadioMute.LoadInputSettings();
            ToggleOtherRadioMute.LoadInputSettings();
            ToggleAllRadiosMute.LoadInputSettings();
            ToggleMicrophoneMute.LoadInputSettings();
            Radio1ChannelUp.LoadInputSettings();
            Radio1ChannelDown.LoadInputSettings();
            Radio2ChannelUp.LoadInputSettings();
            Radio2ChannelDown.LoadInputSettings();
        }

        private void ReloadRadioAudioChannelSettings()
        {
            Radio1Config.Reload();
            Radio2Config.Reload();
            IntercomConfig.Reload();
        }

       
        public InputDeviceManager InputManager { get; set; }

        public FavouriteServersViewModel FavouriteServersViewModel { get; }

        public ServerAddress ServerAddress
        {
            get { return _serverAddress; }
            set
            {
                _serverAddress = value;
                if (value != null)
                {
                    ServerIp.Text = value.Address;
                }

                _connectCommand.RaiseCanExecuteChanged();
            }
        }

        public ICommand ConnectCommand => _connectCommand;

        private void UpdatePlayerLocationAndVUMeters(object sender, EventArgs e)
        {
            if (_audioPreview != null)
            {
                // Only update mic volume output if an audio input device is available - sometimes the value can still change, leaving the user with the impression their mic is working after all
                if (AudioInput.MicrophoneAvailable)
                {
                    Mic_VU.Value = _audioPreview.MicMax;
                }
                Speaker_VU.Value = _audioPreview.SpeakerMax;
            }
            else if (_audioManager != null)
            {
                // Only update mic volume output if an audio input device is available - sometimes the value can still change, leaving the user with the impression their mic is working after all
                if (AudioInput.MicrophoneAvailable)
                {
                    Mic_VU.Value = _audioManager.MicMax;
                }
                Speaker_VU.Value = _audioManager.SpeakerMax;
            }
            else
            {
                Mic_VU.Value = -100;
                Speaker_VU.Value = -100;
            }

            ConnectedClientsSingleton.Instance.NotifyAll();

        }

        private void InitSettingsScreen()
        {
            InitLanguagePicker();
            InitThemePicker();

            AutoConnectPromptToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);
            AutoConnectMismatchPromptToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);
            RadioOverlayTaskbarItem.IsChecked =
                _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
            AutoStartRadioOverlayToggle.IsChecked =
                _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoStartRadioOverlay);
            AutoStartPilotRosterToggle.IsChecked =
                _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoStartPilotRoster);
            RefocusIL2.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RefocusIL2);
            ExpandInputDevices.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ExpandControls);

            MinimiseToTray.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray);
            StartMinimised.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.StartMinimised);

            MicAGC.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AGC);
            MicDenoise.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.Denoise);

            CheckForBetaUpdates.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates);
            PlayConnectionSounds.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds);

            RequireAdminToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin);

            ShowTransmitterName.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ShowTransmitterName);
            ThreeDEffectsToggle.IsChecked = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ThreeDEffectsEnabled);

            RefreshOnOffToggleContent();
        }

        private void InitLanguagePicker()
        {
            _initialisingLanguagePicker = true;
            LanguagePicker.ItemsSource = LocalizationManager.SupportedLanguages;
            LanguagePicker.SelectedItem =
                LocalizationManager.GetLanguageOption(_globalSettings.GetClientSetting(GlobalSettingsKeys.Language).RawValue);
            _initialisingLanguagePicker = false;
        }

        private void InitThemePicker()
        {
            _initialisingThemePicker = true;
            ThemePicker.ItemsSource = new[] { ThemeBakelite, ThemeGrey, ThemeRafGreyGreen, ThemeUsaafOliveDrab, ThemeLuftwaffeRlm66, ThemeSovietA14SteelGrey, ThemeIvory, ThemeWhite };
            ThemePicker.SelectedItem = CurrentTheme();
            _initialisingThemePicker = false;
        }

        private void InitVuMeterStylePicker()
        {
            _initialisingVuMeterStylePicker = true;
            var options = new[]
            {
                new VuMeterStyleOption(VuMeterStyleAuto),
                new VuMeterStyleOption(VuMeterStyleLight),
                new VuMeterStyleOption(VuMeterStyleDark)
            };

            var currentStyle = CurrentVuMeterStyle();
            VuMeterStylePicker.DisplayMemberPath = nameof(VuMeterStyleOption.DisplayName);
            VuMeterStylePicker.ItemsSource = options;
            VuMeterStylePicker.SelectedItem = options.First(option => option.Value == currentStyle);
            _initialisingVuMeterStylePicker = false;
        }

        private string CurrentVuMeterStyle()
        {
            var style = _globalSettings.GetClientSetting(GlobalSettingsKeys.VuMeterStyle).RawValue;
            var normalizedStyle = NormalizeThemeName(style);
            if (normalizedStyle == NormalizeThemeName(VuMeterStyleLight))
            {
                return VuMeterStyleLight;
            }

            if (normalizedStyle == NormalizeThemeName(VuMeterStyleDark))
            {
                return VuMeterStyleDark;
            }

            return VuMeterStyleAuto;
        }

        private string CurrentTheme()
        {
            var theme = _globalSettings.GetClientSetting(GlobalSettingsKeys.Theme).RawValue;
            var normalizedTheme = NormalizeThemeName(theme);
            if (normalizedTheme == NormalizeThemeName(ThemeGrey))
            {
                return ThemeGrey;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeRafGreyGreen))
            {
                return ThemeRafGreyGreen;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeUsaafOliveDrab))
            {
                return ThemeUsaafOliveDrab;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeLuftwaffeRlm66))
            {
                return ThemeLuftwaffeRlm66;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeSovietA14SteelGrey))
            {
                return ThemeSovietA14SteelGrey;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeIvory))
            {
                return ThemeIvory;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeWhite))
            {
                return ThemeWhite;
            }

            return ThemeBakelite;
        }

        private static string NormalizeThemeName(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(theme.Length);
            foreach (var character in theme)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTheme = ResolveThemeSelection(ThemePicker.SelectedItem);
            if (_initialisingThemePicker || selectedTheme == null || selectedTheme == CurrentTheme())
            {
                return;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.Theme, selectedTheme);
            IL2_SR_Client.App.ApplyUiTheme();
            IL2_SR_Client.App.ApplyVuMeterStyle();
            ApplyWeatheringOpacity();
        }

        private void VuMeterStylePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedStyle = ResolveVuMeterStyleSelection(VuMeterStylePicker.SelectedItem);
            if (_initialisingVuMeterStylePicker || selectedStyle == null || selectedStyle == CurrentVuMeterStyle())
            {
                return;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.VuMeterStyle, selectedStyle);
            IL2_SR_Client.App.ApplyVuMeterStyle();
        }

        private static string ResolveVuMeterStyleSelection(object selectedItem)
        {
            var selectedOption = selectedItem as VuMeterStyleOption;
            if (selectedOption != null)
            {
                return selectedOption.Value;
            }

            var selectedText = selectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                selectedText = selectedItem?.ToString();
            }

            var normalizedStyle = NormalizeThemeName(selectedText);
            if (normalizedStyle == NormalizeThemeName(VuMeterStyleLight))
            {
                return VuMeterStyleLight;
            }

            if (normalizedStyle == NormalizeThemeName(VuMeterStyleDark))
            {
                return VuMeterStyleDark;
            }

            if (normalizedStyle == NormalizeThemeName(VuMeterStyleAuto))
            {
                return VuMeterStyleAuto;
            }

            return null;
        }

        private static string ResolveThemeSelection(object selectedItem)
        {
            var selectedText = selectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedText))
            {
                selectedText = selectedItem?.ToString();
            }

            var normalizedTheme = NormalizeThemeName(selectedText);
            if (normalizedTheme == NormalizeThemeName(ThemeGrey))
            {
                return ThemeGrey;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeRafGreyGreen))
            {
                return ThemeRafGreyGreen;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeUsaafOliveDrab))
            {
                return ThemeUsaafOliveDrab;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeLuftwaffeRlm66))
            {
                return ThemeLuftwaffeRlm66;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeSovietA14SteelGrey))
            {
                return ThemeSovietA14SteelGrey;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeIvory))
            {
                return ThemeIvory;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeWhite))
            {
                return ThemeWhite;
            }

            if (normalizedTheme == NormalizeThemeName(ThemeBakelite))
            {
                return ThemeBakelite;
            }

            return null;
        }

        private void ReloadProfileSettings()
        {
            RadioSwitchIsPTT.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioSwitchIsPTT);

            RadioTxStartToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_Start);
            RadioTxEndToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioTxEffects_End);

            RadioRxStartToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_Start);
            RadioRxEndToggle.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioRxEffects_End);

            RadioSoundEffects.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffects);
            RadioSoundEffectsClipping.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.RadioEffectsClipping);

            EnableTextToSpeech.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.EnableTextToSpeech);
            WrapNextRadio.IsChecked = _globalSettings.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.WrapNextRadio);

            //disable to set without triggering onchange
            TextToSpeechVolume.IsEnabled = false;
            TextToSpeechVolume.ValueChanged -= TextToSpeechVolume_ValueChanged;
            TextToSpeechVolume.ValueChanged += TextToSpeechVolume_ValueChanged;
            TextToSpeechVolume.Value = double.Parse(_globalSettings.ProfileSettingsStore.GetClientSetting(ProfileSettingsKeys.TextToSpeechVolume).RawValue,CultureInfo.InvariantCulture);
            TextToSpeechVolume.IsEnabled = true;

            //disable to set without triggering onchange
            SelectedRadioMutedVolume.IsEnabled = false;
            SelectedRadioMutedVolume.ValueChanged -= SelectedRadioMutedVolume_ValueChanged;
            SelectedRadioMutedVolume.ValueChanged += SelectedRadioMutedVolume_ValueChanged;
            SelectedRadioMutedVolume.Value = double.Parse(_globalSettings.ProfileSettingsStore.GetClientSetting(ProfileSettingsKeys.SelectedRadioMutedVolume).RawValue, CultureInfo.InvariantCulture);
            SelectedRadioMutedVolume.IsEnabled = true;

            //disable to set without triggering onchange
            PTTReleaseDelay.IsEnabled = false;
            PTTReleaseDelay.ValueChanged -= PushToTalkReleaseDelay_ValueChanged;
            PTTReleaseDelay.ValueChanged += PushToTalkReleaseDelay_ValueChanged;
            PTTReleaseDelay.Value = double.Parse(_globalSettings.ProfileSettingsStore.GetClientSetting(ProfileSettingsKeys.PTTReleaseDelay).RawValue, CultureInfo.InvariantCulture);
            PTTReleaseDelay.IsEnabled = true;

            RefreshOnOffToggleContent();
        }

        private void RefreshOnOffToggleContent()
        {
            foreach (var toggleButton in GetOnOffToggleButtons())
            {
                toggleButton.Checked -= OnOffToggleButtonStateChanged;
                toggleButton.Unchecked -= OnOffToggleButtonStateChanged;
                toggleButton.Checked += OnOffToggleButtonStateChanged;
                toggleButton.Unchecked += OnOffToggleButtonStateChanged;

                UpdateOnOffToggleContent(toggleButton);
            }
        }

        private IEnumerable<ToggleButton> GetOnOffToggleButtons()
        {
            yield return AutoConnectPromptToggle;
            yield return AutoConnectMismatchPromptToggle;
            yield return RadioOverlayTaskbarItem;
            yield return AutoStartRadioOverlayToggle;
            yield return AutoStartPilotRosterToggle;
            yield return RefocusIL2;
            yield return ExpandInputDevices;
            yield return MinimiseToTray;
            yield return StartMinimised;
            yield return MicAGC;
            yield return MicDenoise;
            yield return CheckForBetaUpdates;
            yield return PlayConnectionSounds;
            yield return RequireAdminToggle;
            yield return ShowTransmitterName;
            yield return ThreeDEffectsToggle;
            yield return WeatheringEffectToggle;
            yield return RadioSwitchIsPTT;
            yield return RadioTxStartToggle;
            yield return RadioTxEndToggle;
            yield return RadioRxStartToggle;
            yield return RadioRxEndToggle;
            yield return RadioSoundEffects;
            yield return RadioSoundEffectsClipping;
            yield return EnableTextToSpeech;
            yield return WrapNextRadio;
        }

        private void OnOffToggleButtonStateChanged(object sender, RoutedEventArgs e)
        {
            UpdateOnOffToggleContent(sender as ToggleButton);
        }

        private static void UpdateOnOffToggleContent(ToggleButton toggleButton)
        {
            if (toggleButton == null)
            {
                return;
            }

            toggleButton.Content = LocalizationManager.Get(toggleButton.IsChecked == true ? "ON" : "OFF");
        }

        private void Connect()
        {
            if (ClientState.IsConnected)
            {
                Stop();
            }
            else
            {
                SaveSelectedInputAndOutput();

                try
                {
                    //process hostname
                    var resolvedAddresses = Dns.GetHostAddresses(GetAddressFromTextBox());
                    var ip = resolvedAddresses.FirstOrDefault(xa => xa.AddressFamily == AddressFamily.InterNetwork); // Ensure we get an IPv4 address in case the host resolves to both IPv6 and IPv4

                    if (ip != null)
                    {
                        _resolvedIp = ip;
                        _port = GetPortFromTextBox();

                        _client = new SRSClientSyncHandler(_guid, UpdateUICallback);
                        _client.TryConnect(new IPEndPoint(_resolvedIp, _port), ConnectCallback);

                        StartStop.Content = LocalizationManager.Get("Connecting...");
                        StartStop.IsEnabled = false;
                        Mic.IsEnabled = false;
                        Speakers.IsEnabled = false;
                        MicOutput.IsEnabled = false;
                        Preview.IsEnabled = false;

                        if (_audioPreview != null)
                        {
                            Preview.Content = LocalizationManager.Get("Audio Preview");
                            _audioPreview.StopEncoding();
                            _audioPreview = null;
                        }
                    }
                    else
                    {
                        //invalid ID
                        MessageBox.Show(LocalizationManager.Get("Invalid IP or Host Name!"),
                            LocalizationManager.Get("Host Name Error"), MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        ClientState.IsConnected = false;
                        ToggleServerSettings.IsEnabled = false;
                    }
                }
                catch (Exception ex) when (ex is SocketException || ex is ArgumentException)
                {
                    MessageBox.Show(LocalizationManager.Get("Invalid IP or Host Name!"),
                        LocalizationManager.Get("Host Name Error"), MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    ClientState.IsConnected = false;
                    ToggleServerSettings.IsEnabled = false;
                }
            }
        }

        private string GetAddressFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (addr.Contains(":"))
            {
                return addr.Split(':')[0];
            }

            return addr;
        }

        private int GetPortFromTextBox()
        {
            var addr = ServerIp.Text.Trim();

            if (addr.Contains(":"))
            {
                int port;
                if (int.TryParse(addr.Split(':')[1], out port))
                {
                    return port;
                }
                throw new ArgumentException("specified port is not valid");
            }

            return 6002;
        }

        private void Stop(bool connectionError = false)
        {
            if (ClientState.IsConnected && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
            {
                try
                {
                    Sounds.BeepDisconnected.Play();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to play disconnect sound");
                }
            }

            ClientState.IsConnectionErrored = connectionError;

            StartStop.Content = LocalizationManager.Get("Connect");
            StartStop.IsEnabled = true;
            Mic.IsEnabled = true;
            Speakers.IsEnabled = true;
            MicOutput.IsEnabled = true;
            Preview.IsEnabled = true;
            ClientState.IsConnected = false;
            ToggleServerSettings.IsEnabled = false;
            UpdateRciStatusIndicator();
            UpdateCombatBoxFeatureVisibility(false);
            _radioOverlayWindow?.SetRciIndicatorEnabled(false);

            try
            {
                _audioManager.StopEncoding();
            }
            catch (Exception ex)
            {
            }

            if (_client != null)
            {
                _client.Disconnect();
                _client = null;
            }
        }

        private void SaveSelectedInputAndOutput()
        {
            //save app settings
            // Only save selected microphone if one is actually available, resulting in a crash otherwise
            if (AudioInput.MicrophoneAvailable)
            {
                if(AudioInput.SelectedAudioInput.Value == null)
                {
                    _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, "default");

                }
                else
                {
                    var input = ((MMDevice)AudioInput.SelectedAudioInput.Value).ID;
                    _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioInputDeviceId, input);
                }
            }

            if (AudioOutput.SelectedAudioOutput.Value == null)
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId, "default");
            }
            else
            {
                var output = (MMDevice)AudioOutput.SelectedAudioOutput.Value;
                _globalSettings.SetClientSetting(GlobalSettingsKeys.AudioOutputDeviceId, output.ID);
            }

            //check if we have optional output
            if (AudioOutput.SelectedMicAudioOutput.Value != null)
            {
                var micOutput = (MMDevice)AudioOutput.SelectedMicAudioOutput.Value;
                _globalSettings.SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, micOutput.ID);
            }
            else
            {
                _globalSettings.SetClientSetting(GlobalSettingsKeys.MicAudioOutputDeviceId, "");
            }
        }

        private void ClientState_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ClientStateSingleton.IsConnected)
                || e.PropertyName == nameof(ClientStateSingleton.IsConnectionErrored)
                || e.PropertyName == nameof(ClientStateSingleton.IsGameConnected))
            {
                Dispatcher.BeginInvoke(new Action(UpdateConnectionStatusStrip));
            }
        }

        private Brush GetConnectionStatusBrush(string resourceKey, Brush fallback)
        {
            return TryFindResource(resourceKey) as Brush ?? fallback;
        }

        private void UpdateConnectionStatusStrip()
        {
            if (ConnectionStatusStrip == null || ConnectionStatusLamp == null || ConnectionStatusText == null || ConnectionStatusHint == null)
            {
                return;
            }

            if (ClientState.IsConnectionErrored)
            {
                ConnectionStatusStrip.Background = new SolidColorBrush(Color.FromRgb(0x32, 0x1C, 0x18));
                ConnectionStatusStrip.BorderBrush = GetConnectionStatusBrush("MilLedErrorBrush", Brushes.Red);
                ConnectionStatusLamp.Fill = GetConnectionStatusBrush("MilLedErrorBrush", Brushes.Red);
                ConnectionStatusText.Foreground = GetConnectionStatusBrush("MilLedErrorBrush", Brushes.Red);
                ConnectionStatusText.Text = LocalizationManager.Get("CONNECTION ERROR");
                ConnectionStatusHint.Text = LocalizationManager.Get("Check address or network");
                return;
            }

            if (ClientState.IsConnected)
            {
                ConnectionStatusStrip.Background = new SolidColorBrush(Color.FromRgb(0x20, 0x30, 0x1C));
                ConnectionStatusStrip.BorderBrush = GetConnectionStatusBrush("MilLedOnBrush", Brushes.LimeGreen);
                ConnectionStatusLamp.Fill = GetConnectionStatusBrush("MilLedOnBrush", Brushes.LimeGreen);
                ConnectionStatusText.Foreground = GetConnectionStatusBrush("MilLedOnBrush", Brushes.LimeGreen);
                ConnectionStatusText.Text = LocalizationManager.Get("CONNECTED");
                ConnectionStatusHint.Text = ClientState.IsGameConnected
                    ? LocalizationManager.Get("Server and IL-2 active")
                    : LocalizationManager.Get("Server link established");
                return;
            }

            ConnectionStatusStrip.Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x25, 0x1B));
            ConnectionStatusStrip.BorderBrush = GetConnectionStatusBrush("MilLedOffBrush", Brushes.Gray);
            ConnectionStatusLamp.Fill = GetConnectionStatusBrush("MilLedOffBrush", Brushes.Gray);
            ConnectionStatusText.Foreground = GetConnectionStatusBrush("MilTextSecondaryBrush", Brushes.LightGray);
            ConnectionStatusText.Text = LocalizationManager.Get("DISCONNECTED");
            ConnectionStatusHint.Text = LocalizationManager.Get("Server link inactive");
        }

        private void ConnectCallback(bool result, bool connectionError, string connection)
        {
            string currentConnection = ServerIp.Text.Trim();
            if (!currentConnection.Contains(":"))
            {
                currentConnection += ":6002";
            }

            if (result)
            {
                if (!ClientState.IsConnected)
                {
                    try
                    {
                        StartStop.Content = LocalizationManager.Get("Disconnect");
                        StartStop.IsEnabled = true;

                        ClientState.IsConnected = true;
                        ClientState.IsVoipConnected = false;
                        UpdateCombatBoxFeatureVisibility();

                        if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.PlayConnectionSounds))
                        {
                            try
                            {
                                Sounds.BeepConnected.Play();
                            }
                            catch (Exception ex)
                            {
                                Logger.Warn(ex, "Failed to play connect sound");
                            }
                        }

                        _globalSettings.SetClientSetting(GlobalSettingsKeys.LastServer, ServerIp.Text);

                        _audioManager.StartEncoding(_guid, InputManager,
                            _resolvedIp, _port);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex,
                            "Unable to get audio device - likely output device error - Pick another. Error:" +
                            ex.Message);
                        Stop();

                        var messageBoxResult = CustomMessageBox.ShowYesNo(
                            "Problem initialising Audio Output!\n\nTry a different Output device and please post your clientlog.txt to the support Discord server.\n\nJoin support Discord server now?",
                            "Audio Output Error",
                            "OPEN PRIVACY SETTINGS",
                            "JOIN DISCORD SERVER",
                            MessageBoxImage.Error);

                        if (messageBoxResult == MessageBoxResult.Yes) Process.Start("https://discord.gg/baw7g3t");
                    }
                }
            }
            else if (string.Equals(currentConnection, connection, StringComparison.OrdinalIgnoreCase))
            {
                // Only stop connection/reset state if connection is currently active
                // Autoconnect mismatch will quickly disconnect/reconnect, leading to double-callbacks
                Stop(connectionError);
            }
            else
            {
                if (!ClientState.IsConnected)
                {
                    Stop(connectionError);
                }
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            ClientStateSingleton.Instance.PropertyChanged -= ClientState_PropertyChanged;
            MessageHub.Instance.ClearSubscriptions();
            _il2RadioSyncManager.Stop();

            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientX, bounds.Left);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientY, bounds.Top);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientWidth, bounds.Width);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientHeight, bounds.Height);

            //save window position and size
            base.OnClosing(e);

            //stop timer
            _updateTimer?.Stop();

            Stop();

            _audioPreview?.StopEncoding();
            _audioPreview = null;

            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            _pilotRosterWindow?.Close();
            _pilotRosterWindow = null;



        }

        private double GetClientWindowWidth(double configuredWidth)
        {
            if (double.IsNaN(configuredWidth) || double.IsInfinity(configuredWidth) || configuredWidth <= 0)
            {
                return DefaultClientWidth;
            }

            return Math.Max(MinWidth, configuredWidth);
        }

        private double GetClientWindowHeight(double configuredHeight)
        {
            if (double.IsNaN(configuredHeight) || double.IsInfinity(configuredHeight) || configuredHeight <= 0)
            {
                return DefaultClientHeight;
            }

            return Math.Max(MinHeight, configuredHeight);
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized && _globalSettings.GetClientSettingBool(GlobalSettingsKeys.MinimiseToTray))
            {
                Hide();
            }

            base.OnStateChanged(e);
        }

        private void PreviewAudio(object sender, RoutedEventArgs e)
        {
            if (_audioPreview == null)
            {
                if (!AudioInput.MicrophoneAvailable)
                {
                    Logger.Info("Unable to preview audio, no valid audio input device available or selected");
                    return;
                }

                //get device
                try
                {
                    SaveSelectedInputAndOutput();

                    _audioPreview = new AudioPreview();
                    _audioPreview.SpeakerBoost = VolumeConversionHelper.ConvertVolumeSliderToScale((float)SpeakerBoost.Value);
                    _audioPreview.StartPreview(AudioOutput.WindowsN);

                    Preview.Content = LocalizationManager.Get("Stop Preview");
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        "Unable to preview audio - likely output device error - Pick another. Error:" + ex.Message);

                }
            }
            else
            {
                Preview.Content = LocalizationManager.Get("Audio Preview");
                _audioPreview.StopEncoding();
                _audioPreview = null;
            }
        }

        private void UpdateUICallback()
        {
            if (ClientState.IsConnected)
            {
                ToggleServerSettings.IsEnabled = true;
            }
            else
            {
                ToggleServerSettings.IsEnabled = false;
            }

            var showRciStatus = ShouldShowRciStatus();
            UpdateRciStatusIndicator(showRciStatus);
            UpdateCombatBoxFeatureVisibility(showRciStatus);
            _radioOverlayWindow?.SetRciIndicatorEnabled(showRciStatus);
        }

        private void UpdateCombatBoxFeatureVisibility()
        {
            UpdateCombatBoxFeatureVisibility(ShouldShowRciStatus());
        }

        private void UpdateCombatBoxFeatureVisibility(bool showCombatBoxFeatures)
        {
            ShowPilotRoster.Visibility = Visibility.Visible;
            CombatBoxRciPanel.Visibility = showCombatBoxFeatures ? Visibility.Visible : Visibility.Collapsed;

            if (!showCombatBoxFeatures && (_pilotRosterWindow?.IsUnavailableMode == false))
            {
                _pilotRosterAutoStartedForCurrentConnection = false;
                _pilotRosterWindow?.Close();
                _pilotRosterWindow = null;
            }
            else if (!showCombatBoxFeatures)
            {
                _pilotRosterAutoStartedForCurrentConnection = false;
            }
            else if (showCombatBoxFeatures && (_pilotRosterWindow?.IsUnavailableMode == true))
            {
                EnsurePilotRosterWindow(showUnavailableMessage: false);
            }
            else if (showCombatBoxFeatures)
            {
                AutoStartPilotRoster();
            }
        }

        private void UpdateRciStatusIndicator()
        {
            UpdateRciStatusIndicator(ShouldShowRciStatus());
        }

        private void UpdateRciStatusIndicator(bool showRciStatus)
        {
            RciStatusPanel.Visibility = showRciStatus ? Visibility.Visible : Visibility.Collapsed;
            if (!showRciStatus)
            {
                ApplyRciDisplayState(RciDisplayState.Create(RciStatus.None, string.Empty));
                return;
            }

            var status = Clients.GetRciStatus(ClientState.PlayerGameState.coalition);
            ApplyRciDisplayState(RciDisplayState.Create(
                status,
                Clients.GetFriendlyRciCallsign(ClientState.PlayerGameState.coalition)));
        }

        private void ApplyRciDisplayState(RciDisplayState displayState)
        {
            RciStatusLabel.Text = displayState.StatusText;
            RciStatusLabel.Foreground = displayState.MainWindowStatusForeground;
            RciStatusIndicator.Background = displayState.StatusBackground;
            RciCallsignLabel.Text = displayState.RcoOnDutyText;
            RciCallsignLabel.Visibility = displayState.HasRcoOnDuty
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private bool ShouldShowRciStatus()
        {
            return ClientState.IsConnected
                   && string.Equals(GetAddressFromTextBox(), CombatBoxRciServerHost, StringComparison.OrdinalIgnoreCase);
        }

        private void SpeakerBoost_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var convertedValue = VolumeConversionHelper.ConvertVolumeSliderToScale((float) SpeakerBoost.Value);

            if (_audioPreview != null)
            {
                _audioPreview.SpeakerBoost = convertedValue;
            }
            if (_audioManager != null)
            {
                _audioManager.SpeakerBoost = convertedValue;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.SpeakerBoost,
                SpeakerBoost.Value.ToString(CultureInfo.InvariantCulture));


            if ((SpeakerBoostLabel != null) && (SpeakerBoost != null))
            {
                SpeakerBoostLabel.Content = VolumeConversionHelper.ConvertLinearDiffToDB(convertedValue);
            }
        }

        private void RadioSwitchPTT_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioSwitchIsPTT, (bool) RadioSwitchIsPTT.IsChecked);
        }

        private void ShowOverlay_OnClick(object sender, RoutedEventArgs e)
        {
            ToggleOverlay(true);
        }

        private static bool IsWindowOpen(Window window)
        {
            return window != null
                   && window.IsVisible
                   && window.WindowState != WindowState.Minimized;
        }

        private void UpdateWindowButtonLabels()
        {
            if (ToggleServerSettings != null)
            {
                ToggleServerSettings.Content = LocalizationManager.Get(
                    IsWindowOpen(_serverSettingsWindow) ? "Hide Server Settings" : "Show Server Settings");
            }

            if (ShowOverlayButton != null)
            {
                ShowOverlayButton.Content = LocalizationManager.Get(
                    IsWindowOpen(_radioOverlayWindow) ? "Hide Radio Overlay" : "Show Radio Overlay");
            }

            if (ShowClientList != null)
            {
                ShowClientList.Content = LocalizationManager.Get(
                    IsWindowOpen(_clientListWindow) ? "Hide Client List" : "Show Client List");
            }

            if (ShowPilotRoster != null)
            {
                ShowPilotRoster.Content = LocalizationManager.Get(
                    IsWindowOpen(_pilotRosterWindow) ? "Hide Pilot Roster" : "Show Pilot Roster");
            }
        }

        private void ToggleOverlay(bool uiButton)
        {
            //debounce show hide (1 tick = 100ns, 6000000 ticks = 600ms debounce)
            if ((DateTime.Now.Ticks - _toggleShowHide > 6000000) || uiButton)
            {
                _toggleShowHide = DateTime.Now.Ticks;
                if ((_radioOverlayWindow == null) || !_radioOverlayWindow.IsVisible ||
                    (_radioOverlayWindow.WindowState == WindowState.Minimized))
                {
                    ShowRadioOverlay();
                }
                else
                {
                    _radioOverlayWindow?.Close();
                    _radioOverlayWindow = null;
                    UpdateWindowButtonLabels();
                }
            }
        }

        private void AutoStartRadioOverlay()
        {
            if (!_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoStartRadioOverlay))
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(ShowRadioOverlay), DispatcherPriority.ContextIdle);
        }

        private void AutoStartPilotRoster()
        {
            if (_pilotRosterAutoStartedForCurrentConnection ||
                !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoStartPilotRoster))
            {
                return;
            }

            _pilotRosterAutoStartedForCurrentConnection = true;
            Dispatcher.BeginInvoke(new Action(() => EnsurePilotRosterWindow(showUnavailableMessage: !ShouldShowRciStatus())),
                DispatcherPriority.ContextIdle);
        }

        private void ShowRadioOverlay()
        {
            if (_radioOverlayWindow != null && _radioOverlayWindow.IsVisible &&
                _radioOverlayWindow.WindowState != WindowState.Minimized)
            {
                return;
            }

            _radioOverlayWindow?.Close();

            _radioOverlayWindow = new Overlay.RadioOverlayWindow
            {
                ShowInTaskbar = !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide),
                Opacity = GetOverlayOpacity(GlobalSettingsKeys.RadioOpacity)
            };
            _radioOverlayWindow.Closed += (sender, args) =>
            {
                if (ReferenceEquals(_radioOverlayWindow, sender))
                {
                    _radioOverlayWindow = null;
                }
                UpdateWindowButtonLabels();
            };
            _radioOverlayWindow.SetRciIndicatorEnabled(ShouldShowRciStatus());
            _radioOverlayWindow.Show();
            UpdateWindowButtonLabels();
        }

        private void InitOverlayOpacitySliders()
        {
            _initialisingOverlayOpacitySliders = true;
            try
            {
                RadioOverlayOpacitySlider.Value = GetOverlayOpacity(GlobalSettingsKeys.RadioOpacity);
                ClientListOpacitySlider.Value = GetOverlayOpacity(GlobalSettingsKeys.ClientListOpacity);
                PilotRosterOpacitySlider.Value = GetOverlayOpacity(GlobalSettingsKeys.PilotRosterOpacity);
            }
            finally
            {
                _initialisingOverlayOpacitySliders = false;
            }
        }

        private double GetOverlayOpacity(GlobalSettingsKeys key)
        {
            return _globalSettings.GetBoundedPositionSetting(key, DefaultOverlayOpacity, 0.05, 1.0);
        }

        private void SetOverlayOpacity(GlobalSettingsKeys key, Window window, double value)
        {
            if (_initialisingOverlayOpacitySliders)
            {
                return;
            }

            _globalSettings.SetPositionSetting(key, value);

            if (window != null)
            {
                window.Opacity = value;
            }
        }

        private void RadioOverlayOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetOverlayOpacity(GlobalSettingsKeys.RadioOpacity, _radioOverlayWindow, e.NewValue);
        }

        private void ClientListOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetOverlayOpacity(GlobalSettingsKeys.ClientListOpacity, _clientListWindow, e.NewValue);
        }

        private void PilotRosterOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetOverlayOpacity(GlobalSettingsKeys.PilotRosterOpacity, _pilotRosterWindow, e.NewValue);
        }

        private void InitWeatheringControls()
        {
            _initialisingWeatheringControls = true;
            try
            {
                var enabled = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.WeatheringEnabled);
                WeatheringEffectToggle.IsChecked = enabled;
                WeatheringOpacitySlider.Value = GetWeatheringOpacity();
                WeatheringOpacitySlider.IsEnabled = enabled;
            }
            finally
            {
                _initialisingWeatheringControls = false;
            }

            ApplyWeatheringOpacity();
        }

        private double GetWeatheringOpacity()
        {
            return _globalSettings.GetBoundedPositionSetting(
                GlobalSettingsKeys.WeatheringOpacity,
                DefaultWeatheringOpacity,
                0.0,
                2.0);
        }

        private void ApplyWeatheringOpacity()
        {
            var enabled = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.WeatheringEnabled);
            var rawOpacity = enabled ? GetWeatheringOpacity() : 0.0;
            var opacity = Math.Min(rawOpacity, 1.0);
            var extraOpacity = Math.Max(0.0, rawOpacity - 1.0);
            var overlayOpacity = opacity * 0.5;
            var overlayExtraOpacity = extraOpacity * 0.5;
            Application.Current.Resources[WeatheringOpacityResourceKey] = opacity;
            Application.Current.Resources[WeatheringExtraOpacityResourceKey] = extraOpacity;
            Application.Current.Resources[OverlayWeatheringOpacityResourceKey] = overlayOpacity;
            Application.Current.Resources[OverlayWeatheringExtraOpacityResourceKey] = overlayExtraOpacity;
            Resources[WeatheringOpacityResourceKey] = opacity;
            Resources[WeatheringExtraOpacityResourceKey] = extraOpacity;
            Resources[OverlayWeatheringOpacityResourceKey] = overlayOpacity;
            Resources[OverlayWeatheringExtraOpacityResourceKey] = overlayExtraOpacity;

            foreach (Window window in Application.Current.Windows)
            {
                window.Resources[WeatheringOpacityResourceKey] = opacity;
                window.Resources[WeatheringExtraOpacityResourceKey] = extraOpacity;
                window.Resources[OverlayWeatheringOpacityResourceKey] = overlayOpacity;
                window.Resources[OverlayWeatheringExtraOpacityResourceKey] = overlayExtraOpacity;
            }

            if (WeatheringOpacitySlider != null)
            {
                WeatheringOpacitySlider.IsEnabled = enabled;
            }
        }

        private void WeatheringEffectToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_initialisingWeatheringControls)
            {
                return;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.WeatheringEnabled, (bool)WeatheringEffectToggle.IsChecked);
            ApplyWeatheringOpacity();
        }

        private void WeatheringOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_initialisingWeatheringControls)
            {
                return;
            }

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.WeatheringOpacity, e.NewValue);
            ApplyWeatheringOpacity();
        }

     
        private void AutoConnect(SRSAddressMessage message)
        {
            string connection = message.SRSAddress;

            Logger.Info($"Received AutoConnect IL2-SRS @ {connection}");

            if (ClientState.IsConnected)
            {
                string currentConnection = ServerIp.Text.Trim();

                if (string.Equals(connection, currentConnection, StringComparison.OrdinalIgnoreCase))
                {
                    // Current connection matches SRS server advertised by IL2, all good
                    Logger.Info($"Current SRS connection {currentConnection} matches advertised server {connection}, ignoring autoconnect");
                }
                else 
                {
                    // Port mismatch, will always be a different server, no need to perform hostname lookups
                    HandleAutoConnectMismatch(currentConnection, connection);
                }
            }
            else
            {
                // Show auto connect prompt if client is not connected yet and setting has been enabled, otherwise automatically connect
                bool showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt);

                bool connectToServer = !showPrompt;
                if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectPrompt))
                {
                    WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                    var result = ShowLocalizedYesNo(
                        $"Would you like to try to auto-connect to IL2-SRS @ {connection}? ", "Auto Connect",
                        MessageBoxImage.Question);

                    connectToServer = (result == MessageBoxResult.Yes) && !ClientState.IsConnected;
                }

                if (connectToServer)
                {
                    ServerIp.Text = connection;
                    Connect();
                }
            }
        }

        private async void HandleAutoConnectMismatch(string currentConnection, string advertisedConnection)
        {
            // Show auto connect mismatch prompt if setting has been enabled (default), otherwise automatically switch server
            bool showPrompt = _globalSettings.GetClientSettingBool(GlobalSettingsKeys.AutoConnectMismatchPrompt);

            Logger.Info($"Current SRS connection {currentConnection} does not match advertised server {advertisedConnection}, {(showPrompt ? "displaying mismatch prompt" : "automatically switching server")}");

            bool switchServer = !showPrompt;
            if (showPrompt)
            {
                WindowHelper.BringProcessToFront(Process.GetCurrentProcess());

                var result = ShowLocalizedYesNo(
                    $"The SRS server advertised by IL2 @ {advertisedConnection} does not match the SRS server @ {currentConnection} you are currently connected to.\n\n" +
                    $"Would you like to connect to the advertised SRS server?",
                    "Auto Connect Mismatch",
                    MessageBoxImage.Warning);

                switchServer = result == MessageBoxResult.Yes;
            }

            if (switchServer)
            {
                Stop();

                StartStop.IsEnabled = false;
                StartStop.Content = LocalizationManager.Get("Connecting...");
                await Task.Delay(2000);
                StartStop.IsEnabled = true;
                ServerIp.Text = advertisedConnection;
                Connect();
            }
        }

        private void ResetRadioWindow_Click(object sender, RoutedEventArgs e)
        {
            //close overlay
            _radioOverlayWindow?.Close();
            _radioOverlayWindow = null;

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, 300);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY,300);
                            
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, 260);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, 300);
                         
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity, 1.0);
            if (RadioOverlayOpacitySlider != null)
            {
                RadioOverlayOpacitySlider.Value = 1.0;
            }
        }

        private void ToggleServerSettings_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_serverSettingsWindow == null) || !_serverSettingsWindow.IsVisible ||
                (_serverSettingsWindow.WindowState == WindowState.Minimized))
            {
                _serverSettingsWindow?.Close();

                _serverSettingsWindow = new ServerSettingsWindow();
                _serverSettingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _serverSettingsWindow.Owner = this;
                _serverSettingsWindow.Closed += (closedSender, args) =>
                {
                    if (ReferenceEquals(_serverSettingsWindow, closedSender))
                    {
                        _serverSettingsWindow = null;
                    }
                    UpdateWindowButtonLabels();
                };
                _serverSettingsWindow.Show();
                UpdateWindowButtonLabels();
            }
            else
            {
                _serverSettingsWindow?.Close();
                _serverSettingsWindow = null;
                UpdateWindowButtonLabels();
            }
        }

        private void AutoConnectPromptToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnectPrompt,(bool) AutoConnectPromptToggle.IsChecked);
        }

        private void AutoConnectMismatchPromptToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoConnectMismatchPrompt, (bool) AutoConnectMismatchPromptToggle.IsChecked);
        }

        private void RadioOverlayTaskbarItem_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RadioOverlayTaskbarHide, (bool) RadioOverlayTaskbarItem.IsChecked);

            if (_radioOverlayWindow != null)
                _radioOverlayWindow.ShowInTaskbar = !_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide);
         
        }

        private void AutoStartRadioOverlayToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoStartRadioOverlay, (bool) AutoStartRadioOverlayToggle.IsChecked);
        }

        private void AutoStartPilotRosterToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AutoStartPilotRoster, (bool)AutoStartPilotRosterToggle.IsChecked);
        }

        private void IL2Refocus_OnClick_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RefocusIL2, (bool) RefocusIL2.IsChecked);
        }

        private void ExpandInputDevices_OnClick_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "You must restart SRS for this setting to take effect.\n\nTurning this on will allow almost any DirectX device to be used as input expect a Mouse but may cause issues with other devices being detected",
                "Restart SimpleRadio Standalone", MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _globalSettings.SetClientSetting(GlobalSettingsKeys.ExpandControls, (bool) ExpandInputDevices.IsChecked);
        }

        private void MicAGC_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.AGC, (bool) MicAGC.IsChecked);
        }

        private void MicDenoise_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.Denoise, (bool) MicDenoise.IsChecked);
        }

        private void RadioSoundEffects_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEffects,
                (bool) RadioSoundEffects.IsChecked);
        }

        private void RadioTxStart_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioTxEffects_Start,(bool) RadioTxStartToggle.IsChecked);
        }

        private void RadioTxEnd_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioTxEffects_End,(bool) RadioTxEndToggle.IsChecked);
        }

        private void RadioRxStart_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioRxEffects_Start,(bool) RadioRxStartToggle.IsChecked);
        }

        private void RadioRxEnd_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioRxEffects_End, (bool) RadioRxEndToggle.IsChecked);
        }

        private void RadioSoundEffectsClipping_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.RadioEffectsClipping,
                (bool) RadioSoundEffectsClipping.IsChecked);

        }

        private void MinimiseToTray_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.MinimiseToTray, (bool) MinimiseToTray.IsChecked);
        }

        private void StartMinimised_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.StartMinimised,(bool)StartMinimised.IsChecked);
        }

        private void CheckForBetaUpdates_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.CheckForBetaUpdates,(bool)CheckForBetaUpdates.IsChecked);
        }

        private void ThreeDEffectsToggle_Click(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.ThreeDEffectsEnabled, (bool)ThreeDEffectsToggle.IsChecked);
            IL2_SR_Client.App.ApplyThreeDEffectSetting();
        }

        private void PlayConnectionSounds_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.PlayConnectionSounds, (bool)PlayConnectionSounds.IsChecked);
        }

    
        private void RescanInputDevices(object sender, RoutedEventArgs e)
        {
            InputManager.InitDevices();
            MessageBox.Show(this,
                LocalizationManager.Get("Input Devices Rescanned"),
                LocalizationManager.Get("New input devices can now be used."),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LanguagePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLanguage = LanguagePicker.SelectedItem as LanguageOption;
            if (_initialisingLanguagePicker || selectedLanguage == null)
            {
                return;
            }

            var currentLanguage = LocalizationManager.NormalizeLanguage(
                _globalSettings.GetClientSetting(GlobalSettingsKeys.Language).RawValue);
            if (selectedLanguage.Code == currentLanguage)
            {
                return;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.Language, selectedLanguage.Code);
            var restartResult = ShowLocalizedYesNo(
                LocalizationManager.Get("Please restart SRS for the language change to take effect.") + "\n\n" +
                LocalizationManager.Get("Restart SRS now to apply the language change?"),
                LocalizationManager.Get("Restart Required"),
                MessageBoxImage.Question);

            if (restartResult == MessageBoxResult.Yes)
            {
                RestartClient();
            }
        }

        private void RestartClient()
        {
            try
            {
                var executablePath = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    Arguments = BuildRestartArguments(),
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unable to restart SRS after language change");
                MessageBox.Show(this,
                    LocalizationManager.Get("Please restart SRS for the language change to take effect."),
                    LocalizationManager.Get("Restart Required"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private MessageBoxResult ShowLocalizedYesNo(string message, string caption, MessageBoxImage icon)
        {
            return CustomMessageBox.ShowYesNo(
                message,
                caption,
                LocalizationManager.Get("Yes"),
                LocalizationManager.Get("No"),
                icon);
        }

        private static string BuildRestartArguments()
        {
            var args = Environment.GetCommandLineArgs()
                .Skip(1)
                .Where(arg => !string.Equals(arg, "-allowMultiple", StringComparison.OrdinalIgnoreCase))
                .Select(QuoteCommandLineArgument)
                .ToList();

            args.Add("-allowMultiple");
            return string.Join(" ", args);
        }

        private static string QuoteCommandLineArgument(string argument)
        {
            if (argument == null)
            {
                return "\"\"";
            }

            if (argument.Length > 0 && argument.IndexOfAny(new[] {' ', '\t', '\n', '\v', '"'}) < 0)
            {
                return argument;
            }

            var quoted = new System.Text.StringBuilder();
            quoted.Append('"');

            var backslashes = 0;
            foreach (var character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    quoted.Append('\\', (backslashes * 2) + 1);
                    quoted.Append('"');
                    backslashes = 0;
                    continue;
                }

                quoted.Append('\\', backslashes);
                backslashes = 0;
                quoted.Append(character);
            }

            quoted.Append('\\', backslashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }

        private void SetSRSPath_Click(object sender, RoutedEventArgs e)
        {
            Registry.SetValue("HKEY_CURRENT_USER\\SOFTWARE\\IL2-SR-Standalone","SRPathStandalone",Directory.GetCurrentDirectory());

            MessageBox.Show(this,
                "SRS Path set to: " + Directory.GetCurrentDirectory(),
                "SRS Client Path",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void RequireAdminToggle_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RequireAdmin, (bool)RequireAdminToggle.IsChecked);
            MessageBox.Show(this,
                "SRS Requires admin rights to be able to read keyboard input in the background. \n\nIf you do not use any keyboard binds you can disable SRS Admin Privileges. \n\nFor this setting to take effect SRS must be restarted",
                "SRS Admin Privileges", MessageBoxButton.OK, MessageBoxImage.Warning);

        }

        private void CreateProfile(object sender, RoutedEventArgs e)
        {
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        _globalSettings.ProfileSettingsStore.AddNewProfile(name);
                        InitSettingsProfiles();
                     
                    }
                });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = this;
            inputProfileWindow.ShowDialog();
        }

        private void DeleteProfile(object sender, RoutedEventArgs e)
        {
            var current = ControlsProfile.SelectedValue as string;

            if (current.Equals("default"))
            {
                MessageBox.Show(this,
                    "Cannot delete the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var result = ShowLocalizedYesNo(
                    $"Are you sure you want to delete {current} ?",
                    "Confirmation",
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    ControlsProfile.SelectedIndex = 0;
                    _globalSettings.ProfileSettingsStore.RemoveProfile(current);
                    InitSettingsProfiles();
                }

            }

        }

        private void RenameProfile(object sender, RoutedEventArgs e)
        {

            var current = ControlsProfile.SelectedValue as string;
            if (current.Equals("default"))
            {
                MessageBox.Show(this,
                    "Cannot rename the default input!",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                var oldName = current;
                var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
                {
                    if (name.Trim().Length > 0)
                    {
                        _globalSettings.ProfileSettingsStore.RenameProfile(oldName,name);
                        InitSettingsProfiles();
                    }
                }, true,oldName);
                inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                inputProfileWindow.Owner = this;
                inputProfileWindow.ShowDialog();
            }

        }


        private void CopyProfile(object sender, RoutedEventArgs e)
        {
            var current = ControlsProfile.SelectedValue as string;
            var inputProfileWindow = new InputProfileWindow.InputProfileWindow(name =>
            {
                if (name.Trim().Length > 0)
                {
                    _globalSettings.ProfileSettingsStore.CopyProfile(current,name);
                    InitSettingsProfiles();
                }
            });
            inputProfileWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            inputProfileWindow.Owner = this;
            inputProfileWindow.ShowDialog();
        }

        private void ShowClientList_OnClick(object sender, RoutedEventArgs e)
        {
            if ((_clientListWindow == null) || !_clientListWindow.IsVisible ||
                (_clientListWindow.WindowState == WindowState.Minimized))
            {
                _clientListWindow?.Close();

                _clientListWindow = new ClientListWindow
                {
                    Opacity = GetOverlayOpacity(GlobalSettingsKeys.ClientListOpacity)
                };
                _clientListWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _clientListWindow.Owner = this;
                _clientListWindow.Closed += (closedSender, args) =>
                {
                    if (ReferenceEquals(_clientListWindow, closedSender))
                    {
                        _clientListWindow = null;
                    }
                    UpdateWindowButtonLabels();
                };
                _clientListWindow.Show();
                UpdateWindowButtonLabels();
            }
            else
            {
                _clientListWindow?.Close();
                _clientListWindow = null;
                UpdateWindowButtonLabels();
            }
        }

        private void ShowPilotRoster_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ShouldShowRciStatus())
            {
                ShowPilotRosterWindow(showUnavailableMessage: true);
                return;
            }

            ShowPilotRosterWindow(showUnavailableMessage: false);
        }

        private void ShowPilotRosterWindow(bool showUnavailableMessage)
        {
            if ((_pilotRosterWindow == null) || !_pilotRosterWindow.IsVisible ||
                (_pilotRosterWindow.WindowState == WindowState.Minimized) ||
                (_pilotRosterWindow.IsUnavailableMode != showUnavailableMessage))
            {
                EnsurePilotRosterWindow(showUnavailableMessage);
            }
            else
            {
                _pilotRosterWindow?.Close();
                _pilotRosterWindow = null;
                UpdateWindowButtonLabels();
            }
        }

        private void EnsurePilotRosterWindow(bool showUnavailableMessage)
        {
            if (_pilotRosterWindow != null && _pilotRosterWindow.IsVisible &&
                _pilotRosterWindow.WindowState != WindowState.Minimized &&
                _pilotRosterWindow.IsUnavailableMode == showUnavailableMessage)
            {
                return;
            }

            _pilotRosterWindow?.Close();

            _pilotRosterWindow = new PilotRosterWindow(showUnavailableMessage)
            {
                Opacity = GetOverlayOpacity(GlobalSettingsKeys.PilotRosterOpacity),
                Owner = this
            };
            _pilotRosterWindow.WindowStartupLocation = WindowStartupLocation.Manual;
            _pilotRosterWindow.Closed += (closedSender, args) =>
            {
                if (ReferenceEquals(_pilotRosterWindow, closedSender))
                {
                    _pilotRosterWindow = null;
                }
                UpdateWindowButtonLabels();
            };
            _pilotRosterWindow.Show();
            UpdateWindowButtonLabels();
        }

        private void ShowTransmitterName_OnClick_OnClick(object sender, RoutedEventArgs e)
        { 
            _globalSettings.SetClientSetting(GlobalSettingsKeys.ShowTransmitterName, ((bool)ShowTransmitterName.IsChecked).ToString());
        }

        private void Donate_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenExternalUrl("https://www.patreon.com/ciribob");
            }
            catch (Exception ex)
            {
            }
        }

        private void ReportProblem_OnClick(object sender, RoutedEventArgs e)
        {
            var result = ShowLocalizedYesNo(
                LocalizedOrDefault(
                    "DiagnosticBundlePrivacyWarning",
                    "The diagnostic bundle may include device names, server addresses, player names, and recent connection/audio events. Please review it before attaching it publicly."),
                LocalizationManager.Get("Report a Problem"),
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                var bundle = DiagnosticBundleBuilder.Create(
                    RedactDiagnosticDetails.IsChecked == true,
                    ServerAddress?.Name,
                    ServerIp.Text);

                OpenFileInExplorer(bundle.ZipPath);
                OpenExternalUrl(DiagnosticBundleBuilder.BuildBugReportUrl(bundle));

                MessageBox.Show(this,
                    LocalizedFormatOrDefault(
                        "DiagnosticBundleCreatedMessage",
                        "Diagnostic bundle created at:\n{0}\n\nAttach this ZIP to the GitHub issue that opened in your browser.",
                        bundle.ZipPath),
                    LocalizationManager.Get("Report a Problem"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to create diagnostic bundle");
                MessageBox.Show(this,
                    LocalizedOrDefault(
                        "DiagnosticBundleCreateFailed",
                        "Failed to create the diagnostic bundle. Please attach clientlog.txt manually when reporting the issue."),
                    LocalizationManager.Get("Report a Problem"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SuggestImprovement_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenExternalUrl(DiagnosticBundleBuilder.BuildFeatureRequestUrl());
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to open feature request page");
                MessageBox.Show(this,
                    LocalizedOrDefault(
                        "SuggestionPageOpenFailed",
                        "Failed to open the GitHub suggestion page."),
                    LocalizationManager.Get("Suggest an Improvement"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void OpenExternalUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        private static void OpenFileInExplorer(string path)
        {
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true });
        }

        private static string LocalizedOrDefault(string key, string fallback)
        {
            var localized = LocalizationManager.Get(key);
            return string.Equals(localized, key, StringComparison.Ordinal) ? fallback : localized;
        }

        private static string LocalizedFormatOrDefault(string key, string fallback, params object[] args)
        {
            return string.Format(CultureInfo.CurrentUICulture, LocalizedOrDefault(key, fallback), args);
        }

        private void EnableTextToSpeech_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.EnableTextToSpeech, (bool)EnableTextToSpeech.IsChecked);
        }

        private void EnableRadioWrap_OnClick(object sender, RoutedEventArgs e)
        {
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.WrapNextRadio, (bool)WrapNextRadio.IsChecked);
        }

        private void TextToSpeechVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if(TextToSpeechVolume.IsEnabled)
                _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.TextToSpeechVolume, e.NewValue.ToString(CultureInfo.InvariantCulture));
        }

        private void SelectedRadioMutedVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SelectedRadioMutedVolume.IsEnabled)
                _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.SelectedRadioMutedVolume, e.NewValue.ToString(CultureInfo.InvariantCulture));
        }

        private void PushToTalkReleaseDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PTTReleaseDelay.IsEnabled)
                _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.PTTReleaseDelay, e.NewValue.ToString(CultureInfo.InvariantCulture));
        }
    }
}
