using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Network
{
    /// <summary>When and how soon to reconnect after the server connection ends without the user disconnecting.</summary>
    internal static class ConnectionRecovery
    {
        private static readonly int[] DelaysSeconds = { 2, 5, 10, 30 };

        /// <summary>
        /// Reconnect after a connection fails with a network error (timeout, reset), or when a reconnect attempt could
        /// not reach the server. A clean close by the server (kick, ban, shutdown) or a refused version is not retried.
        /// </summary>
        public static bool ShouldReconnect(bool connectionLost, bool connectionError, bool reconnecting)
        {
            return connectionLost || (reconnecting && connectionError);
        }

        /// <summary>Delay before reconnect attempt number <paramref name="attempt"/> (1 = the first): 2, 5, 10, then every 30 s.</summary>
        public static TimeSpan DelayBefore(int attempt)
        {
            var index = Math.Min(Math.Max(attempt, 1), DelaysSeconds.Length) - 1;
            return TimeSpan.FromSeconds(DelaysSeconds[index]);
        }
    }
}
