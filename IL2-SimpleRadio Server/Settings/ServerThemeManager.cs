using System;
using System.Windows;
using System.Windows.Media;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Settings
{
    public static class ServerThemeManager
    {
        public const string WhiteTheme = "White";
        public const string DarkTheme = "Dark";

        public static string CurrentTheme { get; private set; } = WhiteTheme;

        public static void Apply(string theme)
        {
            CurrentTheme = string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase)
                ? DarkTheme
                : WhiteTheme;

            var dark = CurrentTheme == DarkTheme;
            SetBrush("ServerWindowBackgroundBrush", dark ? "#FF202428" : "#FFF4F4F4");
            SetBrush("ServerSurfaceBrush", dark ? "#FF292E33" : "#FFFFFFFF");
            SetBrush("ServerControlBackgroundBrush", dark ? "#FF343A40" : "#FFE6E6E6");
            SetBrush("ServerInputBackgroundBrush", dark ? "#FF252A2F" : "#FFFFFFFF");
            SetBrush("ServerTextBrush", dark ? "#FFF2F2F2" : "#FF171717");
            SetBrush("ServerMutedTextBrush", dark ? "#FFB8C0C8" : "#FF4F565C");
            SetBrush("ServerBorderBrush", dark ? "#FF626A73" : "#FF8A8A8A");
            SetBrush("ServerAccentBrush", dark ? "#FF5FA8DC" : "#FF2D6FA3");
            SetBrush("ServerSelectionBrush", dark ? "#FF365A73" : "#FFD7EAF8");
            SetBrush("ServerHealthyBrush", dark ? "#FF67D17A" : "#FF247A37");
            SetBrush("ServerErrorBrush", dark ? "#FFFF7B72" : "#FFB3261E");
            SetBrush("ServerStartActionBrush", "#FF247A37");
            SetBrush("ServerStopActionBrush", "#FFB3261E");
        }

        private static void SetBrush(string key, string color)
        {
            if (Application.Current == null)
            {
                return;
            }

            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            Application.Current.Resources[key] = brush;
        }
    }
}
