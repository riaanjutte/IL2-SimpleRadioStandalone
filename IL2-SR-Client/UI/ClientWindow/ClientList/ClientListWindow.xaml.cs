using MahApps.Metro.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.ClientList
{
    /// <summary>
    /// Interaction logic for ClientListWindow.xaml
    /// </summary>
    public partial class ClientListWindow :  Window
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly GlobalSettingsStore _globalSettings = GlobalSettingsStore.Instance;
        private readonly DispatcherTimer _updateTimer;

        private readonly ObservableCollection<ClientListModel> _clientList = new ObservableCollection<ClientListModel>();

        public ClientListWindow()
        {
            InitializeComponent();
            LocalizationManager.LocalizeElement(this);
            RestoreWindowBounds();
            ClientList.ItemsSource = _clientList;
            UpdateList();

            LocationChanged += WindowBoundsChanged;
            SizeChanged += WindowBoundsChanged;

            _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private void UpdateList()
        {
            _clientList.Clear();

            //first create temporary list to sort
            var tempList = new List<ClientListModel>();


            foreach (var srClient in ConnectedClientsSingleton.Instance.Values)
            {
                var client = new ClientListModel()
                {
                    Name = srClient.Name,
                    Coalition = srClient.Coalition
                };

                if (srClient.GameState.radios.Length >= 3)
                {
                    client.Channel = srClient.GameState.radios[1].Channel + "";

                    if (srClient.GameState.radios[2] != null &&
                        srClient.GameState.radios[2].modulation == RadioInformation.Modulation.AM)
                    {
                        client.Channel += ("-" + srClient.GameState.radios[2].Channel);
                    }
                }
                else
                {
                    client.Channel = srClient.GameState.radios[1].Channel + "";
                }

                tempList.Add(client);
            }

            foreach (var clientListModel in tempList.OrderByDescending(model => model.Coalition)
                .ThenBy(model => model.Name.ToLower()).ToList())
            {
                _clientList.Add(clientListModel);
            }
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            UpdateList();
        }

        private void RestoreWindowBounds()
        {
            const double margin = 0.0;
            var bounds = new Rect(
                _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientListX, 360),
                _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientListY, 260),
                Math.Max(MinWidth, _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientListWidth, 300)),
                Math.Max(MinHeight, _globalSettings.GetFinitePositionSetting(GlobalSettingsKeys.ClientListHeight, 390)));
            var screens = System.Windows.Forms.Screen.AllScreens;
            var primaryWorkArea = System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea;
            var fallback = primaryWorkArea.HasValue
                ? new Rect(primaryWorkArea.Value.Left, primaryWorkArea.Value.Top,
                    primaryWorkArea.Value.Width, primaryWorkArea.Value.Height)
                : SystemParameters.WorkArea;
            var workArea = PilotRosterScreenBounds.SelectWorkArea(
                bounds,
                screens.Select(screen => new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top,
                    screen.WorkingArea.Width, screen.WorkingArea.Height)),
                fallback);
            var restored = PilotRosterScreenBounds.ConstrainToWorkArea(bounds, workArea, margin, MinWidth, MinHeight);

            Width = restored.Width;
            Height = restored.Height;
            Left = restored.Left;
            Top = restored.Top;
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

            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientListX, Left);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientListY, Top);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientListWidth, Width);
            _globalSettings.SetPositionSetting(GlobalSettingsKeys.ClientListHeight, Height);
        }

        private void ClientListWindow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            SaveWindowBounds();
            base.OnClosing(e);

            _updateTimer?.Stop();
        }


    }
}
