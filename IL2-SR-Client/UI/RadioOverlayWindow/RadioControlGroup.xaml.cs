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
        private const double ReceiveIndicatorHoldMilliseconds = 500.0;
        private const double TransmitIndicatorHoldMilliseconds = 250.0;
        private static readonly Color ActiveGreen = (Color)ColorConverter.ConvertFromString("#96FF6D");
        private static readonly Color InactiveGrey = (Color)ColorConverter.ConvertFromString("#3A3A3A");
        private static readonly Color TxRed = (Color)ColorConverter.ConvertFromString("#FF3B30");
        private static readonly Brush TxActiveBrush = CreateFrozenStatusBrush(TxRed);
        private static readonly Brush RxActiveBrush = CreateFrozenStatusBrush(ActiveGreen);
        private static readonly Brush InactiveLedBrush = CreateFrozenStatusBrush(InactiveGrey);
        private static readonly Brush DisconnectedLedBrush = CreateFrozenStatusBrush(Colors.Red);
        private static readonly Brush SelectedChannelBorderBrush = CreateFrozenBrush("#00FF00");
        private static readonly Brush OccupiedChannelBorderBrush = new SolidColorBrush(Color.FromArgb(128, 255, 255, 0));
        private static readonly Brush RadioDisplayGreenBrush = CreateFrozenBrush("#00FF00");
        private static readonly Brush SpeakerDisplayBrush = CreateFrozenBrush("#FFFFFF");

        private bool _dragging;
        private bool _syncingSliderFromState;
        private readonly ConcurrentDictionary<int, Button> _channelButtons = new ConcurrentDictionary<int, Button>();
        private readonly ConcurrentDictionary<int, bool> _channelOccupancyStates = new ConcurrentDictionary<int, bool>();
        private readonly ConcurrentDictionary<int, int> _channelButtonVisualStates = new ConcurrentDictionary<int, int>();
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
        private bool? _lastTxActive;
        private bool? _lastRxActive;
        private bool? _lastDisconnected;
        private string _scrollMeasuredText = string.Empty;
        private double _scrollMeasuredViewportWidth = -1.0;
        private double _scrollTextWidth;
        private double _scrollTextHeight;
        private double _scrollingWidth;
        private double _scrollTravelDistance;
        private double _scrollTravelMilliseconds;

        public RadioControlGroup()
        {
            this.DataContext = this; // set data context

            InitializeComponent();
            CreateChannelButtons();
            LocalizationManager.LocalizeElement(this);
        }

        public void RefreshLocalization()
        {
            LocalizationManager.LocalizeElement(this);
            foreach (var button in _channelButtons)
            {
                button.Value.ToolTip = LocalizationManager.Format("Channel {0}", button.Key);
            }

            RepaintRadioStatus();
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
                    Content = channel.ToString(CultureInfo.InvariantCulture),
                    Tag = channel,
                    IsEnabled = true,
                    ToolTip = LocalizationManager.Format("Channel {0}", channel)
                };

                button.Style = (Style)FindResource("OverlayChannelButton");
                button.Click += Channel_Click;

                _channelButtons[channel] = button;
                _channelOccupancyStates[channel] = false;
                _channelButtonVisualStates[channel] = -1;
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
            RepaintRadioStatus(includeTelemetry: true);
        }

        internal void RepaintRadioLiveState()
        {
            RepaintRadioStatus(includeTelemetry: false);
        }

        internal void ApplyFastState(OverlayRadioFastState state)
        {
            if (!state.IsConnected || !state.IsAvailable)
            {
                var notConnectedText = LocalizationManager.Get("Not Connected");
                var alreadyRenderedDisconnected = _lastRenderedChannel == -1 &&
                                                  string.Equals(_currentDisplayText, notConnectedText, StringComparison.Ordinal) &&
                                                  !_speakerDisplayActive;

                UpdateStatusLeds(false, false, true);
                SetDisplayText(notConnectedText,
                    RadioDisplayGreenBrush,
                    1.0,
                    false);
                if (!alreadyRenderedDisconnected)
                {
                    ClearSpeakerDisplayState();
                    _lastRenderedChannel = -1;
                    UpdateChannelButtonState(-1);
                }

                _dragging = false;
                return;
            }

            if (_lastRenderedChannel != state.Channel)
            {
                _lastRenderedChannel = state.Channel;
                ClearSpeakerDisplayState();
            }

            UpdateStatusLeds(state.TxActive, state.RxActive, false);
            UpdateReceiveDisplay(state.RxActive, state.SpeakerName);

            if (!_speakerDisplayActive)
            {
                SetDisplayText(LocalizationManager.Format("CHN {0}", state.Channel),
                    RadioDisplayGreenBrush,
                    1.0,
                    false);
            }

            UpdateChannelButtonState(state.Channel);

            if (!_dragging && Math.Abs(RadioVolume.Value - state.VolumePercent) > 0.1)
            {
                _syncingSliderFromState = true;
                RadioVolume.Value = state.VolumePercent;
                _syncingSliderFromState = false;
            }
        }

        internal void RefreshOverlayTelemetry()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;
            if (IL2PlayerRadioInfo == null || !_clientStateSingleton.IsConnected)
            {
                UsersCount.Text = "0";
                UpdateChannelOccupancyIndicators(true);
                return;
            }

            var currentRadio = IL2PlayerRadioInfo.radios[RadioId];
            if (currentRadio == null)
            {
                return;
            }

            RefreshChannelOccupancyIndicatorsIfNeeded();
            int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);
            UsersCount.Text = count.ToString(CultureInfo.InvariantCulture);
        }

        private void RepaintRadioStatus(bool includeTelemetry)
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            if (IL2PlayerRadioInfo == null || !_clientStateSingleton.IsConnected)
            {
                UpdateStatusLeds(false, false, true);
                SetDisplayText(LocalizationManager.Get("Not Connected"),
                    RadioDisplayGreenBrush,
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
                var txActive = transmitting != null &&
                               transmitting.SendingOn == RadioId &&
                               (transmitting.IsSending || IsRecent(transmitting.LastSentAt, TransmitIndicatorHoldMilliseconds));
                var rxActive = receiving != null && IsRecent(receiving.LastReceivedAt, ReceiveIndicatorHoldMilliseconds);
                UpdateStatusLeds(txActive, rxActive, false);

                if (!_speakerDisplayActive)
                {
                    SetDisplayText(LocalizationManager.Format("CHN {0}", currentRadio.channel),
                        RadioDisplayGreenBrush,
                        1.0,
                        false);
                }
                UpdateChannelButtonState(currentRadio.channel);
                if (includeTelemetry)
                {
                    RefreshChannelOccupancyIndicatorsIfNeeded();
                    int count = _connectClientsSingleton.ClientsOnFreq(currentRadio.freq, currentRadio.modulation);
                    UsersCount.Text = count.ToString(CultureInfo.InvariantCulture);
                }

                if (_dragging == false)
                {
                    _syncingSliderFromState = true;
                    RadioVolume.Value = RadioHelper.GetEffectiveReceiveVolume(RadioId, currentRadio) * 100.0;
                    _syncingSliderFromState = false;
                }
            }
        }

        private void UpdateStatusLeds(bool txActive, bool rxActive, bool disconnected)
        {
            if (_lastTxActive == txActive && _lastRxActive == rxActive && _lastDisconnected == disconnected)
            {
                return;
            }

            _lastTxActive = txActive;
            _lastRxActive = rxActive;
            _lastDisconnected = disconnected;

            if (disconnected)
            {
                TxActive.Fill = DisconnectedLedBrush;
                RxActive.Fill = DisconnectedLedBrush;
                return;
            }

            TxActive.Fill = txActive ? TxActiveBrush : InactiveLedBrush;
            RxActive.Fill = rxActive ? RxActiveBrush : InactiveLedBrush;
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
            var hasVisualStateChange = false;
            foreach (var pair in _channelButtons)
            {
                var visualState = GetChannelVisualState(pair.Key, selectedChannel);
                if (!_channelButtonVisualStates.TryGetValue(pair.Key, out var previousVisualState) ||
                    previousVisualState != visualState)
                {
                    hasVisualStateChange = true;
                    break;
                }
            }

            if (!hasVisualStateChange)
            {
                return;
            }

            foreach (var pair in _channelButtons)
            {
                var button = pair.Value;
                var visualState = GetChannelVisualState(pair.Key, selectedChannel);

                if (_channelButtonVisualStates.TryGetValue(pair.Key, out var previousVisualState) &&
                    previousVisualState == visualState)
                {
                    continue;
                }

                _channelButtonVisualStates[pair.Key] = visualState;

                if (visualState == 2)
                {
                    button.SetResourceReference(ForegroundProperty, "OverlayChannelButtonSelectedForegroundBrush");
                    button.BorderBrush = SelectedChannelBorderBrush;
                    button.BorderThickness = new Thickness(1.5);
                }
                else if (visualState == 1)
                {
                    button.SetResourceReference(ForegroundProperty, "OverlayChannelButtonForegroundBrush");
                    button.BorderBrush = OccupiedChannelBorderBrush;
                    button.BorderThickness = new Thickness(1.5);
                }
                else
                {
                    button.SetResourceReference(ForegroundProperty, "OverlayChannelButtonForegroundBrush");
                    button.ClearValue(BorderBrushProperty);
                    button.ClearValue(BorderThicknessProperty);
                }
            }
        }

        private int GetChannelVisualState(int channel, int selectedChannel)
        {
            if (channel == selectedChannel)
            {
                return 2;
            }

            return _channelOccupancyStates.TryGetValue(channel, out var occupied) && occupied ? 1 : 0;
        }

        private static Brush CreateFrozenStatusBrush(Color color)
        {
            var brush = CreateStatusBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Brush CreateFrozenBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }

        internal void RepaintRadioReceive()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;
            if (IL2PlayerRadioInfo == null)
            {
                ClearSpeakerDisplayState();
                RadioFrequency.Foreground = RadioDisplayGreenBrush;
            }
            else
            {
                var receiveState = _clientStateSingleton.RadioReceivingState[RadioId];
                var receivingNow = receiveState != null && IsRecent(receiveState.LastReceivedAt, ReceiveIndicatorHoldMilliseconds);
                //check if current

                if (!receivingNow)
                {
                    _currentSpeakerActive = false;
                    UpdateLastSpeakerHold();
                }
                else if (receivingNow)
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

        private void UpdateReceiveDisplay(bool receivingNow, string speakerName)
        {
            if (!receivingNow)
            {
                _currentSpeakerActive = false;
                UpdateLastSpeakerHold();
                return;
            }

            _currentSpeakerActive = true;
            if (!string.IsNullOrWhiteSpace(speakerName))
            {
                _wasReceivingSpeaker = true;
                _speakerDisplayActive = true;
                _lastSpeakerName = speakerName;
                _lastSpeakerEndedAt = DateTime.MinValue;
                SetDisplayText(speakerName, SpeakerDisplayBrush, 1.0, true);
            }
            else
            {
                ClearHeldSpeakerDisplay();
            }
        }

        private static bool IsRecent(long timestampTicks, double holdMilliseconds)
        {
            if (timestampTicks <= 0)
            {
                return false;
            }

            return (DateTime.Now.Ticks - timestampTicks) < TimeSpan.FromMilliseconds(holdMilliseconds).Ticks;
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
                RadioFrequency.Foreground = RadioDisplayGreenBrush;
                StopSpeakerNameScroll();
                return;
            }

            var elapsed = (DateTime.UtcNow - _lastSpeakerEndedAt).TotalMilliseconds;
            if (elapsed >= LastSpeakerHoldMilliseconds)
            {
                ClearSpeakerDisplayState();
                RadioFrequency.Foreground = RadioDisplayGreenBrush;
                return;
            }

            _speakerDisplayActive = true;
            SetDisplayText(_lastSpeakerName, SpeakerDisplayBrush, 1.0, true);
        }

        private void SetDisplayText(string text, Brush foreground, double opacity, bool allowScroll)
        {
            text = text ?? string.Empty;
            var textChanged = !string.Equals(_currentDisplayText, text, StringComparison.Ordinal);
            if (textChanged)
            {
                _currentDisplayText = text;
                RadioFrequency.Text = text;
                _speakerNameScrollStartedAt = DateTime.UtcNow;
                InvalidateSpeakerNameScrollMetrics();
            }

            RadioFrequency.Foreground = foreground;
            RadioFrequency.Opacity = opacity;
            if (!allowScroll && !textChanged)
            {
                return;
            }

            UpdateSpeakerNameScroll(allowScroll);
        }

        private void UpdateSpeakerNameScroll(bool allowScroll)
        {
            if (!allowScroll)
            {
                StopSpeakerNameScroll();
                return;
            }

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

            EnsureSpeakerNameScrollMetrics(viewportWidth);
            Canvas.SetTop(RadioFrequency, Math.Max(0, (viewportHeight - _scrollTextHeight) / 2.0));

            if (_scrollTextWidth <= viewportWidth)
            {
                StopSpeakerNameScroll();
                return;
            }

            RadioFrequency.HorizontalAlignment = HorizontalAlignment.Left;
            RadioFrequency.TextAlignment = TextAlignment.Left;
            RadioFrequency.Width = _scrollingWidth;

            var cycleMilliseconds = SpeakerNameScrollPauseMilliseconds + _scrollTravelMilliseconds + SpeakerNameScrollPauseMilliseconds;
            var elapsed = (DateTime.UtcNow - _speakerNameScrollStartedAt).TotalMilliseconds % cycleMilliseconds;

            double offset;
            if (elapsed <= SpeakerNameScrollPauseMilliseconds)
            {
                offset = 0;
            }
            else if (elapsed <= SpeakerNameScrollPauseMilliseconds + _scrollTravelMilliseconds)
            {
                offset = -_scrollTravelDistance * ((elapsed - SpeakerNameScrollPauseMilliseconds) / _scrollTravelMilliseconds);
            }
            else
            {
                offset = -_scrollTravelDistance;
            }

            Canvas.SetLeft(RadioFrequency, offset);
        }

        private void EnsureSpeakerNameScrollMetrics(double viewportWidth)
        {
            if (string.Equals(_scrollMeasuredText, RadioFrequency.Text, StringComparison.Ordinal) &&
                Math.Abs(_scrollMeasuredViewportWidth - viewportWidth) < 0.1)
            {
                return;
            }

            _scrollMeasuredText = RadioFrequency.Text;
            _scrollMeasuredViewportWidth = viewportWidth;

            RadioFrequency.Width = double.NaN;
            RadioFrequency.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _scrollTextWidth = RadioFrequency.DesiredSize.Width;
            _scrollTextHeight = RadioFrequency.DesiredSize.Height;

            var scrollMetrics = SpeakerNameScrollLayout.Calculate(_scrollTextWidth, viewportWidth);
            _scrollingWidth = scrollMetrics.ScrollingWidth;
            _scrollTravelDistance = scrollMetrics.TravelDistance;
            _scrollTravelMilliseconds = _scrollTravelDistance / SpeakerNameScrollPixelsPerSecond * 1000.0;
        }

        private void InvalidateSpeakerNameScrollMetrics()
        {
            _scrollMeasuredText = string.Empty;
            _scrollMeasuredViewportWidth = -1.0;
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
            RadioFrequency.Foreground = RadioDisplayGreenBrush;
            RadioFrequency.Opacity = 1.0;
            StopSpeakerNameScroll();
        }

    }

    internal sealed class OverlayFastSnapshot
    {
        public OverlayRadioFastState Radio1 { get; set; }
        public OverlayRadioFastState Radio2 { get; set; }
        public OverlayRadioFastState Intercom { get; set; }
        public bool SecondRadioEnabled { get; set; }
    }

    internal struct OverlayRadioFastState
    {
        public int RadioId { get; set; }
        public bool IsConnected { get; set; }
        public bool IsAvailable { get; set; }
        public int Channel { get; set; }
        public bool TxActive { get; set; }
        public bool RxActive { get; set; }
        public string SpeakerName { get; set; }
        public double VolumePercent { get; set; }
    }
}
