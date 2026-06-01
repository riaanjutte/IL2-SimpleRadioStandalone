using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow
{
    public static class SpeakerNameScrollLayout
    {
        public const double RightPadding = 14.0;

        public static SpeakerNameScrollMetrics Calculate(double textWidth, double viewportWidth)
        {
            if (textWidth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textWidth));
            }

            if (viewportWidth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(viewportWidth));
            }

            if (textWidth <= viewportWidth)
            {
                return new SpeakerNameScrollMetrics(false, double.NaN, 0);
            }

            var scrollingWidth = textWidth + RightPadding;
            return new SpeakerNameScrollMetrics(true, scrollingWidth, scrollingWidth - viewportWidth);
        }
    }

    public struct SpeakerNameScrollMetrics
    {
        public SpeakerNameScrollMetrics(bool shouldScroll, double scrollingWidth, double travelDistance)
        {
            ShouldScroll = shouldScroll;
            ScrollingWidth = scrollingWidth;
            TravelDistance = travelDistance;
        }

        public bool ShouldScroll { get; }
        public double ScrollingWidth { get; }
        public double TravelDistance { get; }
    }
}
