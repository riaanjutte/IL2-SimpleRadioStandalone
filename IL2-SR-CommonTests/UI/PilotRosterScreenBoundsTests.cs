using System.Windows;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class PilotRosterScreenBoundsTests
    {
        [TestMethod]
        public void HeightChangeKeepsRosterInsideItsSecondMonitor()
        {
            var primary = new Rect(0, 0, 1920, 1040);
            var secondary = new Rect(1920, 0, 2560, 1400);
            var resizedRoster = new Rect(2200, 180, 560, 900);

            var selected = PilotRosterScreenBounds.SelectWorkArea(
                resizedRoster,
                new[] { primary, secondary },
                primary);
            var constrained = PilotRosterScreenBounds.ConstrainToWorkArea(
                resizedRoster,
                selected,
                10,
                460,
                90);

            Assert.AreEqual(secondary, selected);
            Assert.AreEqual(2200, constrained.Left);
            Assert.AreEqual(180, constrained.Top);
        }

        [TestMethod]
        public void OversizedRosterIsConstrainedToItsCurrentMonitorNotPrimaryMonitor()
        {
            var primary = new Rect(0, 0, 1920, 1040);
            var secondary = new Rect(-1600, 0, 1600, 900);
            var resizedRoster = new Rect(-1500, 100, 560, 1200);

            var selected = PilotRosterScreenBounds.SelectWorkArea(
                resizedRoster,
                new[] { primary, secondary },
                primary);
            var constrained = PilotRosterScreenBounds.ConstrainToWorkArea(
                resizedRoster,
                selected,
                10,
                460,
                90);

            Assert.AreEqual(secondary, selected);
            Assert.AreEqual(-1500, constrained.Left);
            Assert.AreEqual(880, constrained.Height);
            Assert.IsTrue(constrained.Right <= secondary.Right - 10);
        }

        [TestMethod]
        public void GrowingRosterNearBottomMovesUpOnlyOnItsCurrentMonitor()
        {
            var primary = new Rect(0, 0, 1920, 1040);
            var secondary = new Rect(1920, 0, 1600, 900);
            var currentRoster = new Rect(3000, 700, 500, 150);
            var selected = PilotRosterScreenBounds.SelectWorkArea(
                currentRoster,
                new[] { primary, secondary },
                primary);
            var fitted = PilotRosterScreenBounds.ConstrainToWorkArea(
                new Rect(currentRoster.Left, currentRoster.Top, currentRoster.Width, 400),
                selected,
                0,
                460,
                90);

            Assert.AreEqual(secondary, selected);
            Assert.AreEqual(currentRoster.Left, fitted.Left);
            Assert.AreEqual(currentRoster.Width, fitted.Width);
            Assert.AreEqual(500, fitted.Top);
            Assert.AreEqual(400, fitted.Height);
        }

        [TestMethod]
        public void ShrinkingRosterKeepsItsPositionOnSecondMonitor()
        {
            var primary = new Rect(0, 0, 1920, 1040);
            var secondary = new Rect(-1600, 0, 1600, 900);
            var currentRoster = new Rect(-1300, 240, 560, 600);
            var selected = PilotRosterScreenBounds.SelectWorkArea(
                currentRoster,
                new[] { primary, secondary },
                primary);
            var fitted = PilotRosterScreenBounds.ConstrainToWorkArea(
                new Rect(currentRoster.Left, currentRoster.Top, currentRoster.Width, 180),
                selected,
                0,
                460,
                90);

            Assert.AreEqual(secondary, selected);
            Assert.AreEqual(currentRoster.Left, fitted.Left);
            Assert.AreEqual(currentRoster.Top, fitted.Top);
            Assert.AreEqual(180, fitted.Height);
        }

        [TestMethod]
        public void AutoFitGrowsRosterToContentWhenNotManuallySized()
        {
            var workArea = new Rect(0, 0, 1920, 1040);
            var currentRoster = new Rect(300, 800, 560, 150);

            var fitted = PilotRosterScreenBounds.FitHeightToContent(
                currentRoster, workArea, 400, 460, 90, manuallySized: false);

            Assert.AreEqual(400, fitted.Height);
            Assert.AreEqual(640, fitted.Top);
            Assert.AreEqual(currentRoster.Left, fitted.Left);
            Assert.AreEqual(currentRoster.Width, fitted.Width);
        }

        [TestMethod]
        public void AutoFitLeavesManuallySizedRosterUntouched()
        {
            var workArea = new Rect(0, 0, 1920, 1040);
            var currentRoster = new Rect(300, 800, 560, 150);

            var grown = PilotRosterScreenBounds.FitHeightToContent(
                currentRoster, workArea, 400, 460, 90, manuallySized: true);
            var shrunk = PilotRosterScreenBounds.FitHeightToContent(
                currentRoster, workArea, 100, 460, 90, manuallySized: true);

            Assert.AreEqual(currentRoster, grown);
            Assert.AreEqual(currentRoster, shrunk);
        }
    }
}
