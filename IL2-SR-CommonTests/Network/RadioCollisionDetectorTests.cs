using System;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class RadioCollisionDetectorTests
    {
        private static readonly byte[] Am = { (byte)RadioInformation.Modulation.AM };

        [TestMethod]
        public void OverlappingTransmittersOnSameChannelCollide()
        {
            var detector = new RadioCollisionDetector();
            var now = DateTime.UtcNow;

            Assert.IsFalse(detector.RegisterPacket("one", 1, new[] { 125000000.0 }, Am, true, now));
            Assert.IsTrue(detector.RegisterPacket("two", 1, new[] { 125000100.0 }, Am, true,
                now.AddMilliseconds(20)));
            Assert.IsTrue(detector.RegisterPacket("one", 1, new[] { 125000000.0 }, Am, true,
                now.AddMilliseconds(40)));
        }

        [TestMethod]
        public void DifferentChannelsAndExpiredTransmittersDoNotCollide()
        {
            var detector = new RadioCollisionDetector(TimeSpan.FromMilliseconds(120));
            var now = DateTime.UtcNow;

            Assert.IsFalse(detector.RegisterPacket("one", 1, new[] { 125000000.0 }, Am, true, now));
            Assert.IsFalse(detector.RegisterPacket("two", 1, new[] { 126000000.0 }, Am, true,
                now.AddMilliseconds(20)));
            Assert.IsFalse(detector.RegisterPacket("three", 1, new[] { 125000000.0 }, Am, true,
                now.AddMilliseconds(150)));
        }

        [TestMethod]
        public void CoalitionSecuritySeparatesCollisionDomains()
        {
            var detector = new RadioCollisionDetector();
            var now = DateTime.UtcNow;

            Assert.IsFalse(detector.RegisterPacket("one", 1, new[] { 125000000.0 }, Am, true, now));
            Assert.IsFalse(detector.RegisterPacket("two", 2, new[] { 125000000.0 }, Am, true,
                now.AddMilliseconds(20)));
            Assert.IsTrue(detector.RegisterPacket("three", 2, new[] { 125000000.0 }, Am, false,
                now.AddMilliseconds(40)));
        }

        [TestMethod]
        public void IntercomDoesNotUseRadioCollisionEffects()
        {
            var detector = new RadioCollisionDetector();
            var now = DateTime.UtcNow;
            var intercom = new[] { (byte)RadioInformation.Modulation.INTERCOM };

            Assert.IsFalse(detector.RegisterPacket("one", 1, new[] { 100.0 }, intercom, false, now));
            Assert.IsFalse(detector.RegisterPacket("two", 1, new[] { 100.0 }, intercom, false,
                now.AddMilliseconds(20)));
        }
    }
}
