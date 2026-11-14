using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    /// <summary>
    /// A 16-segment LED bargraph VU meter with peak hold, drawn like real LEDs behind a glass window.
    /// Value is the level in dB, the same value the analogue dial shows.
    /// </summary>
    public sealed class LedVuMeter : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(LedVuMeter),
            new FrameworkPropertyMetadata(-100.0, FrameworkPropertyMetadataOptions.AffectsRender, OnValueChanged));

        private const double MeterWidth = 150;
        private const double MeterHeight = 28;
        private const double WindowHeight = 15;
        private const double Padding = 3;
        private const double Gap = 1.5;
        private static readonly int[] LabelledSegments = { 0, 4, 7, 9, 12, 15 };
        private static readonly string[] Labels = { "-40", "-20", "-10", "-6", "0", "+6" };

        private static readonly Brush WindowBrush = Frozen(new SolidColorBrush(Color.FromRgb(0x0D, 0x0E, 0x0C)));
        private static readonly Pen WindowEdgePen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0x05, 0x05, 0x04))), 1));
        private static readonly Pen WindowHighlightPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x90, 0x6A, 0x6D, 0x63))), 1));
        private static readonly Brush GlassBrush = Frozen(new LinearGradientBrush(
            Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), new Point(0, 0), new Point(0, 0.5)));
        private static readonly Brush LabelBrush = Frozen(new SolidColorBrush(Color.FromRgb(0xD9, 0xD2, 0xB6)));
        private static readonly Typeface LabelTypeface = new Typeface("Consolas");

        private static readonly LedLook[] Looks =
        {
            new LedLook(Color.FromRgb(0x12, 0x30, 0x1A), Color.FromRgb(0x4D, 0xFF, 0x6A), Color.FromRgb(0x13, 0xA8, 0x3A)),
            new LedLook(Color.FromRgb(0x3A, 0x2E, 0x0C), Color.FromRgb(0xFF, 0xD2, 0x3D), Color.FromRgb(0xC9, 0x8A, 0x00)),
            new LedLook(Color.FromRgb(0x3D, 0x10, 0x10), Color.FromRgb(0xFF, 0x4A, 0x3D), Color.FromRgb(0xB3, 0x15, 0x0E))
        };

        private readonly LedPeakHold _peakHold = new LedPeakHold();
        private DispatcherTimer _releaseTimer;
        private int _lit;
        private int _peak;

        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(MeterWidth, MeterHeight);
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var meter = (LedVuMeter)d;
            meter._lit = LedVuMeterScale.LitSegments((double)e.NewValue);
            meter.UpdatePeak();
        }

        private void UpdatePeak()
        {
            _peak = _peakHold.Update(_lit, DateTime.UtcNow);

            // The level can stop changing (preview stopped), so a held peak needs its own wake-up to go out.
            DateTime? releaseAt = _peakHold.ReleaseAt;
            if (releaseAt == null)
            {
                _releaseTimer?.Stop();
                return;
            }

            if (_releaseTimer == null)
            {
                _releaseTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher);
                _releaseTimer.Tick += (sender, args) =>
                {
                    _releaseTimer.Stop();
                    UpdatePeak();
                    InvalidateVisual();
                };
            }

            _releaseTimer.Stop();
            _releaseTimer.Interval = TimeSpan.FromTicks(Math.Max(TimeSpan.TicksPerMillisecond, (releaseAt.Value - DateTime.UtcNow).Ticks));
            _releaseTimer.Start();
        }

        protected override void OnRender(DrawingContext dc)
        {
            var window = new Rect(0.5, 0.5, MeterWidth - 1, WindowHeight - 1);
            dc.DrawRoundedRectangle(WindowBrush, WindowEdgePen, window, 2, 2);
            dc.DrawLine(WindowHighlightPen, new Point(1, WindowHeight + 0.5), new Point(MeterWidth - 1, WindowHeight + 0.5));

            int count = LedVuMeterScale.Thresholds.Length;
            double segmentWidth = (MeterWidth - 2 * Padding - (count - 1) * Gap) / count;
            double segmentHeight = WindowHeight - 2 * Padding;

            // Glows first, so a lit segment's glow sits behind its neighbours like light through a diffuser.
            for (int i = 0; i < _lit; i++)
            {
                LedLook look = Looks[(int)LedVuMeterScale.ColourOf(i)];
                Rect segment = SegmentRect(i, segmentWidth, segmentHeight);
                dc.DrawRoundedRectangle(look.OuterGlow, null, Inflate(segment, 2.5), 3, 3);
                dc.DrawRoundedRectangle(look.InnerGlow, null, Inflate(segment, 1), 2, 2);
            }

            for (int i = 0; i < count; i++)
            {
                LedLook look = Looks[(int)LedVuMeterScale.ColourOf(i)];
                bool lit = i < _lit || i == _peak - 1;
                dc.DrawRoundedRectangle(lit ? look.On : look.Off, null, SegmentRect(i, segmentWidth, segmentHeight), 0.8, 0.8);
            }

            dc.DrawRoundedRectangle(GlassBrush, null, window, 2, 2);

            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            for (int i = 0; i < LabelledSegments.Length; i++)
            {
                var text = new FormattedText(Labels[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    LabelTypeface, 7, LabelBrush, pixelsPerDip);
                Rect under = SegmentRect(LabelledSegments[i], segmentWidth, segmentHeight);
                double x = Math.Max(0, Math.Min(MeterWidth - text.Width, under.Left + (under.Width - text.Width) / 2));
                dc.DrawText(text, new Point(x, WindowHeight + 3));
            }
        }

        private static Rect SegmentRect(int index, double segmentWidth, double segmentHeight)
        {
            return new Rect(Padding + index * (segmentWidth + Gap), Padding, segmentWidth, segmentHeight);
        }

        private static Rect Inflate(Rect rect, double by)
        {
            rect.Inflate(by, by);
            return rect;
        }

        private static T Frozen<T>(T freezable) where T : Freezable
        {
            freezable.Freeze();
            return freezable;
        }

        private sealed class LedLook
        {
            public LedLook(Color off, Color on, Color onDark)
            {
                Off = Frozen(new LinearGradientBrush(off, Color.FromRgb(0x0C, 0x0C, 0x0A), 90));
                On = Frozen(new LinearGradientBrush(new GradientStopCollection
                {
                    new GradientStop(Colors.White, 0),
                    new GradientStop(on, 0.3),
                    new GradientStop(onDark, 1)
                }, 90));
                InnerGlow = Frozen(new SolidColorBrush(Color.FromArgb(0x70, on.R, on.G, on.B)));
                OuterGlow = Frozen(new SolidColorBrush(Color.FromArgb(0x28, on.R, on.G, on.B)));
            }

            public Brush Off { get; }
            public Brush On { get; }
            public Brush InnerGlow { get; }
            public Brush OuterGlow { get; }
        }
    }
}
