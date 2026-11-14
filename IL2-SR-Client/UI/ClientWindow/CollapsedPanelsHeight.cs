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
        /// Empty space at the bottom of a tab that collapsing sections frees. A tab whose content already
        /// overflows the viewport frees nothing, so the window never shrinks for it.
        /// </summary>
        public static double FreedHeight(double viewportAtNormalHeight, double expandedContent, double collapsedContent)
        {
            return Math.Max(0, viewportAtNormalHeight - collapsedContent) - Math.Max(0, viewportAtNormalHeight - expandedContent);
        }
    }
}
