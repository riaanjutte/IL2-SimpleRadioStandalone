using System;
using System.Linq;
using System.Windows;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.ValueConverters;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Themes
{
    /// <summary>
    /// The control styles merged into each window: MilitaryControls.xaml for the military themes, nothing for
    /// Classic so the MahApps.Metro styles from App.xaml show through (the 1.0.2.0 look). Chosen when the window
    /// is created, so switching to or from Classic takes effect in windows opened afterwards.
    /// </summary>
    public class ThemeControlsDictionary : ResourceDictionary
    {
        private const string MilitaryControlsSource =
            "pack://application:,,,/IL2-SR-ClientRadio;component/Themes/MilitaryControls.xaml";

        public ThemeControlsDictionary()
        {
            if (UiTheme.IsClassicSelected())
            {
                // Window title templates use this key; MilitaryControls.xaml normally provides it.
                this["MilUpperConverter"] = new StringToUpperConverter();
                return;
            }

            Source = new Uri(MilitaryControlsSource, UriKind.Absolute);
        }
    }

    public static class UiTheme
    {
        public const string Classic = "Classic";

        public static bool IsClassic(string theme)
        {
            return string.Equals(Normalize(theme), Normalize(Classic), StringComparison.Ordinal);
        }

        public static bool IsClassicSelected()
        {
            try
            {
                return IsClassic(GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.Theme).RawValue);
            }
            catch (Exception)
            {
                // Settings unavailable (e.g. the XAML designer): use the default military styles.
                return false;
            }
        }

        // Letters and digits only, like App.SelectedPalette, so a quoted value in global.cfg picks the same theme.
        private static string Normalize(string theme)
        {
            return new string((theme ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }
    }
}
