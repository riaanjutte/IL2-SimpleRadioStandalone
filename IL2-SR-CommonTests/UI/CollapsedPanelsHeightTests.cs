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
            var size = CollapsedPanelsHeight.Resize(650, 0, 250, true, 560);

            Assert.AreEqual(650, size.NormalHeight);
            Assert.AreEqual(310, size.MinHeight);
            Assert.AreEqual(400, size.Height);
        }

        [TestMethod]
        public void NothingCollapsedKeepsTheNormalSize()
        {
            var size = CollapsedPanelsHeight.Resize(650, 0, 0, true, 560);

            Assert.AreEqual(560, size.MinHeight);
            Assert.AreEqual(650, size.Height);
        }

        [TestMethod]
        public void OtherTabsGetTheNormalHeightBack()
        {
            var size = CollapsedPanelsHeight.Resize(400, 250, 250, false, 560);

            Assert.AreEqual(650, size.NormalHeight);
            Assert.AreEqual(560, size.MinHeight);
            Assert.AreEqual(650, size.Height);
        }

        [TestMethod]
        public void SwitchingTabsAndBackDoesNotCreep()
        {
            var general = CollapsedPanelsHeight.Resize(650, 0, 250, true, 560);
            var settings = CollapsedPanelsHeight.Resize(general.Height, general.NormalHeight - general.Height, 250, false, 560);
            var generalAgain = CollapsedPanelsHeight.Resize(settings.Height, settings.NormalHeight - settings.Height, 250, true, 560);

            Assert.AreEqual(general.Height, generalAgain.Height);
            Assert.AreEqual(650, generalAgain.NormalHeight);
        }

        [TestMethod]
        public void DraggingWhileCollapsedSetsTheNormalHeightToTheNewHeightPlusTheCollapsedHeight()
        {
            var size = CollapsedPanelsHeight.Resize(500, 250, 250, true, 560);

            Assert.AreEqual(750, size.NormalHeight);
            Assert.AreEqual(500, size.Height);
        }

        [TestMethod]
        public void RemeasuringWithASmallerCollapsedHeightGrowsTheGeneralTab()
        {
            // The RCI panel went away behind a collapsed section, so less height is hidden.
            var size = CollapsedPanelsHeight.Resize(400, 250, 190, true, 560);

            Assert.AreEqual(650, size.NormalHeight);
            Assert.AreEqual(460, size.Height);
        }

        [TestMethod]
        public void GeneralTabHeightStopsAtTheMinimum()
        {
            var size = CollapsedPanelsHeight.Resize(300, 0, 250, true, 280);

            Assert.AreEqual(30, size.MinHeight);
            Assert.AreEqual(50, size.Height);
            Assert.AreEqual(30, CollapsedPanelsHeight.Resize(300, 0, 280, true, 310).Height);
        }
    }
}
