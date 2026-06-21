using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public static class CombatBoxCallsignRoster
    {
        public static Dictionary<CallsignRosterKey, string> Parse(string rosterJson)
        {
            return ParseAssignments(rosterJson)
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Callsign))
                .ToDictionary(pair => pair.Key, pair => pair.Value.Callsign);
        }

        public static Dictionary<CallsignRosterKey, CombatBoxRosterAssignment> ParseAssignments(string rosterJson)
        {
            var assignments = new Dictionary<CallsignRosterKey, CombatBoxRosterAssignment>();

            if (string.IsNullOrWhiteSpace(rosterJson))
            {
                return assignments;
            }

            var roster = JsonConvert.DeserializeObject<CallsignRoster>(rosterJson);
            if (roster?.Players == null)
            {
                return assignments;
            }

            foreach (var player in roster.Players)
            {
                if (player == null ||
                    string.IsNullOrWhiteSpace(player.Name) ||
                    player.CoalitionCode <= 0)
                {
                    continue;
                }

                var assignment = new CombatBoxRosterAssignment(player.Callsign, player.Vehicle);
                if (!assignment.HasCallsign && !assignment.HasVehicle)
                {
                    continue;
                }

                assignments[new CallsignRosterKey(player.Name, player.CoalitionCode)] = assignment;
            }

            return assignments;
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

            [JsonProperty("vehicle")]
            public string Vehicle { get; set; }
        }
    }

    public class CombatBoxRosterAssignment
    {
        public CombatBoxRosterAssignment(string callsign, string vehicle)
        {
            Callsign = Normalize(callsign);
            Vehicle = Normalize(vehicle);
        }

        public string Callsign { get; }

        public string Vehicle { get; }

        public bool HasCallsign => !string.IsNullOrWhiteSpace(Callsign);

        public bool HasVehicle => !string.IsNullOrWhiteSpace(Vehicle);

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
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
