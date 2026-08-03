using System.Text;
using Ciribob.IL2.SimpleRadio.Standalone.Client;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class LobbyMusicReceptionTests
    {
        [TestMethod]
        public void NeutralClientReceivesLobbyMusicWithoutBeingTunedToLobbyFrequency()
        {
            var playerState = new PlayerGameState();
            var packet = CreateLobbyMusicPacket();

            Assert.AreNotEqual(packet.Frequencies[0], playerState.radios[1].freq);

            var destination = UdpVoiceHandler.GetLobbyMusicReceivingPriority(0, playerState, packet);

            Assert.IsNotNull(destination);
            Assert.AreEqual(1, destination.ReceivingState.ReceivedOn);
            Assert.AreEqual(packet.Frequencies[0], destination.Frequency);
            Assert.AreSame(playerState.radios[1], destination.ReceivingRadio);
        }

        [TestMethod]
        public void SpawnedClientDoesNotReceiveLobbyMusic()
        {
            var destination = UdpVoiceHandler.GetLobbyMusicReceivingPriority(1, new PlayerGameState(),
                CreateLobbyMusicPacket());

            Assert.IsNull(destination);
        }

        [TestMethod]
        public void LobbyMusicIsCentredInStereoOutput()
        {
            var monoPcm = new byte[] { 0x10, 0x27 };

            var stereoPcm = ClientAudioProvider.CreateBalancedLobbyMusicMix(monoPcm);

            Assert.AreEqual(4, stereoPcm.Length);
            CollectionAssert.AreEqual(new[] { stereoPcm[0], stereoPcm[1] },
                new[] { stereoPcm[2], stereoPcm[3] });
            Assert.IsTrue(stereoPcm[0] != 0 || stereoPcm[1] != 0);
        }

        private static UDPVoicePacket CreateLobbyMusicPacket()
        {
            var guid = Encoding.ASCII.GetBytes(UDPVoicePacket.LobbyMusicGuid);
            return new UDPVoicePacket
            {
                GuidBytes = guid,
                OriginalClientGuidBytes = guid,
                Frequencies = new[] { 248220000.0 },
                Modulations = new[] { (byte) RadioInformation.Modulation.AM }
            };
        }
    }
}
