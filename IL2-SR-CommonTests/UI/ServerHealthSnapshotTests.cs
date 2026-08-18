using System;
using System.Collections.Generic;
using System.Net;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.UI.MainWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class ServerHealthSnapshotTests
    {
        [TestMethod]
        public void SnapshotReportsLiveVoiceAndProcessHealth()
        {
            var now = new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);
            var clients = new List<SRClient>
            {
                new SRClient
                {
                    VoipPort = new IPEndPoint(IPAddress.Loopback, 5002),
                    LastTransmissionReceived = now.AddMilliseconds(-500).ToLocalTime()
                },
                new SRClient()
            };

            var snapshot = ServerHealthSnapshot.Create(true, true, true,
                now.AddDays(-1).AddHours(-2).AddMinutes(-3),
                clients, 128L * 1024L * 1024L, now);

            Assert.IsTrue(snapshot.IsRunning);
            Assert.AreEqual("RUNNING", snapshot.Status);
            Assert.AreEqual("1d 02:03:00", snapshot.Uptime);
            Assert.AreEqual(2, snapshot.Clients);
            Assert.AreEqual("1/2", snapshot.VoiceLinks);
            Assert.AreEqual(1, snapshot.RecentTransmitters);
            Assert.AreEqual("128 MB", snapshot.Memory);
        }

        [TestMethod]
        public void StoppedSnapshotHasZeroUptimeAndSafeMemoryValue()
        {
            var snapshot = ServerHealthSnapshot.Create(false, false, false, null, null, -1, DateTime.UtcNow);

            Assert.IsFalse(snapshot.IsRunning);
            Assert.AreEqual("STOPPED", snapshot.Status);
            Assert.AreEqual("00:00:00", snapshot.Uptime);
            Assert.AreEqual("0/0", snapshot.VoiceLinks);
            Assert.AreEqual("0 MB", snapshot.Memory);
        }

        [TestMethod]
        public void SnapshotExcludesCombatBoxServiceClientsFromHealthCounts()
        {
            var now = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
            var clients = new List<SRClient>
            {
                CreateActiveClient("Human Pilot", now),
                CreateActiveClient("Axis Command", now),
                CreateActiveClient("allies command", now),
                CreateActiveClient(" Axis Airfield ", now),
                CreateActiveClient("Allies Airfield", now),
                CreateActiveClient("CB Radio Lobby", now)
            };

            var snapshot = ServerHealthSnapshot.Create(true, true, true, now.AddMinutes(-5), clients, 0, now);

            Assert.AreEqual(1, snapshot.Clients);
            Assert.AreEqual("1/1", snapshot.VoiceLinks);
            Assert.AreEqual(1, snapshot.RecentTransmitters);
        }

        [TestMethod]
        public void SnapshotReportsDegradedWhenEitherListenerIsUnavailable()
        {
            var now = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

            var snapshot = ServerHealthSnapshot.Create(true, true, false,
                now.AddSeconds(-10), null, 0, now);

            Assert.IsFalse(snapshot.IsRunning);
            Assert.AreEqual("DEGRADED", snapshot.Status);
        }

        [TestMethod]
        public void SnapshotAllowsListenersAShortStartupGracePeriod()
        {
            var now = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);

            var snapshot = ServerHealthSnapshot.Create(true, false, false,
                now.AddSeconds(-1), null, 0, now);

            Assert.IsFalse(snapshot.IsRunning);
            Assert.AreEqual("STARTING", snapshot.Status);
        }

        private static SRClient CreateActiveClient(string name, DateTime now)
        {
            return new SRClient
            {
                Name = name,
                VoipPort = new IPEndPoint(IPAddress.Loopback, 5002),
                LastTransmissionReceived = now.AddMilliseconds(-500).ToLocalTime()
            };
        }
    }
}
