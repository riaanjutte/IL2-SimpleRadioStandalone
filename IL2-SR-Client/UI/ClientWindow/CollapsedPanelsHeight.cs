using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    /// <summary>Window heights when General-tab sections are collapsed: only the General tab gets shorter.</summary>
    internal static class CollapsedPanelsHeight
    {
        /// <param name="windowHeight">The window's height now.</param>
        /// <param name="heightReduction">How much shorter than the player's normal height the window is now.</param>
        /// <param name="collapsedHeight">The height the collapsed sections take off the General tab.</param>
        /// <param name="normalMinHeight">The window's minimum height with nothing collapsed.</param>
        public static (double NormalHeight, double MinHeight, double Height) Resize(
            double windowHeight, double heightReduction, double collapsedHeight, bool onGeneralTab, double normalMinHeight)
        {
            double normalHeight = windowHeight + heightReduction;
            if (!onGeneralTab)
            {
                return (normalHeight, normalMinHeight, normalHeight);
            }

            double minHeight = Math.Max(0, normalMinHeight - collapsedHeight);
            return (normalHeight, minHeight, Math.Max(minHeight, normalHeight - collapsedHeight));
        }

        /// <summary>
        /// Empty space below a tab's content at the player's normal height, which the window gives back. A tab whose
        /// content fills or overflows the viewport gives nothing, so it keeps the normal height.
        /// </summary>
        public static double EmptyHeight(double viewportAtNormalHeight, double content)
        {
            // One whole pixel of slack: the window height is rounded to screen pixels, and a fraction short shows a scroll bar.
            return Math.Max(0, Math.Floor(viewportAtNormalHeight - content) - 1);
        }

        /// <summary>
        /// The same for content scaled by the Text size setting. The viewport and the content are measured in the
        /// content's own units; the height reduction and the result are in the window's.
        /// </summary>
        public static double EmptyHeight(double viewport, double heightReduction, double content, double scale)
        {
            return EmptyHeight(viewport + heightReduction / scale, content) * scale;
        }
    }
}
