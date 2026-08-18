using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Easy.MessageHub;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    public class SyncedServerSettings
    {
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static SyncedServerSettings instance;
        private static readonly object _lock = new object();
        private static readonly Dictionary<string, string> defaults = DefaultServerSettings.Defaults;

        private readonly ConcurrentDictionary<string, string> _settings;
        private volatile Dictionary<int, string> _channelNames = new Dictionary<int, string>();
        private int _channelNamesRevision;

        public List<double> GlobalFrequencies { get; set; } = new List<double>();

        // Node Limit of 0 means no retransmission
        public int RetransmitNodeLimit { get; set; } = 0;

        public SyncedServerSettings()
        {
            _settings = new ConcurrentDictionary<string, string>();
        }

        public static SyncedServerSettings Instance
        {
            get
            {
                lock (_lock)
                {
                    if (instance == null)
                    {
                        instance = new SyncedServerSettings();
                    }
                }
                return instance;
            }
        }

        public string GetSetting(ServerSettingsKeys key)
        {
            string setting = key.ToString();

            return _settings.GetOrAdd(setting, defaults.ContainsKey(setting) ? defaults[setting] : "");
        }

        public int GetSettingInt(ServerSettingsKeys key)
        {
            string setting = key.ToString();

            return int.Parse(_settings.GetOrAdd(setting, defaults.ContainsKey(setting) ? defaults[setting] : ""));
        }

        public bool GetSettingAsBool(ServerSettingsKeys key)
        {
            return Convert.ToBoolean(GetSetting(key));
        }

        public bool GetOptionalSettingAsBool(ServerSettingsKeys key)
        {
            return _settings.TryGetValue(key.ToString(), out var value) &&
                   bool.TryParse(value, out var result) &&
                   result;
        }

        public string GetChannelName(int channel)
        {
            return _channelNames.TryGetValue(channel, out var name) ? name : null;
        }

        public Dictionary<int, string> GetChannelNames()
        {
            return new Dictionary<int, string>(_channelNames);
        }

        public int ChannelNamesRevision => _channelNamesRevision;

        public void Decode(Dictionary<string, string> encoded)
        {
            // Optional capabilities must not carry over when reconnecting to an older server.
            _settings.TryRemove(ServerSettingsKeys.PILOT_ROSTER_DATA_AVAILABLE.ToString(), out _);
            _settings.TryRemove(ServerSettingsKeys.RADIO_COLLISION_EFFECTS.ToString(), out _);
            _settings.TryRemove(ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS.ToString(), out _);
            foreach (var settingName in _settings.Keys)
            {
                if (settingName.StartsWith(ChannelNameSettings.SyncedSettingPrefix, StringComparison.Ordinal))
                {
                    _settings.TryRemove(settingName, out _);
                }
            }

            var channelNames = new Dictionary<int, string>();

            foreach (KeyValuePair<string, string> kvp in encoded)
            {
                _settings.AddOrUpdate(kvp.Key, kvp.Value, (key, oldVal) => kvp.Value);

                if (ChannelNameSettings.TryParseSyncedSettingName(kvp.Key, out var channel))
                {
                    var name = ChannelNameSettings.NormalizeName(kvp.Value);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        channelNames[channel] = name;
                    }
                }
                
                if (kvp.Key.Equals(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString()))
                {
                    var freqStringList = kvp.Value.Split(',');

                    var newList = new List<double>();
                    foreach (var freq in freqStringList)
                    {
                        if (double.TryParse(freq.Trim(), out var freqDouble))
                        {
                            freqDouble *= 1e+6; //convert to Hz from MHz
                            newList.Add(freqDouble);
                            Logger.Debug("Adding Server Global Frequency: " + freqDouble);
                        }
                    }

                    GlobalFrequencies = newList;
                }
            }

            _channelNames = channelNames;
            Interlocked.Increment(ref _channelNamesRevision);


            var current = ClientStateSingleton.Instance.PlayerGameState.radios[2].modulation;
            if (GetSettingAsBool(ServerSettingsKeys.SECOND_RADIO_ENABLED))
            {
                ClientStateSingleton.Instance.PlayerGameState.radios[2].modulation =
                    RadioInformation.Modulation.AM;
            }
            else
            {
                ClientStateSingleton.Instance.PlayerGameState.radios[2].modulation =
                    RadioInformation.Modulation.DISABLED;
            }

            //handle allowing the second radio
            if (current != ClientStateSingleton.Instance.PlayerGameState.radios[2].modulation)
            {
                MessageHub.Instance.Publish(new PlayerStateUpdate());
            }
        }
    }
}
