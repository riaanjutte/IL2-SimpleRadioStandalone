using System;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ClientAdmin
{
    public static class ClientAdminPresentation
    {
        public const string AllCoalitions = "All";
        public const string BlueCoalition = "Blue";
        public const string RedCoalition = "Red";
        public const string Spectators = "Spectators";

        public static readonly string[] CoalitionFilters =
        {
            AllCoalitions,
            BlueCoalition,
            RedCoalition,
            Spectators
        };

        public static string GetCoalitionName(int coalition)
        {
            switch (coalition)
            {
                case 1:
                    return RedCoalition;
                case 2:
                    return BlueCoalition;
                default:
                    return Spectators;
            }
        }

        public static string FormatRadioChannel(PlayerGameState gameState, int radioIndex)
        {
            var channel = GetRadioChannel(gameState, radioIndex);
            return channel > 0 ? channel.ToString() : "--";
        }

        public static bool Matches(SRClient client, string searchText, string coalitionFilter)
        {
            if (client == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(coalitionFilter) &&
                !string.Equals(coalitionFilter, AllCoalitions, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(coalitionFilter, GetCoalitionName(client.Coalition), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            var searchableText = string.Join(" ", new[]
            {
                client.Name ?? string.Empty,
                client.AssignedCallsign ?? string.Empty,
                GetCoalitionName(client.Coalition),
                FormatRadioChannel(client.GameState, 1),
                FormatRadioChannel(client.GameState, 2)
            });

            var terms = searchText.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            foreach (var term in terms)
            {
                if (searchableText.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static int GetRadioChannel(PlayerGameState gameState, int radioIndex)
        {
            if (gameState == null || gameState.radios == null || gameState.radios.Length <= radioIndex)
            {
                return 0;
            }

            var radio = gameState.radios[radioIndex];
            if (radio == null || radio.modulation == RadioInformation.Modulation.DISABLED ||
                radio.modulation == RadioInformation.Modulation.INTERCOM)
            {
                return 0;
            }

            var channel = radio.Channel;
            return channel > 0 ? channel : radio.channel;
        }
    }
}
