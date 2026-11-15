using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    internal enum LedColour
    {
        Green,
        Amber,
        Red
    }

    /// <summary>The LED VU meter's 16 segments: the dB level at which each lights, and its colour.</summary>
    internal static class LedVuMeterScale
    {
        public static readonly double[] Thresholds =
        {
            -40, -35, -30, -25, -20, -16, -13, -10, -8, -6, // green
            -4, -2, 0, // amber
            2, 4, 6 // red
        };

        public static int LitSegments(double db)
        {
            int lit = 0;
            while (lit < Thresholds.Length && db >= Thresholds[lit])
            {
                lit++;
            }

            return lit; // NaN compares false, so lights nothing
        }

        public static LedColour ColourOf(int segment)
        {
            return segment < 10 ? LedColour.Green : segment < 13 ? LedColour.Amber : LedColour.Red;
        }
    }

    /// <summary>Peak hold: the highest segment reached stays lit for a second, then follows the level down.</summary>
    internal sealed class LedPeakHold
    {
        public static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(1);

        private int _peak;
        private DateTime _peakSetAt;

        /// <summary>When the held peak goes out, or null when no peak is held above the level.</summary>
        public DateTime? ReleaseAt { get; private set; }

        public int Update(int lit, DateTime now)
        {
            if (lit >= _peak || now - _peakSetAt >= HoldTime)
            {
                _peak = lit;
                _peakSetAt = now;
            }

            ReleaseAt = _peak > lit ? _peakSetAt + HoldTime : (DateTime?)null;
            return _peak;
        }
    }
}
