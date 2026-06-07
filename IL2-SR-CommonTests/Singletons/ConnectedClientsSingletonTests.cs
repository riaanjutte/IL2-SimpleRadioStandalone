using System;
using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
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
            ResetLocalState();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConnectedClientsSingleton.Instance.Clear();
            ResetLocalState();
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

        [TestMethod]
        public void ChannelOccupancyUsesConnectedClientFrequenciesIndependentOfLocalRadioState()
        {
            ClientStateSingleton.Instance.PlayerGameState.radios[1].modulation = RadioInformation.Modulation.DISABLED;
            AddClient("friendly-channel", "Broadway", FriendlyCoalition, 7);

            Assert.IsTrue(ConnectedClientsSingleton.Instance.IsChannelOccupied(7));
            Assert.AreEqual(1, ConnectedClientsSingleton.Instance.ClientsOnChannel(7));
        }

        [TestMethod]
        public void ChannelOccupancyIgnoresDisabledAndIntercomRadios()
        {
            var state = new PlayerGameState();
            state.radios[0].channel = 8;
            state.radios[0].freq = ChannelFrequency(8);
            state.radios[0].modulation = RadioInformation.Modulation.INTERCOM;
            state.radios[1].channel = 8;
            state.radios[1].freq = ChannelFrequency(8);
            state.radios[1].modulation = RadioInformation.Modulation.DISABLED;
            state.radios[2].channel = 8;
            state.radios[2].freq = ChannelFrequency(8);
            state.radios[2].modulation = RadioInformation.Modulation.DISABLED;
            AddClient("disabled-radios", "Disabled", FriendlyCoalition, state);

            Assert.IsFalse(ConnectedClientsSingleton.Instance.IsChannelOccupied(8));
            Assert.AreEqual(0, ConnectedClientsSingleton.Instance.ClientsOnChannel(8));
        }

        [TestMethod]
        public void ChannelOccupancyIgnoresTunedCountAndServerCoalitionFiltersButCountsFriendlyOnly()
        {
            SyncedServerSettings.Instance.Decode(new Dictionary<string, string>
            {
                {ServerSettingsKeys.SHOW_TUNED_COUNT.ToString(), "false"},
                {ServerSettingsKeys.COALITION_AUDIO_SECURITY.ToString(), "true"}
            });
            AddClient("friendly-channel", "Friendly", FriendlyCoalition, 9);
            AddClient("enemy-channel", "Enemy", EnemyCoalition, 9);

            Assert.IsTrue(ConnectedClientsSingleton.Instance.IsChannelOccupied(9));
            Assert.AreEqual(1, ConnectedClientsSingleton.Instance.ClientsOnChannel(9));
        }

        [TestMethod]
        public void ChannelOccupancyIgnoresEnemyCoalitionClients()
        {
            AddClient("enemy-channel", "Enemy", EnemyCoalition, 6);

            Assert.IsFalse(ConnectedClientsSingleton.Instance.IsChannelOccupied(6));
            Assert.AreEqual(0, ConnectedClientsSingleton.Instance.ClientsOnChannel(6));
        }

        [TestMethod]
        public void ChannelOccupancyIncludesLocalPlayerRadio()
        {
            ClientStateSingleton.Instance.PlayerGameState.radios[1].freq = ChannelFrequency(4);
            ClientStateSingleton.Instance.PlayerGameState.radios[1].modulation = RadioInformation.Modulation.AM;

            Assert.IsTrue(ConnectedClientsSingleton.Instance.IsChannelOccupied(4));
            Assert.AreEqual(1, ConnectedClientsSingleton.Instance.ClientsOnChannel(4));
        }

        [TestMethod]
        public void ChannelOccupancyIgnoresNeutralLobbyClients()
        {
            AddClient("neutral-channel", "Lobby", 0, 10);

            Assert.IsFalse(ConnectedClientsSingleton.Instance.IsChannelOccupied(10));
            Assert.AreEqual(0, ConnectedClientsSingleton.Instance.ClientsOnChannel(10));
        }

        [TestMethod]
        public void ChannelOccupancyIgnoresLocalPlayerInNeutralLobby()
        {
            ClientStateSingleton.Instance.PlayerGameState.coalition = 0;
            ClientStateSingleton.Instance.PlayerGameState.radios[1].freq = ChannelFrequency(11);
            ClientStateSingleton.Instance.PlayerGameState.radios[1].modulation = RadioInformation.Modulation.AM;

            Assert.IsFalse(ConnectedClientsSingleton.Instance.IsChannelOccupied(11));
            Assert.AreEqual(0, ConnectedClientsSingleton.Instance.ClientsOnChannel(11));
        }

        [TestMethod]
        public void ClientsOnFreqIncludesLocalPlayerRadio()
        {
            ClientStateSingleton.Instance.PlayerGameState.radios[1].freq = ChannelFrequency(1);
            ClientStateSingleton.Instance.PlayerGameState.radios[1].modulation = RadioInformation.Modulation.AM;

            Assert.AreEqual(1, ConnectedClientsSingleton.Instance.ClientsOnFreq(
                ChannelFrequency(1),
                RadioInformation.Modulation.AM));
        }

        [TestMethod]
        public void ClientsOnFreqIncludesLocalAndRemotePilots()
        {
            ClientStateSingleton.Instance.PlayerGameState.radios[1].freq = ChannelFrequency(5);
            ClientStateSingleton.Instance.PlayerGameState.radios[1].modulation = RadioInformation.Modulation.AM;
            AddClient("friendly-channel", "Broadway", FriendlyCoalition, 5);

            Assert.AreEqual(2, ConnectedClientsSingleton.Instance.ClientsOnFreq(
                ChannelFrequency(5),
                RadioInformation.Modulation.AM));
        }

        [TestMethod]
        public void ClientsOnFreqDoesNotIncludeLocalPlayerInNeutralLobby()
        {
            ClientStateSingleton.Instance.PlayerGameState.coalition = 0;
            ClientStateSingleton.Instance.PlayerGameState.radios[1].freq = ChannelFrequency(3);
            ClientStateSingleton.Instance.PlayerGameState.radios[1].modulation = RadioInformation.Modulation.AM;

            Assert.AreEqual(0, ConnectedClientsSingleton.Instance.ClientsOnFreq(
                ChannelFrequency(3),
                RadioInformation.Modulation.AM));
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

        private static void AddClient(string guid, string name, int coalition, int channel)
        {
            var state = new PlayerGameState();
            state.radios[1].channel = 1;
            state.radios[1].freq = ChannelFrequency(channel);
            state.radios[1].modulation = RadioInformation.Modulation.AM;
            AddClient(guid, name, coalition, state);
        }

        private static void AddClient(string guid, string name, int coalition, PlayerGameState gameState)
        {
            ConnectedClientsSingleton.Instance[guid] = new SRClient
            {
                ClientGuid = guid,
                Name = name,
                Coalition = coalition,
                LastUpdate = DateTime.Now.Ticks,
                GameState = gameState
            };
        }

        private static void ResetLocalState()
        {
            SyncedServerSettings.Instance.Decode(new Dictionary<string, string>
            {
                {ServerSettingsKeys.SHOW_TUNED_COUNT.ToString(), "true"},
                {ServerSettingsKeys.COALITION_AUDIO_SECURITY.ToString(), "false"}
            });
            var localState = ClientStateSingleton.Instance.PlayerGameState;
            localState.coalition = FriendlyCoalition;
            localState.radios[1].channel = 1;
            localState.radios[1].freq = ChannelFrequency(1);
            localState.radios[1].modulation = RadioInformation.Modulation.AM;
            localState.radios[2].channel = 2;
            localState.radios[2].freq = ChannelFrequency(2);
            localState.radios[2].modulation = RadioInformation.Modulation.DISABLED;
        }

        private static double ChannelFrequency(int channel)
        {
            return PlayerGameState.START_FREQ + PlayerGameState.CHANNEL_OFFSET * channel;
        }
    }
}
