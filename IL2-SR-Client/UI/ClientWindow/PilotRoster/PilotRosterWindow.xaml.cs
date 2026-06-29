using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public partial class PilotRosterWindow : Window
    {
        private const int ResizeHitTestThickness = 8;
        private const int WmNcHitTest = 0x0084;
        private const int HtRight = 11;
        private const int HtBottom = 15;
        private const int HtBottomRight = 17;
        private const double RosterMaximumScreenMargin = 80.0;
        private const double RosterGridHeightSafetyPadding = 8.0;
        private const double DefaultRosterX = 360.0;
        private const double DefaultRosterY = 260.0;
        private const double DefaultRosterWidth = 560.0;
        private const double DefaultRosterHeight = 420.0;
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
            VehicleColumn.Header = UppercaseLocalized("VEHICLE");
        }

        public void RefreshLocalization()
        {
            LocalizationManager.LocalizeElement(this);
            ApplyLocalizedText();

            if (IsUnavailableMode)
            {
                ShowUnavailableMessage();
            }
        }

        private static string UppercaseLocalized(string key)
        {
            return LocalizationManager.Get(key).ToUpperInvariant();
        }

        private void RestoreWindowBounds()
        {
            var workArea = SystemParameters.WorkArea;
            var configuredWidth = Math.Max(MinWidth, _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterWidth, DefaultRosterWidth));
            var configuredHeight = Math.Max(MinHeight, _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterHeight, DefaultRosterHeight));

            Width = Math.Min(configuredWidth, Math.Max(MinWidth, workArea.Width));
            Height = Math.Min(configuredHeight, Math.Max(MinHeight, workArea.Height));
            Left = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterX, DefaultRosterX);
            Top = _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.PilotRosterY, DefaultRosterY);
            EnsureWindowIsOnScreen();
            SaveWindowBounds();
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
            ActiveSquadOpsText.Visibility = Visibility.Collapsed;
            PilotList.Visibility = Visibility.Collapsed;
            UnavailableMessage.Text =
                LocalizationManager.Get("Pilot roster is currently only available when connected to Combat Box.")
                + "\r\n\r\n"
                + LocalizationManager.Get("Connect to srs.combatbox.net and open Pilot Roster again.");
            UnavailableMessage.Visibility = Visibility.Visible;

            Width = Math.Max(MinWidth, 500);
            Height = Math.Max(MinHeight, 190);
            EnsureWindowIsOnScreen();
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
            var localState = ClientStateSingleton.Instance.PlayerGameState;
            var connectedClients = ConnectedClientsSingleton.Instance.Values;
            var entries = PilotRosterBuilder.Build(localState, connectedClients);
            var activeSquadOps = PilotRosterBuilder.BuildActiveSquadOpsSummary(localState, connectedClients);

            _pilotRoster.Clear();
            foreach (var entry in entries)
            {
                _pilotRoster.Add(entry);
            }

            ActiveSquadOpsText.Text = activeSquadOps;
            ActiveSquadOpsText.Visibility = string.IsNullOrWhiteSpace(activeSquadOps)
                ? Visibility.Collapsed
                : Visibility.Visible;

            Dispatcher.BeginInvoke(new Action(RefreshRosterLayout), DispatcherPriority.Loaded);
        }

        private void RefreshRosterLayout()
        {
            VehicleColumn.Visibility = _pilotRoster.Any(entry => entry.HasVehicle) ? Visibility.Visible : Visibility.Collapsed;
            RefreshAutoColumnWidths();
            FitHeightToRoster();
        }

        private void RefreshAutoColumnWidths()
        {
            CallsignColumn.Width = DataGridLength.Auto;
            VehicleColumn.Width = DataGridLength.Auto;
            Radio1Column.Width = DataGridLength.Auto;
            Radio2Column.Width = DataGridLength.Auto;
            PilotColumn.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
            PilotList.UpdateLayout();
        }

        private void FitHeightToRoster()
        {
            if (PilotList.Visibility != Visibility.Visible)
            {
                return;
            }

            PilotList.UpdateLayout();

            var currentGridHeight = PilotList.ActualHeight;
            if (currentGridHeight <= 0)
            {
                return;
            }

            var desiredGridHeight = PilotList.ColumnHeaderHeight +
                                    _pilotRoster.Count * PilotList.RowHeight +
                                    PilotList.BorderThickness.Top +
                                    PilotList.BorderThickness.Bottom +
                                    RosterGridHeightSafetyPadding;
            var rosterChromeHeight = Math.Max(0, ActualHeight - currentGridHeight);
            var desiredHeight = rosterChromeHeight + desiredGridHeight;
            var maxHeight = Math.Max(MinHeight, SystemParameters.WorkArea.Height - RosterMaximumScreenMargin);
            var fittedHeight = Math.Max(MinHeight, Math.Min(maxHeight, desiredHeight));
            var shouldScroll = desiredHeight > maxHeight;

            SetPilotListScrollMode(shouldScroll);

            if (Math.Abs(Height - fittedHeight) < 0.5)
            {
                PilotList.UpdateLayout();
                Dispatcher.BeginInvoke(new Action(() => SetPilotListScrollMode(shouldScroll)), DispatcherPriority.Render);
                return;
            }

            Height = fittedHeight;
            EnsureWindowIsOnScreen();
            PilotList.UpdateLayout();
            Dispatcher.BeginInvoke(new Action(() => SetPilotListScrollMode(shouldScroll)), DispatcherPriority.Render);
        }

        private void SetPilotListScrollMode(bool shouldScroll)
        {
            var visibility = shouldScroll ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            ScrollViewer.SetVerticalScrollBarVisibility(PilotList, visibility);

            var scrollViewer = FindVisualChild<ScrollViewer>(PilotList);
            if (scrollViewer == null)
            {
                return;
            }

            scrollViewer.VerticalScrollBarVisibility = visibility;
            if (!shouldScroll)
            {
                scrollViewer.ScrollToTop();
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null)
            {
                return null;
            }

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var index = 0; index < childCount; index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var descendant = FindVisualChild<T>(child);
                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private void EnsureWindowIsOnScreen()
        {
            var workArea = SystemParameters.WorkArea;
            const double margin = 10.0;

            if (double.IsNaN(Left) || double.IsInfinity(Left))
            {
                Left = workArea.Left + margin;
            }

            if (double.IsNaN(Top) || double.IsInfinity(Top))
            {
                Top = workArea.Top + margin;
            }

            if (Width > workArea.Width)
            {
                Width = Math.Max(MinWidth, workArea.Width - margin * 2);
            }

            if (Height > workArea.Height)
            {
                Height = Math.Max(MinHeight, workArea.Height - margin * 2);
            }

            if (Left < workArea.Left + margin)
            {
                Left = workArea.Left + margin;
            }
            else if (Left + Width > workArea.Right - margin)
            {
                Left = Math.Max(workArea.Left + margin, workArea.Right - Width - margin);
            }

            if (Top < workArea.Top + margin)
            {
                Top = workArea.Top + margin;
            }
            else if (Top + Height > workArea.Bottom - margin)
            {
                Top = Math.Max(workArea.Top + margin, workArea.Bottom - Height - margin);
            }
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
