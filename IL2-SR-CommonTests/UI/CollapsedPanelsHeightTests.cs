using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class CollapsedPanelsHeightTests
    {
        [TestMethod]
        public void GeneralTabIsShorterByTheCollapsedHeight()
        {
            Assert.AreEqual(400, CollapsedPanelsHeight.WindowHeight(650, 250, true, 200));
            Assert.AreEqual(650, CollapsedPanelsHeight.WindowHeight(650, 0, true, 200));
        }

        [TestMethod]
        public void OtherTabsUseTheNormalHeight()
        {
            Assert.AreEqual(650, CollapsedPanelsHeight.WindowHeight(650, 250, false, 200));
        }

        [TestMethod]
        public void GeneralTabHeightStopsAtTheMinimum()
        {
            Assert.AreEqual(200, CollapsedPanelsHeight.WindowHeight(300, 250, true, 200));
        }

        [TestMethod]
        public void NormalHeightAddsTheCollapsedHeightBackOnTheGeneralTab()
        {
            Assert.AreEqual(650, CollapsedPanelsHeight.NormalHeight(400, 250, true));
            Assert.AreEqual(650, CollapsedPanelsHeight.NormalHeight(650, 250, false));
        }
    }
}
