using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ClientAdmin
{
    public class ClientViewModel : Screen, IDisposable
    {
        private static readonly SolidColorBrush SpectatorBrush = CreateBrush(Colors.Gray);
        private static readonly SolidColorBrush RedBrush = CreateBrush(Colors.Red);
        private static readonly SolidColorBrush BlueBrush = CreateBrush(Colors.DodgerBlue);
        private readonly IEventAggregator _eventAggregator;
        private SRClient _client;

        public ClientViewModel(SRClient client, IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            UpdateClient(client);
        }

        public SRClient Client => _client;
        public string ClientKey => !string.IsNullOrWhiteSpace(Client.ClientGuid)
            ? Client.ClientGuid
            : Client.Name + ":" + Client.GetHashCode();
        public string ClientName => Client.Name;
        public string AssignedCallsign => EmptyFallback(Client.AssignedCallsign);
        public string CoalitionText => ClientAdminPresentation.GetCoalitionName(Client.Coalition);
        public string Radio1Channel => ClientAdminPresentation.FormatRadioChannel(Client.GameState, 1);
        public string Radio2Channel => ClientAdminPresentation.FormatRadioChannel(Client.GameState, 2);
        public int Radio1SortChannel => ClientAdminPresentation.GetRadioChannel(Client.GameState, 1);
        public int Radio2SortChannel => ClientAdminPresentation.GetRadioChannel(Client.GameState, 2);
        public string VoiceLinkStatus => Client.VoipPort == null ? "WAITING" : "READY";
        public bool HasVoiceLink => Client.VoipPort != null;
        public bool ClientMuted => Client.Muted;

        public SolidColorBrush ClientCoalitionColour
        {
            get
            {
                switch (Client.Coalition)
                {
                    case 1:
                        return RedBrush;
                    case 2:
                        return BlueBrush;
                    default:
                        return SpectatorBrush;
                }
            }
        }

        public bool Matches(string searchText, string coalitionFilter)
        {
            return ClientAdminPresentation.Matches(Client, searchText, coalitionFilter);
        }

        public void UpdateClient(SRClient client)
        {
            if (client == null || ReferenceEquals(_client, client))
            {
                RefreshFromClient();
                return;
            }

            if (_client != null)
            {
                _client.PropertyChanged -= ClientOnPropertyChanged;
            }

            _client = client;
            _client.PropertyChanged += ClientOnPropertyChanged;
            RefreshFromClient();
        }

        public void RefreshFromClient()
        {
            if (Client == null)
            {
                return;
            }

            NotifyOfPropertyChange(() => ClientName);
            NotifyOfPropertyChange(() => AssignedCallsign);
            NotifyOfPropertyChange(() => CoalitionText);
            NotifyOfPropertyChange(() => ClientCoalitionColour);
            NotifyOfPropertyChange(() => Radio1Channel);
            NotifyOfPropertyChange(() => Radio2Channel);
            NotifyOfPropertyChange(() => Radio1SortChannel);
            NotifyOfPropertyChange(() => Radio2SortChannel);
            NotifyOfPropertyChange(() => VoiceLinkStatus);
            NotifyOfPropertyChange(() => HasVoiceLink);
            NotifyOfPropertyChange(() => ClientMuted);
        }

        public void KickClient()
        {
            var result = MessageBox.Show("Disconnect " + Client.Name + " from the server?", "Kick Client",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _eventAggregator.PublishOnBackgroundThread(new KickClientMessage(Client));
            }
        }

        public void BanClient()
        {
            var result = MessageBox.Show("Ban " + Client.Name + " and disconnect them from the server?", "Ban Client",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _eventAggregator.PublishOnBackgroundThread(new BanClientMessage(Client));
            }
        }

        public void ToggleClientMute()
        {
            Client.Muted = !Client.Muted;
            NotifyOfPropertyChange(() => ClientMuted);
        }

        public void Dispose()
        {
            if (_client != null)
            {
                _client.PropertyChanged -= ClientOnPropertyChanged;
            }
        }

        private void ClientOnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            RefreshFromClient();
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value) || value == "---" ? "--" : value;
        }

        private static SolidColorBrush CreateBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }
    }
}
