using System;
using System.Text.Json.Nodes;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Events;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    /// <summary>
    /// In-process notifications between the SRS server and the admin UI
    /// (replaces the Caliburn.Micro event aggregator used by the WPF server).
    /// Handlers run on the publishing thread and must not block.
    /// </summary>
    public sealed class ServerEvents : IEventBus, IServerStatusEvents
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        /// <summary>A client connected, disconnected, or changed name, coalition or callsign.</summary>
        public event Action ClientsChanged;

        /// <summary>The server was started or stopped.</summary>
        public event Action RunningStateChanged;

        /// <summary>Settings synced to clients changed; connected clients should be sent the new settings.</summary>
        public event Action SettingsChanged;

        /// <summary>The global lobby frequency list changed (comma-separated MHz values).</summary>
        public event Action<string> GlobalLobbyFrequenciesChanged;

        /// <summary>A client completed the SRS handshake: client, remote IP.</summary>
        public event Action<SRClient, string> ClientConnected;

        /// <summary>A known client disconnected.</summary>
        public event Action<SRClient> ClientDisconnected;

        /// <summary>A connection was refused: remote IP, reason.</summary>
        public event Action<string, string> ConnectionRejected;

        /// <summary>
        /// A connected client sent new metadata or radio state: client (already updated), previous name,
        /// previous coalition, previous game state (null when unchanged or not sent before).
        /// </summary>
        public event Action<SRClient, string, int, PlayerGameState> ClientUpdated;

        /// <summary>A typed event for the event log and plugins (see <see cref="IEventBus"/>).</summary>
        public event Action<SrsEvent> EventPublished;

        public void PublishClientsChanged() => Raise(ClientsChanged);

        public void PublishRunningStateChanged() => Raise(RunningStateChanged);

        public void PublishSettingsChanged() => Raise(SettingsChanged);

        public void PublishGlobalLobbyFrequenciesChanged(string frequencies) =>
            Raise(GlobalLobbyFrequenciesChanged, handler => handler(frequencies));

        public void PublishClientConnected(SRClient client, string ipAddress) =>
            Raise(ClientConnected, handler => handler(client, ipAddress));

        public void PublishClientDisconnected(SRClient client) =>
            Raise(ClientDisconnected, handler => handler(client));

        public void PublishConnectionRejected(string ipAddress, string reason) =>
            Raise(ConnectionRejected, handler => handler(ipAddress, reason));

        public void PublishClientUpdated(SRClient client, string previousName, int previousCoalition,
            PlayerGameState previousGameState) =>
            Raise(ClientUpdated, handler => handler(client, previousName, previousCoalition, previousGameState));

        public void Publish(SrsEvent srsEvent) => Raise(EventPublished, handler => handler(srsEvent));

        public void Publish(SrsEventType type, string actor, string message, JsonObject payload = null) =>
            Publish(SrsEvent.Create(type, actor, message, payload));

        private static void Raise(Action handlers) => Raise(handlers, handler => handler());

        private static void Raise<T>(T handlers, Action<T> invoke) where T : Delegate
        {
            if (handlers == null)
            {
                return;
            }

            foreach (T handler in handlers.GetInvocationList())
            {
                try
                {
                    invoke(handler);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Server event handler failed");
                }
            }
        }
    }
}
