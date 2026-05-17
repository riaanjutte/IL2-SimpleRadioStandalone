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
