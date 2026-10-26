using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public static class PilotRosterBuilder
    {
        // Tower and command clients on Great Battles and Korea servers; never pilots, whatever the server's list says.
        private static readonly string[] BuiltInNonPilotNames =
        {
            "Axis Airfield", "Axis Command", "Allies Airfield", "Allies Command",
            "UN Airfield", "UN Command", "CF Airfield", "CF Command"
        };

        public static IList<PilotRosterEntry> Build(PlayerGameState localState, IEnumerable<SRClient> connectedClients)
        {
            var ownCoalition = localState?.coalition ?? 0;
            if (ownCoalition <= 0)
            {
                return new List<PilotRosterEntry>();
            }

            var nonPilots = NonPilotNames(SyncedServerSettings.Instance.GetSetting(ServerSettingsKeys.PRIORITY_TRANSMITTER_NAMES));

            var entries = (connectedClients ?? Enumerable.Empty<SRClient>())
                .Where(client => client != null &&
                                 client.Coalition == ownCoalition &&
                                 IsRealPlayerClient(client.Name, nonPilots))
                .Select(client => new PilotRosterEntry(
                    client.AssignedCallsign,
                    client.Name,
                    client.AssignedVehicle,
                    client.AssignedAirfield,
                    GetRadioChannel(client.GameState, 1),
                    GetRadioChannel(client.GameState, 2)));

            return entries
                .OrderBy(entry => entry.Callsign == "--" ? 1 : 0)
                .ThenBy(entry => entry.Callsign, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.PilotName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string BuildActiveSquadOpsSummary(PlayerGameState localState, IEnumerable<SRClient> connectedClients)
        {
            var ownCoalition = localState?.coalition ?? 0;
            if (ownCoalition <= 0)
            {
                return string.Empty;
            }

            var nonPilots = NonPilotNames(SyncedServerSettings.Instance.GetSetting(ServerSettingsKeys.PRIORITY_TRANSMITTER_NAMES));

            var activeSquads = (connectedClients ?? Enumerable.Empty<SRClient>())
                .Where(client => client != null &&
                                 client.Coalition == ownCoalition &&
                                 IsRealPlayerClient(client.Name, nonPilots))
                .Select(client => new
                {
                    Squad = ExtractSquadTag(client.Name),
                    Channels = GetOperationalChannels(client.GameState)
                })
                .Where(client => !string.IsNullOrWhiteSpace(client.Squad))
                .SelectMany(client => client.Channels.Select(channel => new
                {
                    Channel = channel,
                    client.Squad
                }))
                .GroupBy(item => new { item.Channel, item.Squad })
                .Where(group => group.Count() >= 2)
                .OrderBy(group => group.Key.Channel)
                .ThenBy(group => group.Key.Squad, System.StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    group.Key.Channel,
                    group.Key.Squad,
                    Count = group.Count()
                })
                .GroupBy(group => group.Channel)
                .Select(channelGroup => "CH" + channelGroup.Key.ToString(CultureInfo.InvariantCulture) + " " +
                                        string.Join(", ", channelGroup.Select(group => group.Squad + "(" + group.Count.ToString(CultureInfo.InvariantCulture) + ")").ToArray()))
                .ToList();

            return activeSquads.Count == 0
                ? string.Empty
                : "ACTIVE SQUAD OPS: " + string.Join(" | ", activeSquads.ToArray());
        }

        public static string GetMajoritySquadTag(PlayerGameState localState,
            IEnumerable<SRClient> connectedClients, int channel)
        {
            var ownCoalition = localState?.coalition ?? 0;
            if (ownCoalition <= 0 || channel <= 2)
            {
                return string.Empty;
            }

            var nonPilots = NonPilotNames(SyncedServerSettings.Instance.GetSetting(ServerSettingsKeys.PRIORITY_TRANSMITTER_NAMES));

            var squadTags = (connectedClients ?? Enumerable.Empty<SRClient>())
                .Where(client => client != null &&
                                 client.Coalition == ownCoalition &&
                                 IsRealPlayerClient(client.Name, nonPilots) &&
                                 GetOperationalChannels(client.GameState).Contains(channel))
                .Select(client => ExtractSquadTag(client.Name))
                .ToList();

            if (squadTags.Count < 2)
            {
                return string.Empty;
            }

            var majority = squadTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .GroupBy(tag => tag, System.StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Tag = group.Key, Count = group.Count() })
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Tag, System.StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return majority != null && majority.Count >= 2 && majority.Count * 2 > squadTags.Count
                ? majority.Tag
                : string.Empty;
        }

        private static int GetRadioChannel(PlayerGameState gameState, int radioIndex)
        {
            if (gameState?.radios == null ||
                gameState.radios.Length <= radioIndex ||
                gameState.radios[radioIndex] == null)
            {
                return 0;
            }

            var radio = gameState.radios[radioIndex];
            if (radio.modulation == RadioInformation.Modulation.DISABLED ||
                radio.modulation == RadioInformation.Modulation.INTERCOM)
            {
                return 0;
            }

            var channel = radio.Channel;
            if (channel <= 0)
            {
                channel = radio.channel;
            }

            return channel > 0 ? channel : 0;
        }

        private static IEnumerable<int> GetOperationalChannels(PlayerGameState gameState)
        {
            if (gameState?.radios == null)
            {
                return Enumerable.Empty<int>();
            }

            var channels = new List<int>();
            for (var radioIndex = 1; radioIndex <= 2; radioIndex++)
            {
                if (gameState.radios.Length <= radioIndex || gameState.radios[radioIndex] == null)
                {
                    continue;
                }

                var radio = gameState.radios[radioIndex];
                if (radio.modulation == RadioInformation.Modulation.DISABLED ||
                    radio.modulation == RadioInformation.Modulation.INTERCOM)
                {
                    continue;
                }

                var channel = radio.Channel;
                if (channel <= 0)
                {
                    channel = radio.channel;
                }

                if (channel > 2)
                {
                    channels.Add(channel);
                }
            }

            return channels.Distinct();
        }

        private static string ExtractSquadTag(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return string.Empty;
            }

            var name = playerName.Trim();
            var match = Regex.Match(name, @"^=([A-Za-z0-9]{2,8})=");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }

            match = Regex.Match(name, @"^-([A-Za-z0-9]{2,8})-");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }

            match = Regex.Match(name, @"^\[([A-Za-z0-9]{2,8})\]");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }

            match = Regex.Match(name, @"^([A-Za-z]{1,4}[0-9]{1,4}|[0-9]{1,4}[A-Za-z]{1,4})[_\-.]");
            if (match.Success)
            {
                return match.Groups[1].Value.ToUpperInvariant();
            }

            return string.Empty;
        }

        /// <summary>
        /// Names that are not pilots: the built-in tower and command names plus the server's Priority Transmitters
        /// (a comma-separated list, the same setting the server uses for voice priority). Compared ignoring case and
        /// extra spaces.
        /// </summary>
        internal static HashSet<string> NonPilotNames(string priorityTransmitterNames)
        {
            var names = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var name in BuiltInNonPilotNames.Concat((priorityTransmitterNames ?? string.Empty).Split(',')))
            {
                var normalized = NormalizeName(name);
                if (normalized.Length > 0)
                {
                    names.Add(normalized);
                }
            }

            return names;
        }

        private static string NormalizeName(string name)
        {
            return Regex.Replace(name ?? string.Empty, @"[\s\p{Cf}]+", " ").Trim();
        }

        private static bool IsRealPlayerClient(string name, HashSet<string> nonPilotNames)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var normalizedName = NormalizeName(name);
            // RCI controllers are not pilots; the RCI status panel shows them instead.
            return !ConnectedClientsSingleton.IsRciName(normalizedName) && !nonPilotNames.Contains(normalizedName);
        }
    }
}
