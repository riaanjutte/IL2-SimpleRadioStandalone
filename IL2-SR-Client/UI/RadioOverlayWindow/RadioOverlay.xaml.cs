using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using NLog;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Overlay
{
    /// <summary>
    ///     Interaction logic for RadioOverlayWindow.xaml
    /// </summary>
    public partial class RadioOverlayWindow : Window
    {
        private double _aspectRatio;
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();


        private readonly DispatcherTimer _updateTimer;

        private readonly ClientStateSingleton _clientStateSingleton = ClientStateSingleton.Instance;

        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;

        private readonly double _originalMinHeight;
        private HwndSource _hwndSource;
        private const double AssignedCallsignMinHeightDelta = 20.0;
        private const double Radio2MinHeightDelta = 90.0;
        private const double RciStatusMinHeightDelta = 20.0;
        private const double RciCallsignMinHeightDelta = 10.0;
        private const double AssignedCallsignScrollPixelsPerSecond = 18.0;
        private const double AssignedCallsignInitialScrollPauseMilliseconds = 700.0;
        private const double AssignedCallsignRepeatScrollPauseMilliseconds = 2000.0;
        private const double DefaultOverlayX = 300.0;
        private const double DefaultOverlayY = 300.0;
        private const double DefaultOverlayOpacity = 1.0;
        private const double DefaultOverlayWidth = 260.0;
        private const double DefaultOverlayHeight = 286.0;
        private const double PreviousFramedDefaultOverlayHeight = 320.0;
        private const double OldDefaultOverlayWidth = 122.0;
        private const double OldDefaultOverlayHeight = 270.0;
        private bool _suppressSizeHandling = true;
        private bool _rciIndicatorEnabled;
        private bool _overlayTestModeEnabled;
        private int _overlayTestClickCount;
        private int _overlayTestStateIndex;
        private DateTime _lastOverlayTestClickUtc = DateTime.MinValue;
        private DateTime _assignedCallsignScrollStartedAt = DateTime.MinValue;
        private string _currentAssignedCallsignText = string.Empty;
        private DispatcherTimer _overlayTestTimer;
    
        public RadioOverlayWindow()
        {
            //load opacity before the intialising as the slider changed
            //method fires after initialisation
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
            RciStatusLabel.Text = LocalizationManager.Get("RCI");

            this.WindowStartupLocation = WindowStartupLocation.Manual;

            _aspectRatio = MinWidth / MinHeight;

            _originalMinHeight = MinHeight;

            AllowsTransparency = true;
            Opacity = _globalSettings.GetBoundedPositionSetting(GlobalSettingsKeys.RadioOpacity, DefaultOverlayOpacity, 0.0, 1.0);
            WindowOpacitySlider.Value = Opacity;

            //allows click and drag anywhere on the window
            ContainerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;

            Left = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.RadioX, DefaultOverlayX);
            Top = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.RadioY, DefaultOverlayY);

            var configuredWidth = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.RadioWidth, DefaultOverlayWidth);
            var configuredHeight = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.RadioHeight, DefaultOverlayHeight);
            var useDefaultSize = ShouldUseDefaultOverlaySize(configuredWidth, configuredHeight);
            Width = GetOverlayWidth(configuredWidth, useDefaultSize);
            Height = GetOverlayHeight(configuredHeight, useDefaultSize);

            if (useDefaultSize)
            {
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, Width);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, Height);
            }

            Loaded += RadioOverlayWindow_Loaded;
            SourceInitialized += RadioOverlayWindow_SourceInitialized;

            LocationChanged += Location_Changed;

            RadioRefresh(null, null);

            //init radio refresh
            _updateTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(80)};
            _updateTimer.Tick += RadioRefresh;
            _updateTimer.Start();
        }

        private void Location_Changed(object sender, EventArgs e)
        {
        }

        private void RadioOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _suppressSizeHandling = false;
            UpdateOverlayMinimumHeight();
            CalculateScale();
        }

        private void RadioOverlayWindow_SourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource?.AddHook(WndProc);
        }

        private void RadioRefresh(object sender, EventArgs eventArgs)
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

            UpdateAssignedCallsignIndicator();
           
            Radio1.RepaintRadioStatus();
            Radio1.RepaintRadioReceive();

            if (IL2PlayerRadioInfo.radios[2].modulation != RadioInformation.Modulation.DISABLED)
            {
                if (Radio2.Visibility == Visibility.Collapsed)
                {
                    //show
                    Radio2.Visibility = Visibility.Visible;
                    UpdateOverlayMinimumHeight();
                }

                Radio2.RepaintRadioStatus();
                Radio2.RepaintRadioReceive();
            }
            else
            {
                if (Radio2.Visibility != Visibility.Collapsed)
                {
                    Radio2.Visibility = Visibility.Collapsed;
                    UpdateOverlayMinimumHeight();
                }
            }

            Intercom.RepaintRadioStatus();
            UpdateRciStatusIndicator();

            FocusIL2();
        }

        private void UpdateAssignedCallsignIndicator()
        {
            if (_overlayTestModeEnabled)
            {
                return;
            }

            var previousVisibility = AssignedCallsignPanel.Visibility;
            var assignedCallsign = ConnectedClientsSingleton.Instance.GetOwnAssignedCallsign();
            var hasAssignedCallsign = !string.IsNullOrWhiteSpace(assignedCallsign);
            var showRequestPrompt = !hasAssignedCallsign && _rciIndicatorEnabled;
            var friendlyRciCallsigns = showRequestPrompt && _clientStateSingleton.PlayerGameState != null
                ? ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(_clientStateSingleton.PlayerGameState.coalition)
                : string.Empty;
            var showActiveRcoRequestPrompt = !string.IsNullOrWhiteSpace(friendlyRciCallsigns);

            string displayText;
            Brush foreground;
            FontWeight fontWeight;

            if (hasAssignedCallsign)
            {
                displayText = RciDisplayState.FormatAssignedCallsign(assignedCallsign.Trim());
                foreground = Brushes.Lime;
                fontWeight = FontWeights.Normal;
            }
            else if (showActiveRcoRequestPrompt)
            {
                displayText = RciDisplayState.GetActiveRcoRequestCallsignText();
                foreground = Brushes.Red;
                fontWeight = FontWeights.Bold;
            }
            else if (showRequestPrompt)
            {
                displayText = RciDisplayState.GetRequestCallsignText();
                foreground = Brushes.Red;
                fontWeight = FontWeights.Bold;
            }
            else
            {
                displayText = string.Empty;
                foreground = Brushes.Lime;
                fontWeight = FontWeights.Normal;
            }

            AssignedCallsignLabel.Foreground = foreground;
            AssignedCallsignLabel.FontWeight = fontWeight;
            SetAssignedCallsignText(displayText);

            AssignedCallsignPanel.Visibility = string.IsNullOrWhiteSpace(AssignedCallsignLabel.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (previousVisibility != AssignedCallsignPanel.Visibility)
            {
                UpdateOverlayMinimumHeight();
            }
        }

        private void SetAssignedCallsignText(string text, bool allowScroll = true)
        {
            text = text ?? string.Empty;
            if (!string.Equals(_currentAssignedCallsignText, text, StringComparison.Ordinal))
            {
                _currentAssignedCallsignText = text;
                AssignedCallsignLabel.Text = text;
                _assignedCallsignScrollStartedAt = DateTime.UtcNow;
            }

            UpdateAssignedCallsignScroll(!string.IsNullOrWhiteSpace(text) && allowScroll);
        }

        private void UpdateAssignedCallsignScroll(bool allowScroll)
        {
            if (!allowScroll || string.IsNullOrWhiteSpace(AssignedCallsignLabel.Text))
            {
                StopAssignedCallsignScroll();
                return;
            }

            AssignedCallsignLabel.Width = double.NaN;
            AssignedCallsignLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = AssignedCallsignLabel.DesiredSize.Width;
            var textHeight = AssignedCallsignLabel.DesiredSize.Height;
            var viewportWidth = AssignedCallsignViewport.ActualWidth;
            var viewportHeight = AssignedCallsignViewport.ActualHeight;

            if (viewportWidth <= 0)
            {
                viewportWidth = AssignedCallsignPanel.Width - 2;
            }

            if (viewportHeight <= 0)
            {
                viewportHeight = AssignedCallsignPanel.Height - 2;
            }

            Canvas.SetTop(AssignedCallsignLabel, Math.Max(0, (viewportHeight - textHeight) / 2.0));

            if (textWidth <= viewportWidth)
            {
                StopAssignedCallsignScroll();
                return;
            }

            AssignedCallsignLabel.TextAlignment = TextAlignment.Left;
            var scrollMetrics = SpeakerNameScrollLayout.Calculate(textWidth, viewportWidth);
            AssignedCallsignLabel.Width = scrollMetrics.ScrollingWidth;

            var travelMilliseconds = scrollMetrics.TravelDistance / AssignedCallsignScrollPixelsPerSecond * 1000.0;
            var elapsed = (DateTime.UtcNow - _assignedCallsignScrollStartedAt).TotalMilliseconds;
            var offset = SpeakerNameScrollLayout.CalculateResetPauseScrollOffset(
                elapsed,
                scrollMetrics.TravelDistance,
                travelMilliseconds,
                AssignedCallsignInitialScrollPauseMilliseconds,
                AssignedCallsignRepeatScrollPauseMilliseconds);

            Canvas.SetLeft(AssignedCallsignLabel, offset);
        }

        private void StopAssignedCallsignScroll()
        {
            AssignedCallsignLabel.Width = double.NaN;
            AssignedCallsignLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var textWidth = AssignedCallsignLabel.DesiredSize.Width;
            var textHeight = AssignedCallsignLabel.DesiredSize.Height;
            var viewportWidth = AssignedCallsignViewport.ActualWidth;
            var viewportHeight = AssignedCallsignViewport.ActualHeight;

            if (viewportWidth <= 0)
            {
                viewportWidth = AssignedCallsignPanel.Width - 2;
            }

            if (viewportHeight <= 0)
            {
                viewportHeight = AssignedCallsignPanel.Height - 2;
            }

            AssignedCallsignLabel.TextAlignment = TextAlignment.Center;
            Canvas.SetLeft(AssignedCallsignLabel, Math.Max(0, (viewportWidth - textWidth) / 2.0));
            Canvas.SetTop(AssignedCallsignLabel, Math.Max(0, (viewportHeight - textHeight) / 2.0));
        }

        public void UpdateRciStatusIndicator()
        {
            if (_overlayTestModeEnabled)
            {
                return;
            }

            var previousRciVisibility = RciStatusPanel.Visibility;
            var previousCallsignVisibility = RciCallsignLabel.Visibility;

            RciStatusPanel.Visibility = _rciIndicatorEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (!_rciIndicatorEnabled)
            {
                UpdateRciCallsignLabel(string.Empty);
                UpdateOverlayMinimumHeightIfChanged(previousRciVisibility, previousCallsignVisibility);
                return;
            }

            var status = ConnectedClientsSingleton.Instance.GetRciStatus(_clientStateSingleton.PlayerGameState.coalition);
            var displayState = RciDisplayState.Create(
                status,
                ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(_clientStateSingleton.PlayerGameState.coalition));

            RciStatusIndicator.Background = Brushes.Transparent;
            RciStatusLabel.Text = displayState.StatusText;
            RciStatusLabel.Foreground = displayState.OverlayStatusForeground;
            UpdateRciCallsignLabel(displayState.RcoOnDutyText);
            UpdateOverlayMinimumHeightIfChanged(previousRciVisibility, previousCallsignVisibility);
        }

        public void SetRciIndicatorEnabled(bool enabled)
        {
            _rciIndicatorEnabled = enabled;
            UpdateAssignedCallsignIndicator();
            UpdateRciStatusIndicator();
        }

        private void UpdateRciCallsignLabel(string callsigns)
        {
            RciCallsignLabel.Text = callsigns;
            RciCallsignLabel.Visibility = string.IsNullOrWhiteSpace(RciCallsignLabel.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void RciStatusPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            var now = DateTime.UtcNow;
            _overlayTestClickCount = now - _lastOverlayTestClickUtc > TimeSpan.FromSeconds(2)
                ? 1
                : _overlayTestClickCount + 1;
            _lastOverlayTestClickUtc = now;

            if (_overlayTestClickCount < 5)
            {
                return;
            }

            _overlayTestClickCount = 0;
            ToggleOverlayTestMode();
        }

        private void ToggleOverlayTestMode()
        {
            if (_overlayTestModeEnabled)
            {
                StopOverlayTestMode();
                return;
            }

            StartOverlayTestMode();
        }

        private void StartOverlayTestMode()
        {
            _overlayTestModeEnabled = true;
            _overlayTestStateIndex = 0;
            ApplyOverlayTestState();

            if (_overlayTestTimer == null)
            {
                _overlayTestTimer = new DispatcherTimer {Interval = TimeSpan.FromSeconds(2)};
                _overlayTestTimer.Tick += OverlayTestTimer_Tick;
            }

            _overlayTestTimer.Start();
            Logger.Info("Radio overlay test mode started");
        }

        private void StopOverlayTestMode()
        {
            _overlayTestTimer?.Stop();
            _overlayTestModeEnabled = false;
            UpdateAssignedCallsignIndicator();
            UpdateRciStatusIndicator();
            Logger.Info("Radio overlay test mode stopped");
        }

        private void OverlayTestTimer_Tick(object sender, EventArgs e)
        {
            _overlayTestStateIndex++;
            ApplyOverlayTestState();
        }

        private void ApplyOverlayTestState()
        {
            var states = GetOverlayTestStates();
            var state = states[_overlayTestStateIndex % states.Length];

            SetAssignedCallsignText(state.AssignedCallsignText, state.ScrollAssignedCallsignText);
            AssignedCallsignLabel.Foreground = state.AssignedCallsignForeground;
            AssignedCallsignLabel.FontWeight = state.AssignedCallsignFontWeight;
            AssignedCallsignPanel.Visibility = string.IsNullOrWhiteSpace(state.AssignedCallsignText)
                ? Visibility.Collapsed
                : Visibility.Visible;

            RciStatusPanel.Visibility = Visibility.Visible;
            RciStatusIndicator.Background = Brushes.Transparent;
            RciStatusLabel.Text = state.RciStatusText;
            RciStatusLabel.Foreground = state.RciStatusForeground;
            UpdateRciCallsignLabel(state.RciCallsignText);
            UpdateOverlayMinimumHeight();
        }

        private OverlayTestState[] GetOverlayTestStates()
        {
            return new[]
            {
                new OverlayTestState(
                    RciDisplayState.GetActiveRcoRequestCallsignText(),
                    Brushes.Red,
                    FontWeights.Bold,
                    true,
                    RciDisplayState.Create(RciStatus.None, string.Empty)),
                new OverlayTestState(
                    RciDisplayState.FormatAssignedCallsign("CHECKMATE"),
                    Brushes.Lime,
                    FontWeights.Normal,
                    false,
                    RciDisplayState.Create(RciStatus.FriendlyOnly, "DEFCON")),
                new OverlayTestState(
                    RciDisplayState.FormatAssignedCallsign("CHECKMATE"),
                    Brushes.Lime,
                    FontWeights.Normal,
                    false,
                    RciDisplayState.Create(RciStatus.Both, "DEFCON")),
                new OverlayTestState(
                    RciDisplayState.FormatAssignedCallsign("CHECKMATE"),
                    Brushes.Lime,
                    FontWeights.Normal,
                    false,
                    RciDisplayState.Create(RciStatus.EnemyOnly, string.Empty)),
                new OverlayTestState(
                    RciDisplayState.FormatAssignedCallsign("CHECKMATE"),
                    Brushes.Lime,
                    FontWeights.Normal,
                    false,
                    RciDisplayState.Create(RciStatus.Neutral, "RCI TEST"))
            };
        }

        private void UpdateOverlayMinimumHeightIfChanged(Visibility previousRciVisibility, Visibility previousCallsignVisibility)
        {
            if (previousRciVisibility != RciStatusPanel.Visibility ||
                previousCallsignVisibility != RciCallsignLabel.Visibility)
            {
                UpdateOverlayMinimumHeight();
            }
        }

        private void UpdateOverlayMinimumHeight()
        {
            // Measure the real stacked-plate height so MinHeight matches the content
            // exactly. The window is locked to a MinWidth/MinHeight aspect ratio during
            // resize, so any over-estimate here letterboxes empty chassis below the last
            // plate (and that margin grows as the window is dragged larger).
            SectionsPanel.UpdateLayout();
            var contentHeight = SectionsPanel.ActualHeight;
            if (contentHeight < 10)
            {
                return; // not laid out yet; keep current minimum
            }

            if (Math.Abs(MinHeight - contentHeight) < 0.5)
            {
                return;
            }

            MinHeight = contentHeight;
            Recalculate();
        }

        private void Recalculate()
        {
            _aspectRatio = MinWidth / MinHeight;
            if (!_suppressSizeHandling)
            {
                Height = Width / _aspectRatio;
                CalculateScale();
            }
        }

        private long _lastFocus;

        private void FocusIL2()
        {
            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RefocusIL2))
            {
                var overlayWindow = new WindowInteropHelper(this).Handle;

                //focus IL2 if needed
                var foreGround = WindowHelper.GetForegroundWindow();

                Process[] localByName = Process.GetProcessesByName("Il-2");

                if (localByName != null && localByName.Length > 0)
                {
                    //either IL2 is in focus OR Overlay window is not in focus
                    if (foreGround == localByName[0].MainWindowHandle || overlayWindow != foreGround ||
                        this.IsMouseOver)
                    {
                        _lastFocus = DateTime.Now.Ticks;
                    }
                    else if (DateTime.Now.Ticks > _lastFocus + 20000000 && overlayWindow == foreGround)
                    {
                        WindowHelper.BringProcessToFront(localByName[0]);
                    }
                }
            }
        }

        private void WrapPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, bounds.Width);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, bounds.Height);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioOpacity,Opacity);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioX, bounds.Left);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioY, bounds.Top);
            base.OnClosing(e);

            _hwndSource?.RemoveHook(WndProc);
            _updateTimer.Stop();
            _overlayTestTimer?.Stop();
        }

        private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = e.NewValue;
        }

        private void CalculateScale()
        {
            var yScale = ActualHeight / RadioOverlayWin.MinHeight;
            var xScale = ActualWidth / RadioOverlayWin.MinWidth;
            var value = Math.Min(xScale, yScale);
            ScaleValue = (double) OnCoerceScaleValue(RadioOverlayWin, value);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);

            if (_suppressSizeHandling || !IsLoaded)
            {
                return;
            }

            if (sizeInfo.WidthChanged || sizeInfo.HeightChanged)
            {
                CalculateScale();
            }

            // Console.WriteLine(this.Height +" width:"+ this.Width);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int wmSizing = 0x0214;

            if (msg != wmSizing || lParam == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            var rect = (NativeRect)Marshal.PtrToStructure(lParam, typeof(NativeRect));
            EnforceResizeAspectRatio((ResizeEdge)wParam.ToInt32(), ref rect);
            Marshal.StructureToPtr(rect, lParam, true);
            handled = true;

            return IntPtr.Zero;
        }

        private void EnforceResizeAspectRatio(ResizeEdge edge, ref NativeRect rect)
        {
            var width = Math.Max(rect.Right - rect.Left, GetDeviceWidth(MinWidth));
            var height = Math.Max(rect.Bottom - rect.Top, GetDeviceHeight(MinHeight));
            var aspectRatio = _aspectRatio <= 0 ? MinWidth / MinHeight : _aspectRatio;

            switch (edge)
            {
                case ResizeEdge.Left:
                    rect.Left = rect.Right - width;
                    rect.Bottom = rect.Top + (int)Math.Round(width / aspectRatio);
                    break;
                case ResizeEdge.Right:
                    rect.Right = rect.Left + width;
                    rect.Bottom = rect.Top + (int)Math.Round(width / aspectRatio);
                    break;
                case ResizeEdge.Top:
                    rect.Top = rect.Bottom - height;
                    rect.Right = rect.Left + (int)Math.Round(height * aspectRatio);
                    break;
                case ResizeEdge.Bottom:
                    rect.Bottom = rect.Top + height;
                    rect.Right = rect.Left + (int)Math.Round(height * aspectRatio);
                    break;
                case ResizeEdge.TopLeft:
                    rect.Left = rect.Right - width;
                    rect.Top = rect.Bottom - (int)Math.Round(width / aspectRatio);
                    break;
                case ResizeEdge.TopRight:
                    rect.Right = rect.Left + width;
                    rect.Top = rect.Bottom - (int)Math.Round(width / aspectRatio);
                    break;
                case ResizeEdge.BottomLeft:
                    rect.Left = rect.Right - width;
                    rect.Bottom = rect.Top + (int)Math.Round(width / aspectRatio);
                    break;
                case ResizeEdge.BottomRight:
                    rect.Right = rect.Left + width;
                    rect.Bottom = rect.Top + (int)Math.Round(width / aspectRatio);
                    break;
            }
        }

        private int GetDeviceWidth(double width)
        {
            var source = PresentationSource.FromVisual(this);
            return (int)Math.Ceiling(width * (source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0));
        }

        private int GetDeviceHeight(double height)
        {
            var source = PresentationSource.FromVisual(this);
            return (int)Math.Ceiling(height * (source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0));
        }

        private enum ResizeEdge
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private class OverlayTestState
        {
            public OverlayTestState(
                string assignedCallsignText,
                Brush assignedCallsignForeground,
                FontWeight assignedCallsignFontWeight,
                bool scrollAssignedCallsignText,
                RciDisplayState rciDisplayState)
            {
                AssignedCallsignText = assignedCallsignText;
                AssignedCallsignForeground = assignedCallsignForeground;
                AssignedCallsignFontWeight = assignedCallsignFontWeight;
                ScrollAssignedCallsignText = scrollAssignedCallsignText;
                RciStatusText = rciDisplayState.StatusText;
                RciStatusForeground = rciDisplayState.OverlayStatusForeground;
                RciCallsignText = rciDisplayState.RcoOnDutyText;
            }

            public string AssignedCallsignText { get; }
            public Brush AssignedCallsignForeground { get; }
            public FontWeight AssignedCallsignFontWeight { get; }
            public bool ScrollAssignedCallsignText { get; }
            public string RciStatusText { get; }
            public Brush RciStatusForeground { get; }
            public string RciCallsignText { get; }
        }

        private bool ShouldUseDefaultOverlaySize(double configuredWidth, double configuredHeight)
        {
            if (double.IsNaN(configuredWidth) || double.IsInfinity(configuredWidth) ||
                double.IsNaN(configuredHeight) || double.IsInfinity(configuredHeight))
            {
                return true;
            }

            return (Math.Abs(configuredWidth - OldDefaultOverlayWidth) < 0.5 &&
                    Math.Abs(configuredHeight - OldDefaultOverlayHeight) < 0.5) ||
                   (Math.Abs(configuredWidth - DefaultOverlayWidth) < 0.5 &&
                    Math.Abs(configuredHeight - PreviousFramedDefaultOverlayHeight) < 0.5);
        }

        private double GetOverlayWidth(double configuredWidth, bool useDefaultSize)
        {
            if (useDefaultSize || double.IsNaN(configuredWidth) || double.IsInfinity(configuredWidth) || configuredWidth <= 0)
            {
                return DefaultOverlayWidth;
            }

            if (configuredWidth < MinWidth)
            {
                return MinWidth;
            }

            return configuredWidth;
        }

        private double GetOverlayHeight(double configuredHeight, bool useDefaultSize)
        {
            if (useDefaultSize || double.IsNaN(configuredHeight) || double.IsInfinity(configuredHeight) || configuredHeight <= 0)
            {
                return DefaultOverlayHeight;
            }

            if (configuredHeight < MinHeight)
            {
                return MinHeight;
            }

            return configuredHeight;
        }

        #region ScaleValue Depdency Property //StackOverflow: http://stackoverflow.com/questions/3193339/tips-on-developing-resolution-independent-application/5000120#5000120

        public static readonly DependencyProperty ScaleValueProperty = DependencyProperty.Register("ScaleValue",
            typeof(double), typeof(RadioOverlayWindow),
            new UIPropertyMetadata(1.0, OnScaleValueChanged,
                OnCoerceScaleValue));


        private static object OnCoerceScaleValue(DependencyObject o, object value)
        {
            var mainWindow = o as RadioOverlayWindow;
            if (mainWindow != null)
                return mainWindow.OnCoerceScaleValue((double) value);
            return value;
        }

        private static void OnScaleValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
        {
            var mainWindow = o as RadioOverlayWindow;
            if (mainWindow != null)
                mainWindow.OnScaleValueChanged((double) e.OldValue, (double) e.NewValue);
        }

        protected virtual double OnCoerceScaleValue(double value)
        {
            if (double.IsNaN(value))
                return 1.0f;

            value = Math.Max(0.1, value);
            return value;
        }

        protected virtual void OnScaleValueChanged(double oldValue, double newValue)
        {
        }

        public double ScaleValue
        {
            get { return (double) GetValue(ScaleValueProperty); }
            set { SetValue(ScaleValueProperty, value); }
        }

        #endregion

        private void RadioOverlayWindow_OnLocationChanged(object sender, EventArgs e)
        {
            //reset last focus so we dont switch back to IL2 while dragging
            _lastFocus = DateTime.Now.Ticks;
        }
    }
}
