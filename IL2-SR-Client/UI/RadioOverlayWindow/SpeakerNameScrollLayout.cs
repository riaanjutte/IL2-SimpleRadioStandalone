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

        public static double CalculateResetPauseScrollOffset(
            double elapsedMilliseconds,
            double travelDistance,
            double travelMilliseconds,
            double initialPauseMilliseconds,
            double resetPauseMilliseconds)
        {
            if (elapsedMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
            }

            if (travelDistance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(travelDistance));
            }

            if (travelMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(travelMilliseconds));
            }

            if (initialPauseMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialPauseMilliseconds));
            }

            if (resetPauseMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resetPauseMilliseconds));
            }

            if (elapsedMilliseconds <= initialPauseMilliseconds)
            {
                return 0;
            }

            var elapsedAfterInitialPause = elapsedMilliseconds - initialPauseMilliseconds;
            if (elapsedAfterInitialPause <= travelMilliseconds)
            {
                return -travelDistance * (elapsedAfterInitialPause / travelMilliseconds);
            }

            var repeatElapsed = (elapsedAfterInitialPause - travelMilliseconds) %
                                (resetPauseMilliseconds + travelMilliseconds);
            if (repeatElapsed <= resetPauseMilliseconds)
            {
                return 0;
            }

            return -travelDistance * ((repeatElapsed - resetPauseMilliseconds) / travelMilliseconds);
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
