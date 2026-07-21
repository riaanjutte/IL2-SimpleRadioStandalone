using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class PilotRosterAccessPolicyTests
    {
        [TestMethod]
        public void ConnectedServerProvidingRosterDataMakesRosterAvailable()
        {
            Assert.IsTrue(PilotRosterAccessPolicy.IsAvailable(true, true));
        }

        [TestMethod]
        public void RosterRequiresConnectionAndServerData()
        {
            Assert.IsFalse(PilotRosterAccessPolicy.IsAvailable(false, true));
            Assert.IsFalse(PilotRosterAccessPolicy.IsAvailable(true, false));
        }

        [TestMethod]
        public void AutoStartRequiresConnectionEnabledSettingAndFreshConnection()
        {
            Assert.IsTrue(PilotRosterAccessPolicy.ShouldAutoStart(true, true, true, false));
            Assert.IsFalse(PilotRosterAccessPolicy.ShouldAutoStart(false, true, true, false));
            Assert.IsFalse(PilotRosterAccessPolicy.ShouldAutoStart(true, false, true, false));
            Assert.IsFalse(PilotRosterAccessPolicy.ShouldAutoStart(true, true, false, false));
            Assert.IsFalse(PilotRosterAccessPolicy.ShouldAutoStart(true, true, true, true));
        }

        [TestMethod]
        public void UnavailableMessageDirectsUsersToServerOwners()
        {
            Assert.IsFalse(PilotRosterAccessPolicy.UnavailableSummary.Contains("Combat Box"));
            StringAssert.Contains(PilotRosterAccessPolicy.UnavailableSummary, "servers that provide Pilot Roster data");
            StringAssert.Contains(PilotRosterAccessPolicy.ReconnectInstruction, "contact the server owners");
        }
    }
}
