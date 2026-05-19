using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using NLog;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
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
        private const double DefaultOverlayWidth = 260.0;
        private const double DefaultOverlayHeight = 300.0;
        private const double OldDefaultOverlayWidth = 122.0;
        private const double OldDefaultOverlayHeight = 270.0;
        private bool _suppressSizeHandling = true;
    
        public RadioOverlayWindow()
        {
            //load opacity before the intialising as the slider changed
            //method fires after initialisation
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);

            this.WindowStartupLocation = WindowStartupLocation.Manual;

            _aspectRatio = MinWidth / MinHeight;

            _originalMinHeight = MinHeight;

            AllowsTransparency = true;
            Opacity = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioOpacity).DoubleValue;
            WindowOpacitySlider.Value = Opacity;

            //allows click and drag anywhere on the window
            ContainerPanel.MouseLeftButtonDown += WrapPanel_MouseLeftButtonDown;

            Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioX).DoubleValue;
            Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioY).DoubleValue;

            var configuredWidth = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioWidth).DoubleValue;
            var configuredHeight = _globalSettings.GetPositionSetting(GlobalSettingsKeys.RadioHeight).DoubleValue;
            var migratedOldDefaultSize = IsOldDefaultOverlaySize(configuredWidth, configuredHeight);
            Width = GetOverlayWidth(configuredWidth, migratedOldDefaultSize);
            Height = GetOverlayHeight(configuredHeight, migratedOldDefaultSize);

            if (migratedOldDefaultSize)
            {
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioWidth, Width);
                _globalSettings.SetPositionSetting(GlobalSettingsKeys.RadioHeight, Height);
            }

            Loaded += RadioOverlayWindow_Loaded;

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
            CalculateScale();
        }

        private void RadioRefresh(object sender, EventArgs eventArgs)
        {
            var IL2PlayerRadioInfo = _clientStateSingleton.PlayerGameState;

           
            Radio1.RepaintRadioStatus();
            Radio1.RepaintRadioReceive();

            if (IL2PlayerRadioInfo.radios[2].modulation != RadioInformation.Modulation.DISABLED)
            {
                if (Radio2.Visibility == Visibility.Collapsed)
                {
                    //show
                    Radio2.Visibility = Visibility.Visible;
                    Radio2Seperator.Visibility = Visibility.Visible;
                    MinHeight = _originalMinHeight + 70 + 25;
                    Recalculate();
                }

                Radio2.RepaintRadioStatus();
                Radio2.RepaintRadioReceive();
            }
            else
            {
                if (Radio2.Visibility != Visibility.Collapsed)
                {
                    Radio2.Visibility = Visibility.Collapsed;
                    MinHeight = _originalMinHeight;
                    Radio2Seperator.Visibility = Visibility.Collapsed;
                    Recalculate();
                }
            }

            Intercom.RepaintRadioStatus();

            FocusIL2();
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

            _updateTimer.Stop();
        }

        private void Button_Minimise(object sender, RoutedEventArgs e)
        {
            // Minimising a window without a taskbar icon leads to the window's menu bar still showing up in the bottom of screen
            // Since controls are unusable, but a very small portion of the always-on-top window still showing, we're closing it instead, similar to toggling the overlay
            if (_globalSettings.GetClientSettingBool(GlobalSettingsKeys.RadioOverlayTaskbarHide))
            {
                Close();
            }
            else
            {
                WindowState = WindowState.Minimized;
            }
        }


        private void Button_Close(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void windowOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            Opacity = e.NewValue;
        }

        private void containerPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_suppressSizeHandling)
            {
                return;
            }

            //force aspect ratio
            CalculateScale();

            WindowState = WindowState.Normal;
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

            try
            {
                _suppressSizeHandling = true;

                if (sizeInfo.WidthChanged && !sizeInfo.HeightChanged)
                {
                    Height = sizeInfo.NewSize.Width / _aspectRatio;
                }
                else if (sizeInfo.HeightChanged && !sizeInfo.WidthChanged)
                {
                    Width = sizeInfo.NewSize.Height * _aspectRatio;
                }
            }
            finally
            {
                _suppressSizeHandling = false;
            }

            // Console.WriteLine(this.Height +" width:"+ this.Width);
        }

        private bool IsOldDefaultOverlaySize(double configuredWidth, double configuredHeight)
        {
            return Math.Abs(configuredWidth - OldDefaultOverlayWidth) < 0.5 &&
                   Math.Abs(configuredHeight - OldDefaultOverlayHeight) < 0.5;
        }

        private double GetOverlayWidth(double configuredWidth, bool useDefaultSize)
        {
            if (useDefaultSize || double.IsNaN(configuredWidth) || configuredWidth <= 0)
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
            if (useDefaultSize || double.IsNaN(configuredHeight) || configuredHeight <= 0)
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
