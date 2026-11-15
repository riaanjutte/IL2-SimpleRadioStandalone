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

        [TestMethod]
        public void EmptyHeightIsTheSpaceBelowTheTabsContent()
        {
            // A tab whose panels moved elsewhere, or whose rows are collapsed, gives it back, less one pixel of slack.
            Assert.AreEqual(249, CollapsedPanelsHeight.EmptyHeight(500, 250));
        }

        [TestMethod]
        public void EmptyHeightLeavesSlackForPixelRounding()
        {
            // The window height is rounded to whole screen pixels; a fraction short of the content shows a scroll bar.
            Assert.AreEqual(248, CollapsedPanelsHeight.EmptyHeight(500, 250.4));
        }

        [TestMethod]
        public void EmptyHeightIsZeroWhenTheContentFillsOrOverflows()
        {
            Assert.AreEqual(0, CollapsedPanelsHeight.EmptyHeight(500, 500));
            Assert.AreEqual(0, CollapsedPanelsHeight.EmptyHeight(500, 499.5));
            Assert.AreEqual(0, CollapsedPanelsHeight.EmptyHeight(500, 900));
        }

        [TestMethod]
        public void APanelFillingItsTabGivesBackEverythingBelowItsHeader()
        {
            // Expanded, Controls or Help fills the viewport exactly; collapsed it is just its header bar.
            Assert.AreEqual(439, CollapsedPanelsHeight.EmptyHeight(500, 60));
            var size = CollapsedPanelsHeight.Resize(650, 0, 439, true, 560);
            Assert.AreEqual(211, size.Height);
            Assert.AreEqual(650, CollapsedPanelsHeight.Resize(size.Height, 650 - size.Height, 0, true, 560).Height);
        }

        [TestMethod]
        public void AFittedWindowCanStillBeDraggedSmaller()
        {
            // General fitted from 700 to 401: the minimum drops by the same amount, so the player can drag below it.
            var size = CollapsedPanelsHeight.Resize(700, 0, CollapsedPanelsHeight.EmptyHeight(560, 260), true, 560);
            Assert.AreEqual(401, size.Height);
            Assert.AreEqual(261, size.MinHeight);
        }
    }
}
