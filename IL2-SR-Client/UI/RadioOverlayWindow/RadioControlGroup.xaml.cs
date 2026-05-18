using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings.RadioChannels;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow
{
    /// <summary>
    ///     Interaction logic for RadioControlGroup.xaml
    /// </summary>
    public partial class RadioControlGroup : UserControl
    {
        private bool _dragging;
        private readonly ConcurrentDictionary<int, Button> _channelButtons = new ConcurrentDictionary<int, Button>();
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;

        public RadioControlGroup()
        {
            this.DataContext = this; // set data context

            InitializeComponent();
            CreateChannelButtons();
            LocalizationManager.LocalizeElement(this);
        }

        private int _radioId;

        public int RadioId
        { 
            get { return _radioId; }
            set
            {
                _radioId = value;
            }
        }

        private void RadioSelectSwitch(object sender, RoutedEventArgs e)
        {
            RadioHelper.SelectRadio(RadioId);
        }

        private void RadioFrequencyText_Click(object sender, MouseButtonEventArgs e)
        {
            RadioHelper.SelectRadio(RadioId);
        }

        private void CreateChannelButtons()
        {
            ChannelGrid.Children.Clear();
            _channelButtons.Clear();

            for (var channel = 1; channel <= 12; channel++)
            {
                var button = new Button
                {
                    Width = 15,
                    Height = 15,
                    Margin = new Thickness(0, 0, 2, 0),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Courier New"),
                    FontSize = channel >= 10 ? 8 : 10,
                    Foreground = Brushes.White,
                    Content = channel.ToString(CultureInfo.InvariantCulture),
                    Tag = channel,
                    IsEnabled = true,
                    ToolTip = LocalizationManager.Format("Channel {0}", channel)
                };

                button.Style = (Style)FindResource("DarkStyle-Button");
                button.Click += Channel_Click;

                _channelButtons[channel] = button;
                ChannelGrid.Children.Add(button);
            }
        }

        private void Channel_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is int channel)
            {
                RadioHelper.SelectRadioChannel(channel, RadioId);
            }
        }

        private void RadioVolume_DragStarted(object sender, RoutedEventArgs e)
        {
            _dragging = true;
        }


        private void RadioVolume_DragCompleted(object sender, RoutedEventArgs e)
        {
            var currentRadio = _clientStateSingleton.PlayerGameState.radios[RadioId];

            if (currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {
                var clientRadio = _clientStateSingleton.PlayerGameState.radios[RadioId];

                clientRadio.volume = (float) RadioVolume.Value / 100.0f;
            }

            _dragging = false;
        }


        internal void RepaintRadioStatus()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            if (IL2PlayerRadioInfo == null || !_clientStateSingleton.IsConnected)
            {
                RadioActive.Fill = new SolidColorBrush(Colors.Red);
                RadioFrequency.Text = LocalizationManager.Get("Not Connected");
                UpdateChannelButtonState(-1);


                //reset dragging just incase
                _dragging = false;
            }
            else
            {
                var currentRadio = IL2PlayerRadioInfo.radios[RadioId];

                if (currentRadio == null)
                {
                    return;
                }

                var transmitting = _clientStateSingleton.RadioSendingState;
                if (RadioId == IL2PlayerRadioInfo.selected)
                {

                    if (transmitting.IsSending && (transmitting.SendingOn == RadioId))
                    {
                        RadioActive.Fill = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#96FF6D"));
                    }
                    else
                    {
                        RadioActive.Fill = new SolidColorBrush(Colors.Green);
                    }
                }
                else
                {
                    RadioActive.Fill = new SolidColorBrush(Colors.Orange);
                }

                RadioFrequency.Text = LocalizationManager.Format("CHN {0}", currentRadio.channel);
                UpdateChannelButtonState(currentRadio.channel);

                int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);

                if (count > 0)
                {
                    RadioFrequency.Text += " - " + count;
                }

                if (_dragging == false)
                {
                    RadioVolume.Value = currentRadio.volume * 100.0;
                }
            }
        }

        private void UpdateChannelButtonState(int selectedChannel)
        {
            foreach (var pair in _channelButtons)
            {
                var button = pair.Value;
                if (pair.Key == selectedChannel)
                {
                    button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                    button.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                    button.BorderThickness = new Thickness(1.5);
                }
                else
                {
                    button.Foreground = Brushes.White;
                    button.ClearValue(BorderBrushProperty);
                    button.ClearValue(BorderThicknessProperty);
                }
            }
        }

        internal void RepaintRadioReceive()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;
            if (IL2PlayerRadioInfo == null)
            {
                RadioFrequency.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
            }
            else
            {
                var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
                //check if current

                if ((receiveState == null) || !receiveState.IsReceiving)
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
                else if ((receiveState != null) && receiveState.IsReceiving)
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color)Colors.White);

                    if (receiveState.SentBy.Length > 0)
                    {
                        RadioFrequency.Text = receiveState.SentBy;
                    }
                }
                else
                {
                    RadioFrequency.Foreground =
                        new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
                }
            }
        }

    }
}
