using System;
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
        private const double LastSpeakerHoldMilliseconds = 3000.0;
        private const double SpeakerNameScrollPixelsPerSecond = 18.0;
        private const double SpeakerNameScrollPauseMilliseconds = 700.0;
        private static readonly Color ActiveGreen = (Color)ColorConverter.ConvertFromString("#96FF6D");

        private bool _dragging;
        private bool _syncingSliderFromState;
        private readonly ConcurrentDictionary<int, Button> _channelButtons = new ConcurrentDictionary<int, Button>();
        private readonly ConcurrentDictionary<int, bool> _channelOccupancyStates = new ConcurrentDictionary<int, bool>();
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;
        private string _currentDisplayText = string.Empty;
        private string _lastSpeakerName = string.Empty;
        private DateTime _lastSpeakerEndedAt = DateTime.MinValue;
        private DateTime _speakerNameScrollStartedAt = DateTime.MinValue;
        private bool _speakerDisplayActive;
        private bool _currentSpeakerActive;
        private bool _wasReceivingSpeaker;
        private int _lastRenderedChannel = -1;
        private DateTime _lastChannelOccupancyRefresh = DateTime.MinValue;

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
            _channelOccupancyStates.Clear();

            for (var channel = 1; channel <= 12; channel++)
            {
                var button = new Button
                {
                    Width = 15,
                    Height = 15,
                    Margin = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Right,
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

                button.Style = (Style)FindResource("OverlayChannelButton");
                button.Click += Channel_Click;

                _channelButtons[channel] = button;
                _channelOccupancyStates[channel] = false;
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

        private void ChannelUp_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.RadioChannelUp(RadioId);
        }

        private void ChannelDown_Click(object sender, RoutedEventArgs e)
        {
            RadioHelper.RadioChannelDown(RadioId);
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

            if (IL2PlayerRadioInfo == null || !_clientStateSingleton.IsConnected)
            {
                RadioActive.Fill = CreateStatusBrush(Colors.Red);
                SetDisplayText(LocalizationManager.Get("Not Connected"),
                    new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00")),
                    1.0,
                    false);
                ClearSpeakerDisplayState();
                _lastRenderedChannel = -1;
                UsersCount.Text = "0";
                UpdateChannelButtonState(-1);
                UpdateChannelOccupancyIndicators(true);


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

                if (_lastRenderedChannel != currentRadio.channel)
                {
                    _lastRenderedChannel = currentRadio.channel;
                    ClearSpeakerDisplayState();
                }

                var transmitting = _clientStateSingleton.RadioSendingState;
                var receiving = _clientStateSingleton.RadioReceivingState[RadioId];
                var speakingOnThisRadio = transmitting.IsSending && (transmitting.SendingOn == RadioId)
                                          || receiving != null && receiving.IsReceiving;
                if (RadioId == IL2PlayerRadioInfo.selected)
                {

                    if (speakingOnThisRadio)
                    {
                        RadioActive.Fill = CreateStatusBrush(ActiveGreen);
                    }
                    else
                    {
                        RadioActive.Fill = CreateStatusBrush(Colors.Green);
                    }
                }
                else
                {
                    RadioActive.Fill = CreateStatusBrush(speakingOnThisRadio ? ActiveGreen : Colors.Green);
                }

                if (!_speakerDisplayActive)
                {
                    SetDisplayText(LocalizationManager.Format("CHN {0}", currentRadio.channel),
                        new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00")),
                        1.0,
                        false);
                }
                UpdateChannelButtonState(currentRadio.channel);
                RefreshChannelOccupancyIndicatorsIfNeeded();

                int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);
                UsersCount.Text = count.ToString(CultureInfo.InvariantCulture);

                if (_dragging == false)
                {
                    _syncingSliderFromState = true;
                    RadioVolume.Value = RadioHelper.GetEffectiveReceiveVolume(RadioId, currentRadio) * 100.0;
                    _syncingSliderFromState = false;
                }
            }
        }

        private void RefreshChannelOccupancyIndicatorsIfNeeded()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastChannelOccupancyRefresh).TotalSeconds < 5)
            {
                return;
            }

            _lastChannelOccupancyRefresh = now;
            UpdateChannelOccupancyIndicators(false);
        }

        private void UpdateChannelOccupancyIndicators(bool clear)
        {
            foreach (var pair in _channelButtons)
            {
                _channelOccupancyStates[pair.Key] = !clear && pair.Key >= 3 && _connectClientsSingleton.IsChannelOccupied(pair.Key);
            }

            UpdateChannelButtonState(clear ? -1 : _lastRenderedChannel);
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
                else if (_channelOccupancyStates.TryGetValue(pair.Key, out var occupied) && occupied)
                {
                    button.Foreground = Brushes.White;
                    button.BorderBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 0));
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
                ClearSpeakerDisplayState();
                RadioFrequency.Foreground = new SolidColorBrush((Color) ColorConverter.ConvertFromString("#00FF00"));
            }
            else
            {
                var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
                //check if current

                if ((receiveState == null) || !receiveState.IsReceiving)
                {
                    _currentSpeakerActive = false;
                    UpdateLastSpeakerHold();
                }
                else if ((receiveState != null) && receiveState.IsReceiving)
                {
                    _currentSpeakerActive = true;
                    if (!string.IsNullOrWhiteSpace(receiveState.SentBy))
                    {
                        _wasReceivingSpeaker = true;
                        _speakerDisplayActive = true;
                        _lastSpeakerName = receiveState.SentBy;
                        _lastSpeakerEndedAt = DateTime.MinValue;
                        SetDisplayText(receiveState.SentBy, new SolidColorBrush(Colors.White), 1.0, true);
                    }
                    else
                    {
                        ClearHeldSpeakerDisplay();
                    }
                }
                else
                {
                    _currentSpeakerActive = false;
                    UpdateLastSpeakerHold();
                }
            }
        }

        private void UpdateLastSpeakerHold()
        {
            if (_currentSpeakerActive)
            {
                return;
            }

            if (_wasReceivingSpeaker)
            {
                _lastSpeakerEndedAt = DateTime.UtcNow;
                _wasReceivingSpeaker = false;
            }

            if (string.IsNullOrWhiteSpace(_lastSpeakerName) || _lastSpeakerEndedAt == DateTime.MinValue)
            {
                _speakerDisplayActive = false;
                RadioFrequency.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                StopSpeakerNameScroll();
                return;
            }

            var elapsed = (DateTime.UtcNow - _lastSpeakerEndedAt).TotalMilliseconds;
            if (elapsed >= LastSpeakerHoldMilliseconds)
            {
                ClearSpeakerDisplayState();
                RadioFrequency.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                return;
            }

            _speakerDisplayActive = true;
            SetDisplayText(_lastSpeakerName, new SolidColorBrush(Colors.White), 1.0, true);
        }

        private void SetDisplayText(string text, Brush foreground, double opacity, bool allowScroll)
        {
            text = text ?? string.Empty;
            if (!string.Equals(_currentDisplayText, text, StringComparison.Ordinal))
            {
                _currentDisplayText = text;
                RadioFrequency.Text = text;
                _speakerNameScrollStartedAt = DateTime.UtcNow;
            }

            RadioFrequency.Foreground = foreground;
            RadioFrequency.Opacity = opacity;
            UpdateSpeakerNameScroll(allowScroll);
        }

        private void UpdateSpeakerNameScroll(bool allowScroll)
        {
            if (!allowScroll)
            {
                StopSpeakerNameScroll();
                return;
            }

            RadioFrequency.Width = double.NaN;
            RadioFrequency.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = RadioFrequency.DesiredSize.Width;
            var textHeight = RadioFrequency.DesiredSize.Height;
            var viewportWidth = RadioFrequencyViewport.ActualWidth;
            var viewportHeight = RadioFrequencyViewport.ActualHeight;
            if (viewportWidth <= 0)
            {
                viewportWidth = 76;
            }

            if (viewportHeight <= 0)
            {
                viewportHeight = 12;
            }

            Canvas.SetTop(RadioFrequency, Math.Max(0, (viewportHeight - textHeight) / 2.0));

            if (textWidth <= viewportWidth)
            {
                StopSpeakerNameScroll();
                return;
            }

            RadioFrequency.HorizontalAlignment = HorizontalAlignment.Left;
            RadioFrequency.TextAlignment = TextAlignment.Left;
            var scrollMetrics = SpeakerNameScrollLayout.Calculate(textWidth, viewportWidth);
            var scrollingWidth = scrollMetrics.ScrollingWidth;
            RadioFrequency.Width = scrollingWidth;

            var travelDistance = scrollMetrics.TravelDistance;
            var travelMilliseconds = travelDistance / SpeakerNameScrollPixelsPerSecond * 1000.0;
            var cycleMilliseconds = SpeakerNameScrollPauseMilliseconds + travelMilliseconds + SpeakerNameScrollPauseMilliseconds;
            var elapsed = (DateTime.UtcNow - _speakerNameScrollStartedAt).TotalMilliseconds % cycleMilliseconds;

            double offset;
            if (elapsed <= SpeakerNameScrollPauseMilliseconds)
            {
                offset = 0;
            }
            else if (elapsed <= SpeakerNameScrollPauseMilliseconds + travelMilliseconds)
            {
                offset = -travelDistance * ((elapsed - SpeakerNameScrollPauseMilliseconds) / travelMilliseconds);
            }
            else
            {
                offset = -travelDistance;
            }

            Canvas.SetLeft(RadioFrequency, offset);
        }

        private void StopSpeakerNameScroll()
        {
            RadioFrequency.Width = double.NaN;
            RadioFrequency.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = RadioFrequency.DesiredSize.Width;
            var textHeight = RadioFrequency.DesiredSize.Height;
            var viewportWidth = RadioFrequencyViewport.ActualWidth;
            var viewportHeight = RadioFrequencyViewport.ActualHeight;
            if (viewportWidth <= 0)
            {
                viewportWidth = 76;
            }

            if (viewportHeight <= 0)
            {
                viewportHeight = 12;
            }

            RadioFrequency.HorizontalAlignment = HorizontalAlignment.Center;
            RadioFrequency.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(RadioFrequency, Math.Max(0, (viewportWidth - textWidth) / 2.0));
            Canvas.SetTop(RadioFrequency, Math.Max(0, (viewportHeight - textHeight) / 2.0));
        }

        private void ClearSpeakerDisplayState()
        {
            _speakerDisplayActive = false;
            _currentSpeakerActive = false;
            _wasReceivingSpeaker = false;
            _lastSpeakerName = string.Empty;
            _lastSpeakerEndedAt = DateTime.MinValue;
            RadioFrequency.Opacity = 1.0;
            StopSpeakerNameScroll();
        }

        private void ClearHeldSpeakerDisplay()
        {
            _speakerDisplayActive = false;
            _wasReceivingSpeaker = false;
            _lastSpeakerName = string.Empty;
            _lastSpeakerEndedAt = DateTime.MinValue;
            RadioFrequency.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
            RadioFrequency.Opacity = 1.0;
            StopSpeakerNameScroll();
        }

    }
}
