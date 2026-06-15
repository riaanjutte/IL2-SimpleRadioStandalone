using System;
using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Input
{
    [TestClass]
    public class InputPollingCycleCacheTests
    {
        [TestMethod]
        public void GetOrPollCachesDeviceStateForOnePollingCycle()
        {
            var instanceGuid = Guid.NewGuid();
            var deviceStateCache = new Dictionary<Guid, object>();
            var pollCount = 0;

            var firstState = InputPollingCycleCache.GetOrPoll(instanceGuid, deviceStateCache, () =>
            {
                pollCount++;
                return "first poll";
            });

            var secondState = InputPollingCycleCache.GetOrPoll(instanceGuid, deviceStateCache, () =>
            {
                pollCount++;
                return "second poll";
            });

            Assert.AreEqual("first poll", firstState);
            Assert.AreEqual("first poll", secondState);
            Assert.AreEqual(1, pollCount, "Bindings that share the same DirectInput device should poll it once per cycle.");
        }

        [TestMethod]
        public void GetOrPollDoesNotCacheAcrossDifferentDevices()
        {
            var deviceStateCache = new Dictionary<Guid, object>();
            var pollCount = 0;

            InputPollingCycleCache.GetOrPoll(Guid.NewGuid(), deviceStateCache, () =>
            {
                pollCount++;
                return "first device";
            });

            InputPollingCycleCache.GetOrPoll(Guid.NewGuid(), deviceStateCache, () =>
            {
                pollCount++;
                return "second device";
            });

            Assert.AreEqual(2, pollCount);
        }

        [TestMethod]
        public void GetOrPollWithoutCacheKeepsLegacyPollingBehavior()
        {
            var instanceGuid = Guid.NewGuid();
            var pollCount = 0;

            InputPollingCycleCache.GetOrPoll(instanceGuid, null, () =>
            {
                pollCount++;
                return "first poll";
            });

            InputPollingCycleCache.GetOrPoll(instanceGuid, null, () =>
            {
                pollCount++;
                return "second poll";
            });

            Assert.AreEqual(2, pollCount);
        }

        [TestMethod]
        public void RecoveryScanIsSkippedWhenBoundDeviceIsAvailable()
        {
            Assert.IsFalse(
                InputPollingCycleCache.ShouldAttemptRecoveryScan(true),
                "Normal polling of an available DirectInput device must not scan every recovered HID candidate.");
            Assert.IsTrue(
                InputPollingCycleCache.ShouldAttemptRecoveryScan(false),
                "Recovery scans should still run when the original bound device is missing.");
        }
    }
}
