using System;
using System.Collections.Generic;
using System.Globalization;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.UI.MainWindow
{
    public sealed class ServerHealthSnapshot
    {
        private static readonly TimeSpan RecentTransmissionWindow = TimeSpan.FromSeconds(2);
        private static readonly HashSet<string> ExcludedClientNames = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "Axis Command",
            "Allies Command",
            "Axis Airfield",
            "Allies Airfield",
            "CB Radio Lobby"
        };

        private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(3);

        private ServerHealthSnapshot(bool isRunning, string status, string uptime, int clients, int voiceLinks,
            int recentTransmitters, string memory)
        {
            IsRunning = isRunning;
            Status = status;
            Uptime = uptime;
            Clients = clients;
            VoiceLinks = voiceLinks.ToString(CultureInfo.InvariantCulture) + "/" +
                         clients.ToString(CultureInfo.InvariantCulture);
            RecentTransmitters = recentTransmitters;
            Memory = memory;
        }

        public bool IsRunning { get; }
        public string Status { get; }
        public string Uptime { get; }
        public int Clients { get; }
        public string VoiceLinks { get; }
        public int RecentTransmitters { get; }
        public string Memory { get; }

        public static ServerHealthSnapshot Create(bool isRunning, bool isTcpListenerRunning,
            bool isUdpListenerRunning, DateTime? startedAtUtc,
            IEnumerable<SRClient> clients, long workingSetBytes, DateTime utcNow)
        {
            var clientCount = 0;
            var voiceLinks = 0;
            var recentTransmitters = 0;

            foreach (var client in clients ?? new SRClient[0])
            {
                if (client == null)
                {
                    continue;
                }

                var clientName = client.Name == null ? string.Empty : client.Name.Trim();
                if (ExcludedClientNames.Contains(clientName))
                {
                    continue;
                }

                clientCount++;
                if (client.VoipPort != null)
                {
                    voiceLinks++;
                }

                if (client.LastTransmissionReceived != default(DateTime))
                {
                    var transmissionAge = utcNow - client.LastTransmissionReceived.ToUniversalTime();
                    if (transmissionAge >= TimeSpan.Zero && transmissionAge <= RecentTransmissionWindow)
                    {
                        recentTransmitters++;
                    }
                }
            }

            var uptimeValue = isRunning && startedAtUtc.HasValue
                ? utcNow - startedAtUtc.Value
                : TimeSpan.Zero;
            var uptime = isRunning && startedAtUtc.HasValue
                ? FormatUptime(uptimeValue)
                : "00:00:00";
            var memoryMegabytes = Math.Max(0L, workingSetBytes) / (1024d * 1024d);

            var listenersHealthy = isRunning && isTcpListenerRunning && isUdpListenerRunning;
            var status = !isRunning
                ? "STOPPED"
                : listenersHealthy
                    ? "RUNNING"
                    : uptimeValue < StartupGracePeriod
                        ? "STARTING"
                        : "DEGRADED";

            return new ServerHealthSnapshot(listenersHealthy, status, uptime, clientCount, voiceLinks, recentTransmitters,
                memoryMegabytes.ToString("0", CultureInfo.InvariantCulture) + " MB");
        }

        internal static string FormatUptime(TimeSpan uptime)
        {
            if (uptime < TimeSpan.Zero)
            {
                uptime = TimeSpan.Zero;
            }

            var clock = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                uptime.Hours, uptime.Minutes, uptime.Seconds);
            return uptime.Days > 0
                ? uptime.Days.ToString(CultureInfo.InvariantCulture) + "d " + clock
                : clock;
        }
    }
}
