using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    /// <summary>
    /// Procedurally weathers equipment plates: hairline scratches, scuff smudges,
    /// and corner grime rendered once per plate into a bitmap overlay. Each plate
    /// instance rolls its own wear pattern so panels don't look factory-fresh.
    /// </summary>
    public static class PanelWeathering
    {
        private static readonly Random Rng = new Random();

        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(PanelWeathering),
                new PropertyMetadata(false, OnEnabledChanged));

        public static bool GetEnabled(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnabledProperty);
        }

        public static void SetEnabled(DependencyObject obj, bool value)
        {
            obj.SetValue(EnabledProperty, value);
        }

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is Rectangle rect) || !(bool)e.NewValue)
            {
                return;
            }

            rect.IsHitTestVisible = false;
            rect.Loaded += (s, _) => Apply(rect);
            rect.SizeChanged += (s, _) => Apply(rect);
        }

        private static void Apply(Rectangle rect)
        {
            var w = (int)rect.ActualWidth;
            var h = (int)rect.ActualHeight;
            if (w < 20 || h < 20)
            {
                return;
            }

            rect.Fill = new ImageBrush(RenderWear(w, h)) { Stretch = Stretch.None, TileMode = TileMode.None };
        }

        private static BitmapSource RenderWear(int w, int h)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                DrawGrime(dc, w, h);
                DrawWornPaint(dc, w, h);
                DrawScratches(dc, w, h);
                DrawScuffs(dc, w, h);
            }

            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawScratches(DrawingContext dc, int w, int h)
        {
            var count = 6 + (w * h) / 11000;
            for (var i = 0; i < count; i++)
            {
                var x = Rng.NextDouble() * w;
                var y = Rng.NextDouble() * h;
                var length = 6 + Rng.NextDouble() * 55;
                var angle = Rng.NextDouble() * Math.PI;
                var end = new Point(x + Math.Cos(angle) * length, y + Math.Sin(angle) * length);

                // light catch on one side of the gouge, shadow on the other
                var alpha = (byte)(12 + Rng.Next(24));
                var colour = Rng.NextDouble() < 0.5
                    ? Color.FromArgb(alpha, 0xFF, 0xF6, 0xDC)
                    : Color.FromArgb(alpha, 0x00, 0x00, 0x00);
                var pen = new Pen(new SolidColorBrush(colour), 0.7 + Rng.NextDouble() * 0.7);
                pen.Freeze();
                dc.DrawLine(pen, new Point(x, y), end);
            }
        }

        private static void DrawScuffs(DrawingContext dc, int w, int h)
        {
            // scuffs as clusters of short parallel hairlines (gradients band visibly
            // at low alpha, so build smudges from strokes instead)
            var count = 3 + (w * h) / 47000;
            for (var i = 0; i < count; i++)
            {
                var cx = Rng.NextDouble() * w;
                var cy = Rng.NextDouble() * h;
                var angle = Rng.NextDouble() * Math.PI;
                var light = Rng.NextDouble() < 0.4;
                var strokes = 3 + Rng.Next(4);
                for (var s = 0; s < strokes; s++)
                {
                    var offset = (s - strokes / 2.0) * (1.2 + Rng.NextDouble());
                    var ox = cx + Math.Cos(angle + Math.PI / 2) * offset;
                    var oy = cy + Math.Sin(angle + Math.PI / 2) * offset;
                    var len = 4 + Rng.NextDouble() * 12;
                    var alpha = (byte)(6 + Rng.Next(12));
                    var colour = light
                        ? Color.FromArgb(alpha, 0xFF, 0xF6, 0xDC)
                        : Color.FromArgb(alpha, 0x00, 0x00, 0x00);
                    var pen = new Pen(new SolidColorBrush(colour), 0.6 + Rng.NextDouble() * 0.5);
                    pen.Freeze();
                    dc.DrawLine(pen,
                        new Point(ox - Math.Cos(angle) * len / 2, oy - Math.Sin(angle) * len / 2),
                        new Point(ox + Math.Cos(angle) * len / 2, oy + Math.Sin(angle) * len / 2));
                }
            }
        }

        private static void DrawWornPaint(DrawingContext dc, int w, int h)
        {
            // paint rubbed through to bare metal where hands and gear touch:
            // corner chips around the screws, flecks and rub patches along edges
            var metal = new[]
            {
                Color.FromRgb(0x9C, 0x8C, 0x6A),
                Color.FromRgb(0xA9, 0x9B, 0x7C),
                Color.FromRgb(0xB5, 0xA3, 0x7F)
            };

            // corner chip clusters
            var corners = new[]
            {
                new Point(0, 0), new Point(w, 0), new Point(0, h), new Point(w, h)
            };
            foreach (var corner in corners)
            {
                var chips = 6 + Rng.Next(10);
                for (var i = 0; i < chips; i++)
                {
                    var dx = Rng.NextDouble() * Rng.NextDouble() * 26;
                    var dy = Rng.NextDouble() * Rng.NextDouble() * 26;
                    var x = corner.X == 0 ? dx : corner.X - dx;
                    var y = corner.Y == 0 ? dy : corner.Y - dy;
                    var alpha = (byte)(14 + Rng.Next(30));
                    var brush = new SolidColorBrush(
                        Color.FromArgb(alpha, metal[Rng.Next(metal.Length)].R, metal[Rng.Next(metal.Length)].G, metal[Rng.Next(metal.Length)].B));
                    brush.Freeze();
                    var r = 0.5 + Rng.NextDouble() * 1.6;
                    dc.DrawEllipse(brush, null, new Point(x, y), r, r * (0.6 + Rng.NextDouble() * 0.4));
                }
            }

            // edge rub patches: runs of short strokes parallel to the nearest edge
            var patches = 3 + Rng.Next(4) + (w + h) / 400;
            for (var i = 0; i < patches; i++)
            {
                var edge = Rng.Next(4); // 0=top 1=bottom 2=left 3=right
                var along = Rng.NextDouble();
                var inset = Rng.NextDouble() * Rng.NextDouble() * 8;
                double cx, cy, angle;
                switch (edge)
                {
                    case 0: cx = along * w; cy = inset; angle = 0; break;
                    case 1: cx = along * w; cy = h - inset; angle = 0; break;
                    case 2: cx = inset; cy = along * h; angle = Math.PI / 2; break;
                    default: cx = w - inset; cy = along * h; angle = Math.PI / 2; break;
                }

                var strokes = 4 + Rng.Next(6);
                var tone = metal[Rng.Next(metal.Length)];
                for (var s = 0; s < strokes; s++)
                {
                    var offset = (s - strokes / 2.0) * (0.9 + Rng.NextDouble() * 0.6);
                    var ox = cx + Math.Cos(angle + Math.PI / 2) * offset + (Rng.NextDouble() - 0.5) * 4;
                    var oy = cy + Math.Sin(angle + Math.PI / 2) * offset + (Rng.NextDouble() - 0.5) * 2;
                    var len = 5 + Rng.NextDouble() * 16;
                    var alpha = (byte)(8 + Rng.Next(14));
                    var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, tone.R, tone.G, tone.B)),
                        0.6 + Rng.NextDouble() * 0.6);
                    pen.Freeze();
                    dc.DrawLine(pen,
                        new Point(ox - Math.Cos(angle) * len / 2, oy - Math.Sin(angle) * len / 2),
                        new Point(ox + Math.Cos(angle) * len / 2, oy + Math.Sin(angle) * len / 2));
                }
            }

            // stray chips scattered across the face
            var flecks = 8 + (w * h) / 30000;
            for (var i = 0; i < flecks; i++)
            {
                var x = Rng.NextDouble() * w;
                var y = Rng.NextDouble() * h;
                var alpha = (byte)(10 + Rng.Next(22));
                var tone = metal[Rng.Next(metal.Length)];
                var brush = new SolidColorBrush(Color.FromArgb(alpha, tone.R, tone.G, tone.B));
                brush.Freeze();
                var r = 0.4 + Rng.NextDouble() * 1.1;
                dc.DrawEllipse(brush, null, new Point(x, y), r, r * (0.5 + Rng.NextDouble() * 0.5));
            }
        }

        private static void DrawGrime(DrawingContext dc, int w, int h)
        {
            // dirt collects along edges and in corners: dense speckle, no gradients
            var count = 120 + (w * h) / 600;
            for (var i = 0; i < count; i++)
            {
                // bias positions towards the plate border
                var onVerticalEdge = Rng.NextDouble() < 0.5;
                var x = onVerticalEdge
                    ? (Rng.NextDouble() < 0.5 ? Rng.NextDouble() * Rng.NextDouble() * w * 0.18 : w - Rng.NextDouble() * Rng.NextDouble() * w * 0.18)
                    : Rng.NextDouble() * w;
                var y = onVerticalEdge
                    ? Rng.NextDouble() * h
                    : (Rng.NextDouble() < 0.5 ? Rng.NextDouble() * Rng.NextDouble() * h * 0.25 : h - Rng.NextDouble() * Rng.NextDouble() * h * 0.25);

                var radius = 0.4 + Rng.NextDouble() * 1.1;
                var alpha = (byte)(12 + Rng.Next(26));
                var brush = new SolidColorBrush(Color.FromArgb(alpha, 0x0A, 0x07, 0x03));
                brush.Freeze();
                dc.DrawEllipse(brush, null, new Point(x, y), radius, radius);
            }
        }
    }
}
