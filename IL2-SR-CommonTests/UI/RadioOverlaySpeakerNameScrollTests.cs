using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class RadioOverlaySpeakerNameScrollTests
    {
        [TestMethod]
        public void CagSonoftheMorningCanScrollFullyIntoTheChannelBox()
        {
            const string speakerName = "CAG_SonoftheMorning";
            const double channelBoxWidth = 78.0;

            var measuredTextWidth = MeasureOverlayChannelTextWidth(speakerName);
            var scrollMetrics = SpeakerNameScrollLayout.Calculate(measuredTextWidth, channelBoxWidth);

            Assert.IsTrue(
                measuredTextWidth > channelBoxWidth,
                "The regression name must be wider than the overlay channel box to exercise scrolling.");
            Assert.IsTrue(scrollMetrics.ShouldScroll);
            Assert.AreEqual(measuredTextWidth + SpeakerNameScrollLayout.RightPadding, scrollMetrics.ScrollingWidth, 0.01);

            var lastGlyphRightEdgeAtFullTravel = measuredTextWidth - scrollMetrics.TravelDistance;
            Assert.AreEqual(
                channelBoxWidth - SpeakerNameScrollLayout.RightPadding,
                lastGlyphRightEdgeAtFullTravel,
                0.01,
                "The final scroll position should include trailing padding so the last letters are not clipped.");
        }

        [TestMethod]
        public void AssignedCallsignScrollPauseHappensAtResetPosition()
        {
            const double travelDistance = 100.0;
            const double travelMilliseconds = 1000.0;
            const double initialPauseMilliseconds = 700.0;
            const double resetPauseMilliseconds = 15000.0;

            Assert.AreEqual(
                0,
                SpeakerNameScrollLayout.CalculateResetPauseScrollOffset(
                    initialPauseMilliseconds / 2,
                    travelDistance,
                    travelMilliseconds,
                    initialPauseMilliseconds,
                    resetPauseMilliseconds),
                0.01);

            Assert.AreEqual(
                -50,
                SpeakerNameScrollLayout.CalculateResetPauseScrollOffset(
                    initialPauseMilliseconds + 500.0,
                    travelDistance,
                    travelMilliseconds,
                    initialPauseMilliseconds,
                    resetPauseMilliseconds),
                0.01);

            Assert.AreEqual(
                0,
                SpeakerNameScrollLayout.CalculateResetPauseScrollOffset(
                    initialPauseMilliseconds + travelMilliseconds + 1000.0,
                    travelDistance,
                    travelMilliseconds,
                    initialPauseMilliseconds,
                    resetPauseMilliseconds),
                0.01,
                "After the first scroll completes, the text should reset to the start and pause there.");

            Assert.AreEqual(
                -50,
                SpeakerNameScrollLayout.CalculateResetPauseScrollOffset(
                    initialPauseMilliseconds + travelMilliseconds + resetPauseMilliseconds + 500.0,
                    travelDistance,
                    travelMilliseconds,
                    initialPauseMilliseconds,
                    resetPauseMilliseconds),
                0.01);
        }

        private static double MeasureOverlayChannelTextWidth(string text)
        {
            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Courier New"),
                8.0,
                Brushes.White,
                1.0);

            return formattedText.WidthIncludingTrailingWhitespace;
        }
    }
}
