using System;
using System.Windows;
using System.Windows.Media;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    /// <summary>
    /// Gives each panel screw slot a random rotation so plates don't look
    /// machine-aligned. Template values are baked once per app, so per-instance
    /// randomness needs this attached property instead of a static RotateTransform.
    /// </summary>
    public static class ScrewSlotRandomizer
    {
        private static readonly Random Rng = new Random();

        public static readonly DependencyProperty RandomAngleProperty =
            DependencyProperty.RegisterAttached(
                "RandomAngle",
                typeof(bool),
                typeof(ScrewSlotRandomizer),
                new PropertyMetadata(false, OnRandomAngleChanged));

        public static bool GetRandomAngle(DependencyObject obj)
        {
            return (bool)obj.GetValue(RandomAngleProperty);
        }

        public static void SetRandomAngle(DependencyObject obj, bool value)
        {
            obj.SetValue(RandomAngleProperty, value);
        }

        private static void OnRandomAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is UIElement element && (bool)e.NewValue)
            {
                element.RenderTransform = new RotateTransform(Rng.NextDouble() * 180.0);
            }
        }
    }
}
