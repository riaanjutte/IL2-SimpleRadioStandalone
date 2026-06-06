using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons
{
    public sealed class ConnectedClientsSingleton : INotifyPropertyChanged
    {
        private static readonly Regex RciNameSuffixPattern = new Regex(@"_+RCI_*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex RciNamePrefixPattern = new Regex(@"^_*RCI_+", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();
        private static volatile ConnectedClientsSingleton _instance;
        private static object _lock = new Object();
        private readonly string _guid = ClientStateSingleton.Instance.ShortGUID;
        private readonly SyncedServerSettings _serverSettings = SyncedServerSettings.Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        private ConnectedClientsSingleton() { }

        public static ConnectedClientsSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new ConnectedClientsSingleton();
                    }
                }

                return _instance;
            }
        }

        private void NotifyPropertyChanged(string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyAll()
        {
            NotifyPropertyChanged("Total");
        }

        public SRClient this[string key]
        {
            get
            {
                return _clients[key];
            }
            set
            {
                _clients[key] = value;
               NotifyAll();
            }
        }

        public ICollection<SRClient> Values
        {
            get
            {
                return _clients.Values;
            }
        }

        public int Total
        {
            get
            {
                return _clients.Count();
            }
        }

        public string GetOwnAssignedCallsign()
        {
            return _clients.TryGetValue(_guid, out var client) && client != null
                ? client.AssignedCallsign ?? string.Empty
                : string.Empty;
        }

        public RciStatus GetRciStatus(int ownCoalition)
        {
            var friendlyRci = false;
            var enemyRci = false;
            var anyRci = false;

            foreach (var client in _clients)
            {
                var srClient = client.Value;
                if (srClient == null || !TryGetRciCallsign(srClient.Name, out var callsign))
                {
                    continue;
                }

                anyRci = true;

                if (ownCoalition > 0 && srClient.Coalition == ownCoalition)
                {
                    friendlyRci = true;
                }
                else if (ownCoalition > 0 && srClient.Coalition > 0)
                {
                    enemyRci = true;
                }
            }

            if (friendlyRci && enemyRci)
            {
                return RciStatus.Both;
            }

            if (friendlyRci)
            {
                return RciStatus.FriendlyOnly;
            }

            if (enemyRci)
            {
                return RciStatus.EnemyOnly;
            }

            return anyRci ? RciStatus.Neutral : RciStatus.None;
        }

        public string GetFriendlyRciCallsign(int ownCoalition)
        {
            if (ownCoalition <= 0)
            {
                return string.Empty;
            }

            var friendlyRciCallsigns = _clients.Values
                .Where(client => client != null &&
                                 client.ClientGuid != _guid &&
                                 client.Coalition == ownCoalition &&
                                 TryGetRciCallsign(client.Name, out var callsign) &&
                                 !string.IsNullOrWhiteSpace(callsign))
                .Select(client =>
                {
                    TryGetRciCallsign(client.Name, out var callsign);
                    return callsign;
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return friendlyRciCallsigns.Count == 0 ? string.Empty : string.Join(", ", friendlyRciCallsigns);
        }

        public string GetRciDebugSummary(int ownCoalition)
        {
            var candidates = _clients.Values
                .Where(client => client != null &&
                                 TryGetRciCallsign(client.Name, out var callsign))
                .Select(client => $"{client.Name} c{client.Coalition} age {GetAgeSeconds(client.LastUpdate):0}s")
                .ToList();
            var allClients = _clients.Values
                .Where(client => client != null && client.ClientGuid != _guid)
                .OrderBy(client => client.Name)
                .Take(30)
                .Select(client => $"{client.Name} c{client.Coalition}")
                .ToList();

            return $"Own coalition: {ownCoalition}\nKnown RCI clients: {(candidates.Count == 0 ? "none" : string.Join(", ", candidates))}\nConnected clients: {(allClients.Count == 0 ? "none" : string.Join(", ", allClients))}";
        }

        private static double GetAgeSeconds(long ticks)
        {
            return ticks <= 0 ? -1 : new TimeSpan(DateTime.Now.Ticks - ticks).TotalSeconds;
        }

        private static bool TryGetRciCallsign(string name, out string callsign)
        {
            callsign = string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmedName = name.Trim();

            if (RciNameSuffixPattern.IsMatch(trimmedName))
            {
                callsign = RciNameSuffixPattern.Replace(trimmedName, string.Empty).Trim().TrimEnd('_').Trim();
                return true;
            }

            if (RciNamePrefixPattern.IsMatch(trimmedName))
            {
                callsign = RciNamePrefixPattern.Replace(trimmedName, string.Empty).Trim().TrimStart('_').Trim();
                return true;
            }

            return false;
        }

        public bool TryRemove(string key, out SRClient value)
        {
            bool result = _clients.TryRemove(key, out value);
            if (result)
            {
                NotifyPropertyChanged("Total");
            }
            return result;
        }

        public void Clear()
        {
            _clients.Clear();
            NotifyPropertyChanged("Total");
        }

        public bool TryGetValue(string key, out SRClient value)
        {
            return _clients.TryGetValue(key, out value);
        }

        public bool ContainsKey(string key)
        {
            return _clients.ContainsKey(key);
        }

        public int ClientsOnFreq(double freq, RadioInformation.Modulation modulation)
        {
            if (!_serverSettings.GetSettingAsBool(ServerSettingsKeys.SHOW_TUNED_COUNT))
            {
                return 0;
            }
            var currentClientPos = ClientStateSingleton.Instance.PlayerGameState;
            var currentUnitId = currentClientPos.unitId;
            var currentVehicleId = currentClientPos.vehicleId;
            var coalitionSecurity = SyncedServerSettings.Instance.GetSettingAsBool(ServerSettingsKeys.COALITION_AUDIO_SECURITY);
            var globalFrequencies = _serverSettings.GlobalFrequencies;
            var global = globalFrequencies.Contains(freq);
            int count = 0;

            foreach (var client in _clients)
            {
                if (!client.Key.Equals(_guid))
                {
                    // check that either coalition radio security is disabled OR the coalitions match
                    if (global|| (!coalitionSecurity || (client.Value.Coalition == currentClientPos.coalition)))
                    {

                        var radioInfo = client.Value.GameState;

                        if (radioInfo != null)
                        {
                            RadioReceivingState radioReceivingState = null;
                            var receivingRadio = radioInfo.CanHearTransmission(freq,
                                modulation,
                                currentUnitId,
                                currentVehicleId,
                                new List<int>(),
                                out radioReceivingState);

                            //only send if we can hear!
                            if (receivingRadio != null)
                            {
                                count++;
                            }
                        }
                    }
                }
            }

            return count;
        }

        public int ClientsOnChannel(int channel)
        {
            var channelFrequency = GetChannelFrequency(channel);
            var currentClientPos = ClientStateSingleton.Instance.PlayerGameState;
            var ownCoalition = currentClientPos?.coalition ?? 0;
            var count = currentClientPos != null &&
                        IsActiveCoalition(ownCoalition) &&
                        HasRadioTunedToChannel(currentClientPos, channelFrequency)
                ? 1
                : 0;

            foreach (var client in _clients)
            {
                var srClient = client.Value;
                if (srClient == null ||
                    client.Key.Equals(_guid) ||
                    !IsSameActiveCoalition(srClient.Coalition, ownCoalition) ||
                    srClient.GameState?.radios == null)
                {
                    continue;
                }

                if (HasRadioTunedToChannel(srClient.GameState, channelFrequency))
                {
                    count++;
                }
            }

            return count;
        }

        public bool IsChannelOccupied(int channel)
        {
            return ClientsOnChannel(channel) > 0;
        }

        private static double GetChannelFrequency(int channel)
        {
            return PlayerGameState.START_FREQ + PlayerGameState.CHANNEL_OFFSET * channel;
        }

        private static bool IsActiveCoalition(int coalition)
        {
            return coalition > 0;
        }

        private static bool IsSameActiveCoalition(int clientCoalition, int ownCoalition)
        {
            return IsActiveCoalition(ownCoalition) && clientCoalition == ownCoalition;
        }

        private static bool HasRadioTunedToChannel(PlayerGameState gameState, double channelFrequency)
        {
            foreach (var radio in gameState.radios)
            {
                if (radio == null ||
                    radio.modulation == RadioInformation.Modulation.DISABLED ||
                    radio.modulation == RadioInformation.Modulation.INTERCOM)
                {
                    continue;
                }

                if (PlayerGameState.FreqCloseEnough(radio.freq, channelFrequency))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
