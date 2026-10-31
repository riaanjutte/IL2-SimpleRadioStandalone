using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>RCI Controller mode in the main window: the settings panel, the server lock and the code checks.</summary>
    public partial class MainWindow
    {
        private const string RciControllerProfileName = "RCI Controller";

        /// <summary>Called from the constructor, before the server address and IL-2 telemetry are set up.</summary>
        private void InitRciController()
        {
            RciControllerGroup.Visibility = RciControllerMode.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
            RciControllerToggle.IsChecked = RciControllerMode.IsEnabledInSettings;
            RciAccessCodeBox.Text = _globalSettings.GetClientSetting(GlobalSettingsKeys.RciAccessCode).RawValue ?? string.Empty;
            RciControllerMode.ActiveChanged += OnRciControllerModeChanged;

            var result = RciControllerMode.Refresh(DateTime.UtcNow);
            UpdateRciControllerStatus(result);

            if (result != null && !result.IsValid)
            {
                Logger.Warn($"RCI Controller mode is switched on but the access code is not usable: {result.Status}");
                MessageBox.Show(
                    LocalizationManager.Format("RCI Controller mode is off for now: {0}", DescribeRciCodeProblem(result.Status)) +
                    "\n\n" + LocalizationManager.Get("Paste a new access code under Settings > RCI Controller."),
                    LocalizationManager.Get("RCI Controller"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void RciControllerToggle_Click(object sender, RoutedEventArgs e)
        {
            var enabled = RciControllerToggle.IsChecked == true;
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RciControllerEnabled, enabled);
            ApplyRciControllerSettings();
        }

        private void RciUseCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var code = (RciAccessCodeBox.Text ?? string.Empty).Trim();
            RciAccessCodeBox.Text = code;
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RciAccessCode, code);

            // Using a code means using the mode.
            RciControllerToggle.IsChecked = true;
            _globalSettings.SetClientSetting(GlobalSettingsKeys.RciControllerEnabled, true);
            ApplyRciControllerSettings();
        }

        private void ApplyRciControllerSettings()
        {
            var wasActive = RciControllerMode.IsActive;
            var result = RciControllerMode.Refresh(DateTime.UtcNow);
            UpdateRciControllerStatus(result);

            if (!wasActive && RciControllerMode.IsActive)
            {
                OfferRciControllerProfile();
            }
        }

        private void OnRciControllerModeChanged()
        {
            Dispatcher.Invoke(() =>
            {
                ApplyRciServerLock();
                UpdateRciControllerStatus(null);
                UpdateConnectionStatusStrip();

                // A controller only talks on Combat Box; leave any other server when the mode starts.
                if (RciControllerMode.IsActive && ClientState.IsConnected && !IsCombatBoxAddress(ServerIp.Text))
                {
                    Logger.Info("RCI Controller mode started while connected to another server; disconnecting");
                    Stop();
                }
            });
        }

        /// <summary>While the mode is active the server is fixed to Combat Box; otherwise the favourites apply.</summary>
        private void ApplyRciServerLock()
        {
            var active = RciControllerMode.IsActive;
            ServerIp.IsEnabled = !active;
            ServerAddressPicker.IsEnabled = !active;

            if (active)
            {
                ServerIp.Text = RciControllerMode.ServerAddress;
            }
            else if (ServerAddress != null && !ClientState.IsConnected)
            {
                ServerIp.Text = ServerAddress.Address;
            }
        }

        /// <summary>Checked before connecting: an expired or missing code stops a controller connecting.</summary>
        private bool CanConnectAsRciController()
        {
            if (!RciControllerMode.IsEnabledInSettings)
            {
                return true;
            }

            var result = RciControllerMode.Refresh(DateTime.UtcNow);
            UpdateRciControllerStatus(result);
            if (result != null && result.IsValid)
            {
                ServerIp.Text = RciControllerMode.ServerAddress;
                return true;
            }

            MessageBox.Show(this,
                LocalizationManager.Format("Cannot connect as an RCI controller: {0}", DescribeRciCodeProblem(result?.Status ?? RciAccessCodeStatus.Missing)) +
                "\n\n" + LocalizationManager.Get("Paste a new access code under Settings > RCI Controller, or switch RCI Controller mode off."),
                LocalizationManager.Get("RCI Controller"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        /// <summary>IL-2 may advertise another server; a controller stays on Combat Box.</summary>
        private static bool ShouldIgnoreAutoConnect(string advertisedAddress)
        {
            return RciControllerMode.IsActive && !IsCombatBoxAddress(advertisedAddress);
        }

        private static bool IsCombatBoxAddress(string address)
        {
            return RciControllerMode.IsControllerServer(address);
        }

        private void UpdateRciControllerStatus(RciAccessCodeResult result)
        {
            if (RciControllerStatusText == null)
            {
                return;
            }

            var code = RciControllerMode.ActiveCode;
            if (code != null)
            {
                RciControllerStatusText.Text = LocalizationManager.Format(
                    "Active as {0} ({1}) until {2}.",
                    code.RadioName,
                    LocalizationManager.Get(code.Coalition == 1 ? "Allies" : "Axis"),
                    code.ExpiresUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture));
                return;
            }

            if (!RciControllerMode.IsEnabledInSettings)
            {
                RciControllerStatusText.Text = LocalizationManager.Get(
                    "Switch on to use SRS as a Combat Box RCI controller without IL-2. You need a personal access code from the RCI dashboard.");
                return;
            }

            RciControllerStatusText.Text = result != null
                ? DescribeRciCodeProblem(result.Status)
                : LocalizationManager.Get("Paste your personal access code from the Combat Box RCI dashboard.");
        }

        private static string DescribeRciCodeProblem(RciAccessCodeStatus status)
        {
            switch (status)
            {
                case RciAccessCodeStatus.Missing:
                    return LocalizationManager.Get("Paste your personal access code from the Combat Box RCI dashboard.");
                case RciAccessCodeStatus.NoKeysInBuild:
                    return LocalizationManager.Get("This version of SRS cannot check RCI access codes yet.");
                case RciAccessCodeStatus.Expired:
                    return LocalizationManager.Get("Your access code has expired. Get a new one from the RCI dashboard.");
                case RciAccessCodeStatus.BadSignature:
                case RciAccessCodeStatus.UnknownKey:
                    return LocalizationManager.Get("That access code was not issued by the Combat Box RCI dashboard.");
                default:
                    return LocalizationManager.Get("That is not a valid access code. Copy it again from the RCI dashboard.");
            }
        }

        /// <summary>
        /// The first time the mode starts, offers a copy of the current profile (same key bindings) with Radio 1 in
        /// the left ear and Radio 2 in the right, as RCI controllers usually listen.
        /// </summary>
        private void OfferRciControllerProfile()
        {
            var profiles = _globalSettings.ProfileSettingsStore.GetProfiles();
            if (profiles.Any(profile => string.Equals(profile, RciControllerProfileName, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var answer = ShowLocalizedYesNo(
                LocalizationManager.Get("Create an \"RCI Controller\" profile with your current key bindings, Radio 1 in your left ear and Radio 2 in your right?"),
                LocalizationManager.Get("RCI Controller"),
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            var current = ControlsProfile.SelectedValue as string ?? _globalSettings.ProfileSettingsStore.CurrentProfileName;
            _globalSettings.ProfileSettingsStore.CopyProfile(current, RciControllerProfileName);
            InitSettingsProfiles();
            ControlsProfile.SelectedItem = RciControllerProfileName;

            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.Radio1Channel, "-1");
            _globalSettings.ProfileSettingsStore.SetClientSetting(ProfileSettingsKeys.Radio2Channel, "1");
            ReloadProfile();
            Logger.Info("Created the RCI Controller profile");
        }
    }
}
