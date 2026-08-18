using Ciribob.IL2.SimpleRadio.Standalone.Overlay;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class RadioOverlayChannelLimitTests
    {
        [TestMethod]
        public void ConnectedServerLimitControlsAvailableOverlayChannels()
        {
            Assert.AreEqual(5, RadioControlGroup.GetOverlayChannelLimit(true, "5"));
        }

        [TestMethod]
        public void OverlayLimitDoesNotExceedNumberOfVisibleButtons()
        {
            Assert.AreEqual(12, RadioControlGroup.GetOverlayChannelLimit(true, "25"));
        }

        [TestMethod]
        public void DisconnectedOverlayKeepsAllChannelButtonsAvailable()
        {
            Assert.AreEqual(12, RadioControlGroup.GetOverlayChannelLimit(false, "5"));
        }

        [TestMethod]
        public void InvalidServerLimitKeepsAllChannelButtonsAvailable()
        {
            Assert.AreEqual(12, RadioControlGroup.GetOverlayChannelLimit(true, "invalid"));
        }

        [TestMethod]
        public void ConnectionScrewRequestsConnectionWhileDisconnected()
        {
            Assert.IsTrue(RadioOverlayWindow.ShouldRequestConnection(false));
        }

        [TestMethod]
        public void ConnectionScrewDoesNothingWhileConnected()
        {
            Assert.IsFalse(RadioOverlayWindow.ShouldRequestConnection(true));
        }

    }
}
