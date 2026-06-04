using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class RciDisplayStateTests
    {
        [TestMethod]
        public void FriendlyRciFormatsStatusAndRcoCallsign()
        {
            var displayState = RciDisplayState.Create(RciStatus.FriendlyOnly, "DEFCON");

            Assert.AreEqual("RCI - Friendly only", displayState.StatusText);
            Assert.AreEqual("RCO On Duty : DEFCON", displayState.RcoOnDutyText);
            Assert.IsTrue(displayState.HasRcoOnDuty);
        }

        [TestMethod]
        public void EnemyRciDoesNotShowFriendlyRcoCallsign()
        {
            var displayState = RciDisplayState.Create(RciStatus.EnemyOnly, string.Empty);

            Assert.AreEqual("RCI - Opposition only", displayState.StatusText);
            Assert.AreEqual(string.Empty, displayState.RcoOnDutyText);
            Assert.IsFalse(displayState.HasRcoOnDuty);
        }

        [TestMethod]
        public void AssignedCallsignAndRequestPromptUseSharedText()
        {
            Assert.AreEqual("Callsign: CHECKMATE", RciDisplayState.FormatAssignedCallsign("CHECKMATE"));
            Assert.AreEqual("Request callsign CHN 2", RciDisplayState.GetRequestCallsignText());
        }
    }
}
