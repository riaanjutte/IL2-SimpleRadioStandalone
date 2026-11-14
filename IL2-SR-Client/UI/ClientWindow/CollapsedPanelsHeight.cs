using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    /// <summary>Window heights when General-tab sections are collapsed: only the General tab gets shorter.</summary>
    internal static class CollapsedPanelsHeight
    {
        public static double WindowHeight(double normalHeight, double collapsedHeight, bool onGeneralTab, double minHeight)
        {
            return onGeneralTab ? Math.Max(minHeight, normalHeight - collapsedHeight) : normalHeight;
        }
    }
}
