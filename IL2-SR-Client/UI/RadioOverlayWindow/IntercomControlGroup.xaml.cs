using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for IntercomControlGroup.xaml
    /// </summary>
    public partial class IntercomControlGroup : UserControl
    {
        private const string IntercomLabelText = "CREW INTERCOM";
        private const int MaxIntercomSpeakerNameLength = 14;
        private const double ReceiveIndicatorHoldMilliseconds = 500.0;
        private const double TransmitIndicatorHoldMilliseconds = 250.0;
        private static readonly Color ActiveGreen = (Color)ColorConverter.ConvertFromString("#96FF6D");
        private static readonly Color ActiveAmber = (Color)ColorConverter.ConvertFromString("#FFB000");
        private static readonly Color TxRed = (Color)ColorConverter.ConvertFromString("#FF3B30");
        private static readonly Brush ActiveRadioBrush = CreateFrozenStatusBrush(ActiveAmber);
        private static readonly Brush ActiveRadioInactiveBrush = CreateFrozenStatusBrush(Fade(ActiveAmber, 0.2));
        private static readonly Brush TxActiveBrush = CreateFrozenStatusBrush(TxRed);
        private static readonly Brush TxInactiveBrush = CreateFrozenStatusBrush(Fade(TxRed, 0.2));
        private static readonly Brush RxActiveBrush = CreateFrozenStatusBrush(ActiveGreen);
        private static readonly Brush RxInactiveBrush = CreateFrozenStatusBrush(Fade(ActiveGreen, 0.2));
        private static readonly Brush DisconnectedLedBrush = CreateFrozenStatusBrush(Colors.Red);
        private bool _dragging;
        private bool _syncingSliderFromState;
        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;
        private readonly ConnectedClientsSingleton _connectClientsSingleton = ConnectedClientsSingleton.Instance;
        private bool? _lastTxActive;
        private bool? _lastRxActive;
        private bool? _lastSelectedActive;
        private bool? _lastDisconnected;
        private static string LocalizedIntercomLabelText => LocalizationManager.Get(IntercomLabelText);

        public IntercomControlGroup()
        {
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
        }

        public void RefreshLocalization()
        {
            LocalizationManager.LocalizeElement(this);
            RepaintRadioStatus();
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
                UpdateStatusLeds(false, false, true);
                RadioLabel.Content = LocalizedIntercomLabelText;
                RadioLabel.FontSize = 7;
                RadioLabel.ToolTip = null;
                TunedCount.Content = "";
                _dragging = false;
                return;
            }

            UpdateStatusLeds(state.TxActive, state.RxActive, false);
            UpdateIntercomLabel(state.RxActive, state.SpeakerName);

            if (state.RxActive)
            {
                RadioLabel.Foreground = Brushes.White;
                TunedCount.Foreground = RadioLabel.Foreground;
            }
            else
            {
                RadioLabel.Foreground = Brushes.Lime;
                TunedCount.Foreground = RadioLabel.Foreground;
            }

            if (!_dragging && System.Math.Abs(RadioVolume.Value - state.VolumePercent) > 0.1)
            {
                _syncingSliderFromState = true;
                RadioVolume.Value = state.VolumePercent;
                _syncingSliderFromState = false;
            }
        }

        internal void RefreshOverlayTelemetry()
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            if ((IL2PlayerRadioInfo == null) || !_clientStateSingleton.IsConnected)
            {
                TunedCount.Content = "";
                IntercomUsersCount.Text = "0";
                return;
            }

            var currentRadio = IL2PlayerRadioInfo.radios[RadioId];
            if (currentRadio == null)
            {
                return;
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
        }

        private void RepaintRadioStatus(bool includeTelemetry)
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            if ((IL2PlayerRadioInfo == null) || !_clientStateSingleton.IsConnected)
            {
                UpdateStatusLeds(false, false, true);
                RadioLabel.Content = LocalizedIntercomLabelText;
                RadioLabel.FontSize = 7;
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
                var txActive = transmitting != null &&
                               transmitting.SendingOn == RadioId &&
                               (transmitting.IsSending || IsRecent(transmitting.LastSentAt, TransmitIndicatorHoldMilliseconds));
                var rxActive = receiving != null && IsRecent(receiving.LastReceivedAt, ReceiveIndicatorHoldMilliseconds);
                UpdateIntercomLabel(receiving);
                UpdateStatusLeds(txActive, rxActive, false);

                if (rxActive)
                {
                    RadioLabel.Foreground = new SolidColorBrush(Colors.White);
                    TunedCount.Foreground = RadioLabel.Foreground;
                }
                else
                {
                    RadioLabel.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00FF00"));
                    TunedCount.Foreground = RadioLabel.Foreground;
                }

                if (includeTelemetry)
                {
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
                }

                if (_dragging == false)
                {
                    _syncingSliderFromState = true;
                    RadioVolume.Value = currentRadio.volume * 100.0;
                    _syncingSliderFromState = false;
                }
            }
        }

        private void UpdateStatusLeds(bool txActive, bool rxActive, bool disconnected)
        {
            var selectedActive = IsSelectedActive(disconnected);
            if (_lastTxActive == txActive &&
                _lastRxActive == rxActive &&
                _lastSelectedActive == selectedActive &&
                _lastDisconnected == disconnected)
            {
                return;
            }

            _lastTxActive = txActive;
            _lastRxActive = rxActive;
            _lastSelectedActive = selectedActive;
            _lastDisconnected = disconnected;

            if (disconnected)
            {
                RadioSelectedActive.Fill = DisconnectedLedBrush;
                TxActive.Fill = DisconnectedLedBrush;
                RxActive.Fill = DisconnectedLedBrush;
                return;
            }

            RadioSelectedActive.Fill = selectedActive ? ActiveRadioBrush : ActiveRadioInactiveBrush;
            TxActive.Fill = txActive ? TxActiveBrush : TxInactiveBrush;
            RxActive.Fill = rxActive ? RxActiveBrush : RxInactiveBrush;
        }

        private bool IsSelectedActive(bool disconnected)
        {
            return !disconnected &&
                   _clientStateSingleton.PlayerGameState != null &&
                   _clientStateSingleton.PlayerGameState.selected == RadioId;
        }

        private static Brush CreateFrozenStatusBrush(Color color)
        {
            var brush = CreateStatusBrush(color);
            brush.Freeze();
            return brush;
        }

        private void UpdateIntercomLabel(RadioReceivingState receiving)
        {
            if (receiving != null && IsRecent(receiving.LastReceivedAt, ReceiveIndicatorHoldMilliseconds) && !string.IsNullOrWhiteSpace(receiving.SentBy))
            {
                RadioLabel.Content = TrimSpeakerName(receiving.SentBy);
                RadioLabel.FontSize = 7;
                RadioLabel.ToolTip = receiving.SentBy;
                return;
            }

            RadioLabel.Content = LocalizedIntercomLabelText;
            RadioLabel.FontSize = 7;
            RadioLabel.ToolTip = null;
        }

        private void UpdateIntercomLabel(bool receivingNow, string speakerName)
        {
            if (receivingNow && !string.IsNullOrWhiteSpace(speakerName))
            {
                RadioLabel.Content = TrimSpeakerName(speakerName);
                RadioLabel.FontSize = 7;
                RadioLabel.ToolTip = speakerName;
                return;
            }

            RadioLabel.Content = LocalizedIntercomLabelText;
            RadioLabel.FontSize = 7;
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

        private static Color Fade(Color color, double opacity)
        {
            return Color.FromArgb((byte)Math.Round(byte.MaxValue * opacity),
                color.R,
                color.G,
                color.B);
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

        private static bool IsRecent(long timestampTicks, double holdMilliseconds)
        {
            if (timestampTicks <= 0)
            {
                return false;
            }

            return (System.DateTime.Now.Ticks - timestampTicks) < System.TimeSpan.FromMilliseconds(holdMilliseconds).Ticks;
        }

    }
}
