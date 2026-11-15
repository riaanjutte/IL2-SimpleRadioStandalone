using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class LedVuMeterScaleTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void LitSegmentsCountsThresholdsAtOrBelowTheLevel()
        {
            Assert.AreEqual(0, LedVuMeterScale.LitSegments(-100));
            Assert.AreEqual(1, LedVuMeterScale.LitSegments(-40));
            Assert.AreEqual(1, LedVuMeterScale.LitSegments(-39.9));
            Assert.AreEqual(10, LedVuMeterScale.LitSegments(-6));
            Assert.AreEqual(13, LedVuMeterScale.LitSegments(0));
            Assert.AreEqual(16, LedVuMeterScale.LitSegments(6));
            Assert.AreEqual(16, LedVuMeterScale.LitSegments(18));
        }

        [TestMethod]
        public void AnInvalidLevelLightsNothing()
        {
            Assert.AreEqual(0, LedVuMeterScale.LitSegments(double.NaN));
        }

        [TestMethod]
        public void SegmentsAreGreenThenAmberThenRed()
        {
            Assert.AreEqual(LedColour.Green, LedVuMeterScale.ColourOf(9));
            Assert.AreEqual(LedColour.Amber, LedVuMeterScale.ColourOf(10));
            Assert.AreEqual(LedColour.Amber, LedVuMeterScale.ColourOf(12));
            Assert.AreEqual(LedColour.Red, LedVuMeterScale.ColourOf(13));
        }

        [TestMethod]
        public void PeakIsHeldForOneSecondThenFollowsTheLevel()
        {
            var peak = new LedPeakHold();

            Assert.AreEqual(12, peak.Update(12, T0));
            Assert.AreEqual(12, peak.Update(5, T0.AddSeconds(0.5)));
            Assert.AreEqual(5, peak.Update(5, T0.AddSeconds(1)));
            Assert.AreEqual(14, peak.Update(14, T0.AddSeconds(1.2)));
        }

        [TestMethod]
        public void ReleaseAtSaysWhenAHeldPeakGoesOut()
        {
            var peak = new LedPeakHold();

            peak.Update(14, T0);
            Assert.IsNull(peak.ReleaseAt);

            peak.Update(3, T0.AddSeconds(0.2));
            Assert.AreEqual(T0.AddSeconds(1), peak.ReleaseAt);

            peak.Update(3, T0.AddSeconds(1));
            Assert.IsNull(peak.ReleaseAt);
        }
    }
}
