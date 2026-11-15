using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    /// <summary>Renders the LED VU meter at 100% scaling, the most common setting, and reads its pixels.</summary>
    [TestClass]
    public class LedVuMeterRenderTests
    {
        [TestMethod]
        public void ScaleLabelsSitOnADarkPlateEvenOnAWhitePanel()
        {
            // Between the -10 and -6 labels, below the segment window: the light themes' panel would show here.
            Color between = OnSta(() => Render(-100, Colors.White, 78, 24));

            Assert.IsTrue(between.R < 80 && between.G < 80 && between.B < 80, "label strip is " + between);
        }

        [TestMethod]
        public void LitSegmentsStaySeparateAtOneHundredPercent()
        {
            Color segment = OnSta(() => Render(18, Colors.White, 8, 7));
            Color gap = OnSta(() => Render(18, Colors.White, 12, 7));

            Assert.IsTrue(gap.G < segment.G * 0.6, "segment " + segment + ", gap " + gap);
        }

        private static Color Render(double value, Color panel, int x, int y)
        {
            var meter = new LedVuMeter { Value = value };
            var host = new Border { Background = new SolidColorBrush(panel), Child = meter };
            host.Measure(new Size(150, 28));
            host.Arrange(new Rect(0, 0, 150, 28));
            host.UpdateLayout();

            var bitmap = new RenderTargetBitmap(150, 28, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
            return Color.FromArgb(pixel[3], pixel[2], pixel[1], pixel[0]);
        }

        private static T OnSta<T>(Func<T> work)
        {
            T result = default(T);
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA); // WPF only runs on single-threaded apartment threads
            thread.Start();
            thread.Join();

            if (error != null)
            {
                throw new AssertFailedException("Rendering failed: " + error);
            }

            return result;
        }
    }
}
