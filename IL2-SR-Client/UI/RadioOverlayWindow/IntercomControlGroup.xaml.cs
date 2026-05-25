using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for IntercomControlGroup.xaml
    /// </summary>
    public partial class IntercomControlGroup : UserControl
    {
        private const string IntercomLabelText = "INTERCOM";
        private const int MaxIntercomSpeakerNameLength = 14;
        private bool _dragging;
        private bool _syncingSliderFromState;
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;

        public IntercomControlGroup()
        {
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
        }
        public int RadioId { private get; set; }

        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            RadioHelper.SelectRadio(0);
        }

        private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
        {
            _dragging = true;
        }


        private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
        {
            SetVolumeFromSlider();
            _dragging = false;
        }

        private void RadioVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            SetVolumeFromSlider();
        }

        private void SetVolumeFromSlider()
        {
            if (!IsLoaded || _syncingSliderFromState || _clientStateSingleton.PlayerGameState == null)
            {
                return;
            }

            RadioHelper.SetRadioVolume((float)RadioVolume.Value / 100.0f, RadioId);
        }

        internal void RepaintRadioStatus()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            if ((IL2PlayerRadioInfo == null) || !_clientStateSingleton.IsConnected)
            {
                RadioActive.Fill = CreateStatusBrush(Colors.Red);
                RadioLabel.Content = IntercomLabelText;
                RadioLabel.FontSize = 8;
                RadioLabel.ToolTip = null;

                TunedCount.Content = "";
                IntercomUsersCount.Text = "0";

                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                var currentRadio = IL2PlayerRadioInfo.radios[RadioId];
                var transmitting = _clientStateSingleton.RadioSendingState;
                var receiving = _clientStateSingleton.RadioReceivingState[0];
                UpdateIntercomLabel(receiving);

                if (RadioId == IL2PlayerRadioInfo.selected || transmitting.IsSending && (transmitting.SendingOn == RadioId))
                {
                    if (transmitting.IsSending && (transmitting.SendingOn == RadioId))
                    {
                        RadioActive.Fill = CreateStatusBrush((Color)ColorConverter.ConvertFromString("#96FF6D"));
                    }
                    else
                    {
                        RadioActive.Fill = CreateStatusBrush(Colors.Green);
                    }
                    if (receiving!=null && receiving.IsReceiving)
                    {
                        RadioLabel.Foreground = new SolidColorBrush(Colors.White);
                        TunedCount.Foreground = RadioLabel.Foreground;
                    }
                    else
                    {
                        RadioLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                        TunedCount.Foreground = RadioLabel.Foreground;
                    }
                }
                else
                {
                    RadioActive.Fill = CreateStatusBrush(Colors.Orange);
                }

                int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);
                IntercomUsersCount.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

                if (count > 0)
                {
                    TunedCount.Content = "👤" + count;
                }
                else
                {
                    TunedCount.Content = "";
                }

                if (_dragging == false)
                {
                    _syncingSliderFromState = true;
                    RadioVolume.Value = currentRadio.volume * 100.0;
                    _syncingSliderFromState = false;
                }
            }
        }

        private void UpdateIntercomLabel(RadioReceivingState receiving)
        {
            if (receiving != null && receiving.IsReceiving && !string.IsNullOrWhiteSpace(receiving.SentBy))
            {
                RadioLabel.Content = TrimSpeakerName(receiving.SentBy);
                RadioLabel.FontSize = 7;
                RadioLabel.ToolTip = receiving.SentBy;
                return;
            }

            RadioLabel.Content = IntercomLabelText;
            RadioLabel.FontSize = 8;
            RadioLabel.ToolTip = null;
        }

        private static string TrimSpeakerName(string speakerName)
        {
            if (speakerName.Length <= MaxIntercomSpeakerNameLength)
            {
                return speakerName;
            }

            return speakerName.Substring(0, MaxIntercomSpeakerNameLength - 3) + "...";
        }

        private static Brush CreateStatusBrush(Color color)
        {
            return new RadialGradientBrush
            {
                GradientOrigin = new Point(0.32, 0.25),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.72,
                RadiusY = 0.72,
                GradientStops =
                {
                    new GradientStop(Lighten(color, 150), 0.0),
                    new GradientStop(Lighten(color, 40), 0.28),
                    new GradientStop(color, 0.58),
                    new GradientStop(Darken(color, 95), 1.0)
                }
            };
        }

        private static Color Lighten(Color color, byte amount)
        {
            return Color.FromArgb(color.A,
                Add(color.R, amount),
                Add(color.G, amount),
                Add(color.B, amount));
        }

        private static Color Darken(Color color, byte amount)
        {
            return Color.FromArgb(color.A,
                Subtract(color.R, amount),
                Subtract(color.G, amount),
                Subtract(color.B, amount));
        }

        private static byte Add(byte value, byte amount)
        {
            var result = value + amount;
            return result > 255 ? (byte)255 : (byte)result;
        }

        private static byte Subtract(byte value, byte amount)
        {
            return value < amount ? (byte)0 : (byte)(value - amount);
        }

    }
}
