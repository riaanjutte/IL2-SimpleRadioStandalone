using System.Windows;
using System.Windows.Controls;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    ///     Interaction logic for InputBindingControl.xaml
    /// </summary>
    public partial class InputBindingControl : UserControl
    {
        private InputDeviceManager _inputDeviceManager;

        public InputBindingControl()
        {
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
        }

        public InputDeviceManager InputDeviceManager
        {
            get { return _inputDeviceManager; }
            set
            {
                _inputDeviceManager = value;
                LoadInputSettings();
            }
        }

        public InputBinding ControlInputBinding { get; set; }
        public InputBinding ModifierBinding { get; set; }
        public string InputName { get; set; }

        public bool IsAxis => VolumeAxis.IsVolumeAxisBinding(ControlInputBinding);

        public void LoadInputSettings()
        {
            DeviceLabel.Text = InputName;
            ModifierLabel.Text = InputName + " " + LocalizationManager.Get("Modifier");
            ModifierBinding = (InputBinding) ((int) ControlInputBinding) + 100; //add 100 gets the enum of the modifier

            ApplyAxisLayout();

            var currentInputProfile = GlobalSettingsStore.Instance.ProfileSettingsStore.GetCurrentInputProfile();

            if (currentInputProfile != null)
            {
                var devices = currentInputProfile;
                if (currentInputProfile.ContainsKey(ControlInputBinding))
                {
                    DeviceText.Text = FormatInput(devices[ControlInputBinding]);
                    Device.Text = devices[ControlInputBinding].DeviceName;
                    InvertAxis.IsChecked = IsAxis && VolumeAxis.IsInverted(devices[ControlInputBinding]);
                }
                else
                {
                    DeviceText.Text = LocalizationManager.Get("None");
                    Device.Text = LocalizationManager.Get("None");
                    InvertAxis.IsChecked = false;
                }

                if (!IsAxis && currentInputProfile.ContainsKey(ModifierBinding))
                {
                    ModifierText.Text = FormatInput(devices[ModifierBinding]);
                    ModifierDevice.Text = devices[ModifierBinding].DeviceName;
                }
                else
                {
                    ModifierText.Text = LocalizationManager.Get("None");
                    ModifierDevice.Text = LocalizationManager.Get("None");
                }
            }
        }

        private void ApplyAxisLayout()
        {
            var modifierVisibility = IsAxis ? Visibility.Collapsed : Visibility.Visible;
            ModifierLabel.Visibility = modifierVisibility;
            ModifierDevice.Visibility = modifierVisibility;
            ModifierText.Visibility = modifierVisibility;
            ModifierButton.Visibility = modifierVisibility;
            ModifierButtonClear.Visibility = modifierVisibility;

            InvertAxis.Visibility = IsAxis ? Visibility.Visible : Visibility.Collapsed;
        }

        private string FormatInput(InputDevice device)
        {
            if (IsAxis)
            {
                return VolumeAxis.GetAxisName(device.Button);
            }

            //output POV info
            return device.Button < 128 ? (device.Button + 1).ToString() : "POV " + (device.Button - 127);
        }

        private void Device_Click(object sender, RoutedEventArgs e)
        {
            DeviceClear.IsEnabled = false;
            DeviceButton.IsEnabled = false;

            InputDeviceManager.DetectButton onDetected = device =>
            {
                DeviceClear.IsEnabled = true;
                DeviceButton.IsEnabled = true;

                Device.Text = device.DeviceName;
                DeviceText.Text = FormatInput(device);

                device.InputBind = ControlInputBinding;

                if (IsAxis)
                {
                    device.ButtonValue = InvertAxis.IsChecked == true ? 1 : 0;
                }

                GlobalSettingsStore.Instance.ProfileSettingsStore.SetControlSetting(device);
            };

            if (IsAxis)
            {
                InputDeviceManager.AssignAxis(onDetected, () =>
                {
                    DeviceClear.IsEnabled = true;
                    DeviceButton.IsEnabled = true;
                });
            }
            else
            {
                InputDeviceManager.AssignButton(onDetected);
            }
        }


        private void DeviceClear_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.RemoveControlSetting(ControlInputBinding);

            Device.Text = LocalizationManager.Get("None");
            DeviceText.Text = LocalizationManager.Get("None");
        }

        private void InvertAxis_Click(object sender, RoutedEventArgs e)
        {
            var currentInputProfile = GlobalSettingsStore.Instance.ProfileSettingsStore.GetCurrentInputProfile();
            InputDevice device;
            if (currentInputProfile == null || !currentInputProfile.TryGetValue(ControlInputBinding, out device))
            {
                // Nothing bound yet: the choice is applied when an axis is set.
                return;
            }

            device.ButtonValue = InvertAxis.IsChecked == true ? 1 : 0;
            GlobalSettingsStore.Instance.ProfileSettingsStore.SetControlSetting(device);
        }

        private void Modifier_Click(object sender, RoutedEventArgs e)
        {
            ModifierButtonClear.IsEnabled = false;
            ModifierButton.IsEnabled = false;

            InputDeviceManager.AssignButton(device =>
            {
                ModifierButtonClear.IsEnabled = true;
                ModifierButton.IsEnabled = true;

                ModifierDevice.Text = device.DeviceName;
                ModifierText.Text = FormatInput(device);

                device.InputBind = ModifierBinding;

                GlobalSettingsStore.Instance.ProfileSettingsStore.SetControlSetting(device);
            });
        }


        private void ModifierClear_Click(object sender, RoutedEventArgs e)
        {
            GlobalSettingsStore.Instance.ProfileSettingsStore.RemoveControlSetting(ModifierBinding);
            ModifierDevice.Text = LocalizationManager.Get("None");
            ModifierText.Text = LocalizationManager.Get("None");
        }
    }
}
