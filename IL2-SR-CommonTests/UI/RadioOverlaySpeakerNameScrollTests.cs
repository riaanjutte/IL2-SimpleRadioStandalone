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
