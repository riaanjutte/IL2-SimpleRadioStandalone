using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using MahApps.Metro.Controls;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public partial class PilotRosterWindow : MetroWindow
    {
        private const int ResizeHitTestThickness = 8;
        private const int WmNcHitTest = 0x0084;
        private const int HtRight = 11;
        private const int HtBottom = 15;
        private const int HtBottomRight = 17;
        private const double RosterRowHeight = 31.0;
        private const double RosterHeaderHeight = 30.0;
        private const double RosterWindowVerticalChrome = 30.0;
        private const double RosterMaximumScreenMargin = 80.0;
        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private readonly ObservableCollection<PilotRosterEntry> _pilotRoster = new ObservableCollection<PilotRosterEntry>();
        private readonly DispatcherTimer _updateTimer;
        private HwndSource _hwndSource;
        public bool IsUnavailableMode { get; }

        public PilotRosterWindow(bool showUnavailableMessage = false)
        {
            IsUnavailableMode = showUnavailableMessage;
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
            ApplyLocalizedText();
            RestoreWindowBounds();

            if (IsUnavailableMode)
            {
                ShowUnavailableMessage();
            }
            else
            {
                PilotList.ItemsSource = _pilotRoster;
                UpdateRoster();
            }

            LocationChanged += WindowBoundsChanged;
            SizeChanged += WindowBoundsChanged;
            SourceInitialized += PilotRosterWindowSourceInitialized;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _updateTimer.Tick += UpdateTimerTick;
            if (!IsUnavailableMode)
            {
                _updateTimer.Start();
            }
        }

        private void PilotRosterWindowSourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            _hwndSource?.AddHook(WndProc);
        }

        private void ApplyLocalizedText()
        {
            Title = "IL2-SRS Pilot Roster";
            CallsignColumn.Header = UppercaseLocalized("CALLSIGN");
            PilotColumn.Header = UppercaseLocalized("PILOT");
        }

        private static string UppercaseLocalized(string key)
        {
            return LocalizationManager.Get(key).ToUpperInvariant();
        }

        private void RestoreWindowBounds()
        {
            var configuredWidth = Math.Max(MinWidth, _globalSettings.GetPositionSetting(GlobalSettingsKeys.PilotRosterWidth).DoubleValue);
            var configuredHeight = Math.Max(MinHeight, _globalSettings.GetPositionSetting(GlobalSettingsKeys.PilotRosterHeight).DoubleValue);

            Width = configuredWidth;
            Height = configuredHeight;
            Left = _globalSettings.GetPositionSetting(GlobalSettingsKeys.PilotRosterX).DoubleValue;
            Top = _globalSettings.GetPositionSetting(GlobalSettingsKeys.PilotRosterY).DoubleValue;
        }

        private void WindowBoundsChanged(object sender, EventArgs e)
        {
            SaveWindowBounds();
        }

        private void SaveWindowBounds()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterX, Left);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterY, Top);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterWidth, Width);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.PilotRosterHeight, Height);
        }

        private void UpdateTimerTick(object sender, EventArgs e)
        {
            UpdateRoster();
        }

        private void ShowUnavailableMessage()
        {
            PilotList.Visibility = Visibility.Collapsed;
            UnavailableMessage.Text =
                "Pilot roster is currently only available when connected to Combat Box.\r\n\r\nConnect to srs.combatbox.net and open Pilot Roster again.";
            UnavailableMessage.Visibility = Visibility.Visible;

            Width = Math.Max(MinWidth, 500);
            Height = Math.Max(MinHeight, 190);
        }

        private void RosterFrame_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            DragMove();
        }

        private void UpdateRoster()
        {
            var entries = PilotRosterBuilder.Build(
                ClientStateSingleton.Instance.PlayerGameState,
                ConnectedClientsSingleton.Instance.Values);

            _pilotRoster.Clear();
            foreach (var entry in entries)
            {
                _pilotRoster.Add(entry);
            }

            Dispatcher.BeginInvoke(new Action(RefreshAutoColumnWidths), DispatcherPriority.Loaded);
            FitHeightToRoster(entries.Count);
        }

        private void RefreshAutoColumnWidths()
        {
            CallsignColumn.Width = DataGridLength.Auto;
            Radio1Column.Width = DataGridLength.Auto;
            Radio2Column.Width = DataGridLength.Auto;
            PilotColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            PilotList.UpdateLayout();
        }

        private void FitHeightToRoster(int playerCount)
        {
            var desiredHeight = RosterWindowVerticalChrome +
                                RosterHeaderHeight +
                                Math.Max(0, playerCount) * RosterRowHeight;
            var maxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - RosterMaximumScreenMargin);
            var fittedHeight = Math.Max(MinHeight, Math.Min(maxHeight, desiredHeight));

            if (Math.Abs(Height - fittedHeight) < 0.5)
            {
                return;
            }

            Height = fittedHeight;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _updateTimer?.Stop();
            _hwndSource?.RemoveHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmNcHitTest)
            {
                var resizeHitTest = GetResizeHitTest(lParam);
                if (resizeHitTest != IntPtr.Zero)
                {
                    handled = true;
                    return resizeHitTest;
                }
            }

            return IntPtr.Zero;
        }

        private IntPtr GetResizeHitTest(IntPtr lParam)
        {
            if (WindowState != WindowState.Normal || ResizeMode == ResizeMode.NoResize)
            {
                return IntPtr.Zero;
            }

            var mouseX = GetSignedLoWord(lParam);
            var mouseY = GetSignedHiWord(lParam);
            var topLeft = PointToScreen(new Point(0, 0));
            var source = PresentationSource.FromVisual(this);
            var scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
            var right = topLeft.X + ActualWidth * scaleX;
            var bottom = topLeft.Y + ActualHeight * scaleY;
            var resizeWidth = ResizeHitTestThickness * scaleX;
            var resizeHeight = ResizeHitTestThickness * scaleY;

            var insideHorizontal = mouseX >= topLeft.X && mouseX <= right + resizeWidth;
            var insideVertical = mouseY >= topLeft.Y && mouseY <= bottom + resizeHeight;
            var onRightEdge = mouseX >= right - resizeWidth && mouseX <= right + resizeWidth && insideVertical;
            var onBottomEdge = mouseY >= bottom - resizeHeight && mouseY <= bottom + resizeHeight && insideHorizontal;

            if (onRightEdge && onBottomEdge)
            {
                return new IntPtr(HtBottomRight);
            }

            if (onRightEdge)
            {
                return new IntPtr(HtRight);
            }

            if (onBottomEdge)
            {
                return new IntPtr(HtBottom);
            }

            return IntPtr.Zero;
        }

        private static int GetSignedLoWord(IntPtr value)
        {
            return (short)((long)value & 0xFFFF);
        }

        private static int GetSignedHiWord(IntPtr value)
        {
            return (short)(((long)value >> 16) & 0xFFFF);
        }

    }
}
