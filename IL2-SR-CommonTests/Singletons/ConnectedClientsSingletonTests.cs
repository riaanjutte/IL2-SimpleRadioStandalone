using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Singletons
{
    [TestClass]
    public class ConnectedClientsSingletonTests
    {
        private const int FriendlyCoalition = 2;
        private const int EnemyCoalition = 1;

        [TestInitialize]
        public void TestInitialize()
        {
            ConnectedClientsSingleton.Instance.Clear();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConnectedClientsSingleton.Instance.Clear();
        }

        [TestMethod]
        public void RciSuffixNameIsDetectedAndTrimmed()
        {
            AddClient("friendly-rci", "KRAKEN__RCI_", FriendlyCoalition);

            Assert.AreEqual(RciStatus.FriendlyOnly, ConnectedClientsSingleton.Instance.GetRciStatus(FriendlyCoalition));
            Assert.AreEqual("KRAKEN", ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition));
        }

        [TestMethod]
        public void RciPrefixNameIsDetectedAndTrimmed()
        {
            AddClient("friendly-rci", "_RCI_DEFCON", FriendlyCoalition);

            Assert.AreEqual(RciStatus.FriendlyOnly, ConnectedClientsSingleton.Instance.GetRciStatus(FriendlyCoalition));
            Assert.AreEqual("DEFCON", ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition));
        }

        [TestMethod]
        public void RciNameParsingIsCaseInsensitive()
        {
            AddClient("friendly-rci", "Defcon_rCi", FriendlyCoalition);

            Assert.AreEqual(RciStatus.FriendlyOnly, ConnectedClientsSingleton.Instance.GetRciStatus(FriendlyCoalition));
            Assert.AreEqual("Defcon", ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition));
        }

        [TestMethod]
        public void RciMarkersWithoutCallsignCountAsActiveButDoNotDisplayCallsign()
        {
            AddClient("friendly-rci", "__RCI_", FriendlyCoalition);

            Assert.AreEqual(RciStatus.FriendlyOnly, ConnectedClientsSingleton.Instance.GetRciStatus(FriendlyCoalition));
            Assert.AreEqual(string.Empty, ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition));
        }

        [TestMethod]
        public void NormalPlayerNamesContainingRciAreIgnored()
        {
            AddClient("normal-1", "RCI", FriendlyCoalition);
            AddClient("normal-2", "DEFCON_RCI_TRAILER", FriendlyCoalition);
            AddClient("normal-3", "MY_RCI_CALLSIGN", FriendlyCoalition);

            Assert.AreEqual(RciStatus.None, ConnectedClientsSingleton.Instance.GetRciStatus(FriendlyCoalition));
            Assert.AreEqual(string.Empty, ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition));
        }

        [TestMethod]
        public void FriendlyAndEnemyRciNamesProduceBothStatus()
        {
            AddClient("friendly-rci", "DEFCON__RCI", FriendlyCoalition);
            AddClient("enemy-rci", "RCI_KRAKEN", EnemyCoalition);

            Assert.AreEqual(RciStatus.Both, ConnectedClientsSingleton.Instance.GetRciStatus(FriendlyCoalition));
            Assert.AreEqual("DEFCON", ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition));
        }

        [TestMethod]
        public void FriendlyRciCallsignsAreDistinctCaseInsensitively()
        {
            AddClient("friendly-rci-1", "DEFCON__RCI", FriendlyCoalition);
            AddClient("friendly-rci-2", "defcon__RCI", FriendlyCoalition);

            var callsign = ConnectedClientsSingleton.Instance.GetFriendlyRciCallsign(FriendlyCoalition);

            Assert.IsTrue(string.Equals("DEFCON", callsign, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(callsign.Contains(","));
        }

        private static void AddClient(string guid, string name, int coalition)
        {
            ConnectedClientsSingleton.Instance[guid] = new SRClient
            {
                ClientGuid = guid,
                Name = name,
                Coalition = coalition,
                LastUpdate = DateTime.Now.Ticks
            };
        }
    }
}
