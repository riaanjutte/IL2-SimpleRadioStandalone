using Ciribob.IL2.SimpleRadio.Standalone.Server.UI.MainWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class ServerWindowLayoutTests
    {
        [TestMethod]
        public void OpeningClientAdminAddsPanelWidthWithoutChangingHeight()
        {
            Assert.AreEqual(380.0, ServerWindowLayout.GetWindowWidth(false));
            Assert.AreEqual(1060.0, ServerWindowLayout.GetWindowWidth(true));
            Assert.AreEqual(0.0, ServerWindowLayout.GetClientAdminPanelWidth(false));
            Assert.AreEqual(680.0, ServerWindowLayout.GetClientAdminPanelWidth(true));
            Assert.AreEqual(575.0, ServerWindowLayout.WindowHeight);
        }
    }
}
