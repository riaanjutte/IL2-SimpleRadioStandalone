using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class IL2RadioSyncHandlerTests
    {
        [TestMethod]
        public void ApplyControlDataClearsCoalitionAndVehicleForSpectator()
        {
            var state = new PlayerGameState
            {
                coalition = 2,
                vehicleId = 1234
            };

            var changed = IL2RadioSyncHandler.ApplyControlData(state, 1234, 0);

            Assert.IsTrue(changed);
            Assert.AreEqual(0, state.coalition);
            Assert.AreEqual(-1, state.vehicleId);
        }

        [TestMethod]
        public void ApplyControlDataNormalizesWorldWarOneCoalitions()
        {
            var state = new PlayerGameState();

            var changed = IL2RadioSyncHandler.ApplyControlData(state, 4321, 4);

            Assert.IsTrue(changed);
            Assert.AreEqual(2, state.coalition);
            Assert.AreEqual(4321, state.vehicleId);
        }

        [TestMethod]
        public void ApplyControlDataReportsNoChangeForRepeatedState()
        {
            var state = new PlayerGameState
            {
                coalition = 1,
                vehicleId = -1
            };

            var changed = IL2RadioSyncHandler.ApplyControlData(state, -1, 1);

            Assert.IsFalse(changed);
        }
    }
}
