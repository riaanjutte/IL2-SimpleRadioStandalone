using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    internal static class PilotRosterScreenBounds
    {
        internal static Rect SelectWorkArea(Rect windowBounds, IEnumerable<Rect> workAreas, Rect fallback)
        {
            var available = (workAreas ?? Enumerable.Empty<Rect>())
                .Where(IsUsable)
                .ToList();
            if (available.Count == 0)
            {
                return fallback;
            }

            var anchor = new Point(
                windowBounds.Left + Math.Min(Math.Max(0, windowBounds.Width), 80) / 2.0,
                windowBounds.Top + Math.Min(Math.Max(0, windowBounds.Height), 80) / 2.0);
            var containing = available
                .Where(area => area.Contains(anchor))
                .Select(area => (Rect?)area)
                .FirstOrDefault();
            if (containing.HasValue)
            {
                return containing.Value;
            }

            var intersecting = available
                .Select(area => new
                {
                    Area = area,
                    Intersection = Rect.Intersect(area, windowBounds)
                })
                .Where(candidate => !candidate.Intersection.IsEmpty)
                .OrderByDescending(candidate => candidate.Intersection.Width * candidate.Intersection.Height)
                .FirstOrDefault();

            return intersecting?.Area ?? fallback;
        }

        internal static Rect ConstrainToWorkArea(
            Rect windowBounds,
            Rect workArea,
            double margin,
            double minimumWidth,
            double minimumHeight)
        {
            var maximumWidth = Math.Max(minimumWidth, workArea.Width - margin * 2);
            var maximumHeight = Math.Max(minimumHeight, workArea.Height - margin * 2);
            var width = Math.Min(Math.Max(minimumWidth, windowBounds.Width), maximumWidth);
            var height = Math.Min(Math.Max(minimumHeight, windowBounds.Height), maximumHeight);
            var left = Math.Max(workArea.Left + margin,
                Math.Min(windowBounds.Left, workArea.Right - width - margin));
            var top = Math.Max(workArea.Top + margin,
                Math.Min(windowBounds.Top, workArea.Bottom - height - margin));

            return new Rect(left, top, width, height);
        }

        private static bool IsUsable(Rect area)
        {
            return !area.IsEmpty &&
                   IsFinite(area.Left) &&
                   IsFinite(area.Top) &&
                   IsFinite(area.Width) &&
                   IsFinite(area.Height) &&
                   area.Width > 0 &&
                   area.Height > 0;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
