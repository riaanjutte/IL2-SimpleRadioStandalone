using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                    FormatRadioChannel(client.GameState, 1),
                    FormatRadioChannel(client.GameState, 2)))
                .OrderBy(entry => entry.Callsign == "--" ? 1 : 0)
                .ThenBy(entry => entry.Callsign, System.StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.PilotName, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
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
