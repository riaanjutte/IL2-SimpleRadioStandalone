using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public static class CombatBoxCallsignRoster
    {
        public static Dictionary<CallsignRosterKey, string> Parse(string rosterJson)
        {
            var callsigns = new Dictionary<CallsignRosterKey, string>();

            if (string.IsNullOrWhiteSpace(rosterJson))
            {
                return callsigns;
            }

            var roster = JsonConvert.DeserializeObject<CallsignRoster>(rosterJson);
            if (roster?.Players == null)
            {
                return callsigns;
            }

            foreach (var player in roster.Players)
            {
                if (player == null ||
                    string.IsNullOrWhiteSpace(player.Name) ||
                    string.IsNullOrWhiteSpace(player.Callsign) ||
                    player.CoalitionCode <= 0)
                {
                    continue;
                }

                callsigns[new CallsignRosterKey(player.Name, player.CoalitionCode)] = player.Callsign.Trim();
            }

            return callsigns;
        }

        private class CallsignRoster
        {
            [JsonProperty("players")]
            public List<CallsignRosterPlayer> Players { get; set; }
        }

        private class CallsignRosterPlayer
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("coalitionCode")]
            public int CoalitionCode { get; set; }

            [JsonProperty("callsign")]
            public string Callsign { get; set; }
        }
    }

    public struct CallsignRosterKey : IEquatable<CallsignRosterKey>
    {
        private readonly string _playerName;
        private readonly int _coalition;

        public CallsignRosterKey(string playerName, int coalition)
        {
            _playerName = (playerName ?? string.Empty).Trim();
            _coalition = coalition;
        }

        public bool Equals(CallsignRosterKey other)
        {
            return _coalition == other._coalition &&
                   string.Equals(_playerName, other._playerName, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is CallsignRosterKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((_playerName == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(_playerName)) * 397) ^ _coalition;
            }
        }
    }
}
