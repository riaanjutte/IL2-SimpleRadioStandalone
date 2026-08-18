using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;
using System.Windows.Threading;
using Caliburn.Micro;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ClientAdmin
{
    public sealed class ClientAdminViewModel : Screen, IHandle<ServerStateMessage>
    {
        private readonly Dictionary<string, ClientViewModel> _clientsByKey =
            new Dictionary<string, ClientViewModel>(StringComparer.Ordinal);
        private readonly ObservableCollection<ClientViewModel> _clientRows =
            new ObservableCollection<ClientViewModel>();
        private readonly IEventAggregator _eventAggregator;
        private readonly DispatcherTimer _liveRefreshTimer;
        private int _liveRefreshPending;
        private string _searchText = string.Empty;
        private string _selectedCoalitionFilter = ClientAdminPresentation.AllCoalitions;

        public ClientAdminViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);

            DisplayName = "Client Administration";
            Clients = CollectionViewSource.GetDefaultView(_clientRows);
            Clients.Filter = ClientMatchesFilter;
            _liveRefreshTimer = new DispatcherTimer {Interval = TimeSpan.FromMilliseconds(200)};
            _liveRefreshTimer.Tick += LiveRefreshTimerOnTick;
            _liveRefreshTimer.Start();
            UpdateSummary();
        }

        public ICollectionView Clients { get; }
        public IEnumerable<string> CoalitionFilters => ClientAdminPresentation.CoalitionFilters;

        public string SearchText
        {
            get { return _searchText; }
            set
            {
                if (_searchText == value)
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                NotifyOfPropertyChange(() => SearchText);
                RefreshView();
            }
        }

        public string SelectedCoalitionFilter
        {
            get { return _selectedCoalitionFilter; }
            set
            {
                if (string.Equals(_selectedCoalitionFilter, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedCoalitionFilter = string.IsNullOrWhiteSpace(value)
                    ? ClientAdminPresentation.AllCoalitions
                    : value;
                NotifyOfPropertyChange(() => SelectedCoalitionFilter);
                RefreshView();
            }
        }

        public string ClientSummary { get; private set; }

        public void Handle(ServerStateMessage message)
        {
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var client in message.Clients.Where(client => client != null))
            {
                var key = GetClientKey(client);
                seenKeys.Add(key);

                ClientViewModel row;
                if (!_clientsByKey.TryGetValue(key, out row))
                {
                    row = new ClientViewModel(client, _eventAggregator);
                    row.PropertyChanged += ClientRowOnPropertyChanged;
                    _clientsByKey.Add(key, row);
                    _clientRows.Add(row);
                }
                else
                {
                    row.UpdateClient(client);
                }
            }

            foreach (var removedKey in _clientsByKey.Keys.Where(key => !seenKeys.Contains(key)).ToList())
            {
                var row = _clientsByKey[removedKey];
                _clientsByKey.Remove(removedKey);
                _clientRows.Remove(row);
                row.PropertyChanged -= ClientRowOnPropertyChanged;
                row.Dispose();
            }

            RefreshView();
        }

        private bool ClientMatchesFilter(object item)
        {
            var client = item as ClientViewModel;
            return client != null && client.Matches(SearchText, SelectedCoalitionFilter);
        }

        private void RefreshView()
        {
            Interlocked.Exchange(ref _liveRefreshPending, 0);
            Clients.Refresh();
            UpdateSummary();
        }

        private void ClientRowOnPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            Interlocked.Exchange(ref _liveRefreshPending, 1);
        }

        private void LiveRefreshTimerOnTick(object sender, EventArgs args)
        {
            if (Interlocked.Exchange(ref _liveRefreshPending, 0) == 1)
            {
                Clients.Refresh();
                UpdateSummary();
            }
        }

        protected override void OnDeactivate(bool close)
        {
            if (close)
            {
                _liveRefreshTimer.Stop();
                foreach (var row in _clientRows)
                {
                    row.PropertyChanged -= ClientRowOnPropertyChanged;
                    row.Dispose();
                }
            }

            base.OnDeactivate(close);
        }

        private void UpdateSummary()
        {
            var visible = Clients.Cast<object>().Count();
            ClientSummary = string.Format("{0} shown / {1} connected", visible, _clientRows.Count);
            NotifyOfPropertyChange(() => ClientSummary);
        }

        private static string GetClientKey(SRClient client)
        {
            return !string.IsNullOrWhiteSpace(client.ClientGuid)
                ? client.ClientGuid
                : client.Name + ":" + client.GetHashCode();
        }
    }
}
