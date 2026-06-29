using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public static class PilotRosterBuilder
    {
        public static IList<PilotRosterEntry> Build(PlayerGameState localState, IEnumerable<SRClient> connectedClients)
        {
            var ownCoalition = localState?.coalition ?? 0;
            if (ownCoalition <= 0)
            {
                return new List<PilotRosterEntry>();
            }

            return (connectedClients ?? Enumerable.Empty<SRClient>())
                .Where(client => client != null &&
                                 client.Coalition == ownCoalition &&
                                 IsRealPlayerClient(client.Name))
                .Select(client => new PilotRosterEntry(
                    client.AssignedCallsign,
                    client.Name,
                    client.AssignedVehicle,
                    FormatRadioChannel(client.GameState, 1),
                    FormatRadioChannel(client.GameState, 2)))
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

            var activeSquads = (connectedClients ?? Enumerable.Empty<SRClient>())
                .Where(client => client != null &&
                                 client.Coalition == ownCoalition &&
                                 IsRealPlayerClient(client.Name))
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

        private static string FormatRadioChannel(PlayerGameState gameState, int radioIndex)
        {
            if (gameState?.radios == null ||
                gameState.radios.Length <= radioIndex ||
                gameState.radios[radioIndex] == null)
            {
                return "--";
            }

            var radio = gameState.radios[radioIndex];
            if (radio.modulation == RadioInformation.Modulation.DISABLED ||
                radio.modulation == RadioInformation.Modulation.INTERCOM)
            {
                return "--";
            }

            var channel = radio.Channel;
            if (channel <= 0)
            {
                channel = radio.channel;
            }

            return channel > 0
                ? "CHN " + channel.ToString(CultureInfo.InvariantCulture)
                : "--";
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

        private static bool IsRealPlayerClient(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            var trimmedName = name.Trim();
            return !string.Equals(trimmedName, "Axis Airfield", System.StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(trimmedName, "Axis Command", System.StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(trimmedName, "Allies Airfield", System.StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(trimmedName, "Allies Command", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
