using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>
    /// The "Text size" setting: one scale for everything inside the main window and the windows opened from it, so
    /// text, controls and spacing grow together and nothing clips that doesn't at the normal size. The radio overlay
    /// is left out: it scales with its own window.
    /// </summary>
    internal static class UiScale
    {
        public static readonly int[] Percents = { 100, 110, 120, 130 };

        // The scale a window's content has now; 0 until Apply is first called for it.
        private static readonly DependencyProperty AppliedProperty = DependencyProperty.RegisterAttached(
            "Applied", typeof(double), typeof(UiScale), new PropertyMetadata(0d));

        static UiScale()
        {
            // A tooltip opens in a window of its own, so it doesn't take its owner's scale by itself.
            EventManager.RegisterClassHandler(typeof(ToolTip), ToolTip.OpenedEvent, new RoutedEventHandler((sender, e) =>
            {
                var tip = (ToolTip) sender;
                var owner = tip.PlacementTarget == null ? null : Window.GetWindow(tip.PlacementTarget);
                double scale = owner == null ? 1 : Of(owner);
                tip.LayoutTransform = scale == 1 ? Transform.Identity : new ScaleTransform(scale, scale);
            }));
        }

        public static double Current =>
            Parse(GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.TextSize).RawValue);

        /// <summary>The stored percentage as a scale from 1 to 1.3, on a step the picker offers; anything unreadable is 1.</summary>
        public static double Parse(string stored)
        {
            double percent;
            if (!double.TryParse(stored, NumberStyles.Float, CultureInfo.InvariantCulture, out percent) || double.IsNaN(percent))
            {
                return 1;
            }

            percent = Math.Max(Percents.First(), Math.Min(Percents.Last(), percent));
            return Math.Round(percent / 10) * 10 / 100;
        }

        /// <summary>The scale of a window's content: 1 for a window Apply was never called for.</summary>
        public static double Of(Window window)
        {
            double applied = (double) window.GetValue(AppliedProperty);
            return applied > 0 ? applied : 1;
        }

        /// <summary>
        /// Scales a window's content to the setting, and its minimum size with it. Call it after InitializeComponent
        /// and before a saved size is restored; calling it again after the setting changes scales by the difference.
        /// </summary>
        /// <param name="resize">Also scale the window's own width and height.</param>
        public static void Apply(Window window, bool resize = true)
        {
            double scale = Current, ratio = scale / Of(window);
            window.SetValue(AppliedProperty, scale);
            if (window.Content is FrameworkElement content)
            {
                content.LayoutTransform = scale == 1 ? Transform.Identity : new ScaleTransform(scale, scale);
            }

            // Read first: a minimum raised above an open window's size resizes it on the spot.
            double width = window.Width, height = window.Height;
            window.MinWidth *= ratio;
            window.MinHeight *= ratio;
            // NaN: sized by its content, which is scaled already.
            if (resize && !double.IsNaN(width))
            {
                window.Width = width * ratio;
            }

            if (resize && !double.IsNaN(height))
            {
                window.Height = height * ratio;
            }
        }

        /// <summary>After the setting changed: the open windows that were scaled before.</summary>
        public static void ApplyToOpenWindows()
        {
            foreach (var window in Application.Current.Windows.OfType<Window>()
                         .Where(w => (double) w.GetValue(AppliedProperty) > 0 && Of(w) != Current))
            {
                Apply(window);
            }
        }
    }
}
