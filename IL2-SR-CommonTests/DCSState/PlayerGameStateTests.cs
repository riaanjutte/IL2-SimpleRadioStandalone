using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests
{
    [TestClass]
    public class PlayerGameStateTests
    {
        [TestMethod]
        public void IntercomOwnerCanHearCrewMember()
        {
            var receiver = CreateIntercomState(12345, -1);

            var receivingRadio = receiver.CanHearTransmission(
                10000,
                RadioInformation.Modulation.INTERCOM,
                54321,
                12345,
                new List<int>(),
                out var receivingState);

            Assert.IsNotNull(receivingRadio);
            Assert.IsNotNull(receivingState);
            Assert.AreEqual(0, receivingState.ReceivedOn);
        }

        [TestMethod]
        public void IntercomCrewMemberCanHearOwner()
        {
            var receiver = CreateIntercomState(54321, 12345);

            var receivingRadio = receiver.CanHearTransmission(
                10000,
                RadioInformation.Modulation.INTERCOM,
                12345,
                -1,
                new List<int>(),
                out var receivingState);

            Assert.IsNotNull(receivingRadio);
            Assert.IsNotNull(receivingState);
            Assert.AreEqual(0, receivingState.ReceivedOn);
        }

        [TestMethod]
        public void IntercomCrewMembersInSameVehicleCanHearEachOther()
        {
            var receiver = CreateIntercomState(34251, 12345);

            var receivingRadio = receiver.CanHearTransmission(
                10000,
                RadioInformation.Modulation.INTERCOM,
                54321,
                12345,
                new List<int>(),
                out var receivingState);

            Assert.IsNotNull(receivingRadio);
            Assert.IsNotNull(receivingState);
            Assert.AreEqual(0, receivingState.ReceivedOn);
        }

        [TestMethod]
        public void IntercomDifferentVehiclesCannotHearEachOther()
        {
            var receiver = CreateIntercomState(777, 111);

            var receivingRadio = receiver.CanHearTransmission(
                10000,
                RadioInformation.Modulation.INTERCOM,
                888,
                222,
                new List<int>(),
                out var receivingState);

            Assert.IsNull(receivingRadio);
            Assert.IsNull(receivingState);
        }

        [TestMethod]
        public void NewPlayerGameStateDefaultsRadio1ToChannel1AndRadio2ToChannel2()
        {
            var state = new PlayerGameState();

            Assert.AreEqual(1, state.radios[1].channel);
            Assert.AreEqual(PlayerGameState.START_FREQ + PlayerGameState.CHANNEL_OFFSET, state.radios[1].freq);
            Assert.AreEqual(2, state.radios[2].channel);
            Assert.AreEqual(PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * 2), state.radios[2].freq);
        }

        private static PlayerGameState CreateIntercomState(int unitId, int vehicleId)
        {
            return new PlayerGameState
            {
                unitId = unitId,
                vehicleId = vehicleId
            };
        }
    }
}
