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
    }
}
