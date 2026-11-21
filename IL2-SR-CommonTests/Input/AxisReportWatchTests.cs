using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Input
{
    [TestClass]
    public class AxisReportWatchTests
    {
        private static readonly int[] Centred = { 32768, 32768, 32768, 32768, 32768, 32768, 32768, 32768 };

        [TestMethod]
        public void AxesAreIgnoredUntilTheDeviceHasReported()
        {
            var watch = new AxisReportWatch<object>();
            var throttle = new object();

            Assert.IsFalse(watch.HasReported(throttle, Centred));
            Assert.IsFalse(watch.HasReported(throttle, (int[])Centred.Clone()));

            var real = new[] { 0, 0, 32768, 32768, 32768, 32768, 32768, 32768 };
            Assert.IsTrue(watch.HasReported(throttle, real));
            Assert.IsTrue(watch.HasReported(throttle, real));
        }

        [TestMethod]
        public void ARediscoveredDeviceWaitsForItsOwnFirstReport()
        {
            var watch = new AxisReportWatch<object>();
            var original = new object();
            watch.HasReported(original, Centred);
            Assert.IsTrue(watch.HasReported(original, new[] { 0, 0, 0, 0, 0, 0, 0, 0 }));

            var rediscovered = new object();
            Assert.IsFalse(watch.HasReported(rediscovered, Centred));
        }
    }
}
