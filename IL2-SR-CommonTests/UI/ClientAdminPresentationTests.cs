using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.UI.ClientAdmin;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class ClientAdminPresentationTests
    {
        [TestMethod]
        public void RadioChannelsAreFormattedFromLiveGameState()
        {
            var gameState = new PlayerGameState();
            gameState.radios[1].freq = PlayerGameState.START_FREQ + 4 * PlayerGameState.CHANNEL_OFFSET;
            gameState.radios[2].modulation = RadioInformation.Modulation.AM;
            gameState.radios[2].freq = PlayerGameState.START_FREQ + 9 * PlayerGameState.CHANNEL_OFFSET;

            Assert.AreEqual("4", ClientAdminPresentation.FormatRadioChannel(gameState, 1));
            Assert.AreEqual("9", ClientAdminPresentation.FormatRadioChannel(gameState, 2));
            Assert.AreEqual("--", ClientAdminPresentation.FormatRadioChannel(null, 1));
        }

        [TestMethod]
        public void SearchMatchesPilotMetadataAcrossMultipleTerms()
        {
            var client = new SRClient
            {
                Name = "=TBA=Haluter",
                AssignedCallsign = "Raven-1",
                Coalition = 2,
                GameState = new PlayerGameState()
            };

            Assert.IsTrue(ClientAdminPresentation.Matches(client, "haluter raven", "All"));
            Assert.IsTrue(ClientAdminPresentation.Matches(client, "raven blue", "Blue"));
            Assert.IsFalse(ClientAdminPresentation.Matches(client, "haluter axis", "All"));
            Assert.IsFalse(ClientAdminPresentation.Matches(client, string.Empty, "Red"));
        }

        [TestMethod]
        public void RadioChannelSortValuesRemainNumeric()
        {
            var channelTwo = new PlayerGameState();
            channelTwo.radios[1].freq = PlayerGameState.START_FREQ + 2 * PlayerGameState.CHANNEL_OFFSET;
            var channelTen = new PlayerGameState();
            channelTen.radios[1].freq = PlayerGameState.START_FREQ + 10 * PlayerGameState.CHANNEL_OFFSET;

            Assert.IsTrue(ClientAdminPresentation.GetRadioChannel(channelTwo, 1) <
                          ClientAdminPresentation.GetRadioChannel(channelTen, 1));
        }

        [TestMethod]
        public void ClientRaisesLiveAdminPropertyNotifications()
        {
            var client = new SRClient();
            var changedProperties = new System.Collections.Generic.List<string>();
            client.PropertyChanged += (sender, args) => changedProperties.Add(args.PropertyName);

            client.GameState = new PlayerGameState();
            client.VoipPort = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 5002);

            CollectionAssert.Contains(changedProperties, "GameState");
            CollectionAssert.Contains(changedProperties, "VoipPort");
        }
    }
}
