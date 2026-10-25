using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class ConnectionRecoveryTests
    {
        [TestMethod]
        public void ALostConnectionIsRetried()
        {
            Assert.IsTrue(ConnectionRecovery.ShouldReconnect(connectionLost: true, connectionError: false, reconnecting: false));
        }

        [TestMethod]
        public void AFailedReconnectAttemptIsRetried()
        {
            Assert.IsTrue(ConnectionRecovery.ShouldReconnect(connectionLost: false, connectionError: true, reconnecting: true));
        }

        [TestMethod]
        public void AKickOrServerShutdownIsNotRetried()
        {
            // The server closed the connection cleanly: no network error.
            Assert.IsFalse(ConnectionRecovery.ShouldReconnect(connectionLost: false, connectionError: false, reconnecting: false));
            Assert.IsFalse(ConnectionRecovery.ShouldReconnect(connectionLost: false, connectionError: false, reconnecting: true),
                "A reconnect that reaches the server but is refused (e.g. version mismatch) must not loop.");
        }

        [TestMethod]
        public void AFailedFirstConnectIsNotRetried()
        {
            // A wrong address or a server that is down when the user presses Connect shows the error as before.
            Assert.IsFalse(ConnectionRecovery.ShouldReconnect(connectionLost: false, connectionError: true, reconnecting: false));
        }

        [TestMethod]
        public void BacksOffTo30Seconds()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(2), ConnectionRecovery.DelayBefore(1));
            Assert.AreEqual(TimeSpan.FromSeconds(5), ConnectionRecovery.DelayBefore(2));
            Assert.AreEqual(TimeSpan.FromSeconds(10), ConnectionRecovery.DelayBefore(3));
            Assert.AreEqual(TimeSpan.FromSeconds(30), ConnectionRecovery.DelayBefore(4));
            Assert.AreEqual(TimeSpan.FromSeconds(30), ConnectionRecovery.DelayBefore(50));
            Assert.AreEqual(TimeSpan.FromSeconds(2), ConnectionRecovery.DelayBefore(0));
        }
    }
}
