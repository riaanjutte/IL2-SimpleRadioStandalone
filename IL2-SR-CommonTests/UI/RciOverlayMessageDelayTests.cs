using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class RciOverlayMessageDelayTests
    {
        [TestMethod]
        public void HidesRciCallsignsUntilDelayExpires()
        {
            var now = new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);
            var delay = new RciOverlayMessageDelay(TimeSpan.FromSeconds(60), () => now);

            Assert.AreEqual(string.Empty, delay.GetVisibleCallsigns("DEFCON"));

            now = now.AddSeconds(59);
            Assert.AreEqual(string.Empty, delay.GetVisibleCallsigns("DEFCON"));

            now = now.AddSeconds(1);
            Assert.AreEqual("DEFCON", delay.GetVisibleCallsigns("DEFCON"));
        }

        [TestMethod]
        public void RestartsDelayWhenRciCallsignsChange()
        {
            var now = new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);
            var delay = new RciOverlayMessageDelay(TimeSpan.FromSeconds(60), () => now);

            delay.GetVisibleCallsigns("DEFCON");
            now = now.AddSeconds(60);
            Assert.AreEqual("DEFCON", delay.GetVisibleCallsigns("DEFCON"));

            Assert.AreEqual(string.Empty, delay.GetVisibleCallsigns("BROADWAY"));

            now = now.AddSeconds(60);
            Assert.AreEqual("BROADWAY", delay.GetVisibleCallsigns("BROADWAY"));
        }

        [TestMethod]
        public void MissingRciCallsignsResetDelay()
        {
            var now = new DateTime(2026, 6, 22, 12, 0, 0, DateTimeKind.Utc);
            var delay = new RciOverlayMessageDelay(TimeSpan.FromSeconds(60), () => now);

            delay.GetVisibleCallsigns("DEFCON");
            now = now.AddSeconds(60);
            Assert.AreEqual("DEFCON", delay.GetVisibleCallsigns("DEFCON"));

            Assert.AreEqual(string.Empty, delay.GetVisibleCallsigns(string.Empty));
            Assert.AreEqual(string.Empty, delay.GetVisibleCallsigns("DEFCON"));
        }
    }
}
