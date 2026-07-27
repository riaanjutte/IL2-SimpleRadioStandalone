using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class SrsClientConnectionLifecycleTests
    {
        [TestMethod]
        public void TelemetryCannotSendUntilConnectionIsEstablished()
        {
            var lifecycle = new SrsClientConnectionLifecycle();

            Assert.IsFalse(lifecycle.CanSend);
            Assert.IsTrue(lifecycle.TryMarkConnected());
            Assert.IsTrue(lifecycle.CanSend);
        }

        [TestMethod]
        public void CancelledConnectionCannotBecomeConnected()
        {
            var lifecycle = new SrsClientConnectionLifecycle();

            lifecycle.MarkStopping();

            Assert.IsTrue(lifecycle.IsStopping);
            Assert.IsFalse(lifecycle.TryMarkConnected());
            Assert.IsFalse(lifecycle.CanSend);
        }

        [TestMethod]
        public void StoppingConnectedClientBlocksFurtherTelemetrySends()
        {
            var lifecycle = new SrsClientConnectionLifecycle();
            Assert.IsTrue(lifecycle.TryMarkConnected());

            lifecycle.MarkStopping();

            Assert.IsTrue(lifecycle.IsStopping);
            Assert.IsFalse(lifecycle.CanSend);
        }
    }
}
