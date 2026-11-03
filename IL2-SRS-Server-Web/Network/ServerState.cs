using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Hosting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    /// <summary>
    /// Owns the SRS TCP sync server, the UDP voice router, the connected client list and the ban list.
    /// </summary>
    public sealed class ServerState
    {
        private const string DEFAULT_CLIENT_EXPORT_FILE = "clients-list.json";

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<string, SRClient> _connectedClients =
            new ConcurrentDictionary<string, SRClient>();

        private readonly object _lifecycleLock = new object();
        private readonly ServerEvents _events;
        private readonly BanStore _banStore;

        private volatile UDPVoiceRouter _voiceRouter;
        private volatile ServerSync _serverSync;
        private CancellationTokenSource _exportCancellation;

        public ServerState(ServerEvents events, BanStore banStore)
        {
            _events = events;
            _banStore = banStore;
        }

        public bool IsRunning { get; private set; }
        public DateTime? StartedAtUtc { get; private set; }
        public int Port { get; private set; }

        public bool IsTcpListenerRunning => _serverSync?.IsStarted == true;
        public bool IsUdpListenerRunning => _voiceRouter?.IsListening == true;

        public IReadOnlyList<SRClient> GetClients()
        {
            return _connectedClients.Values.Where(client => client != null).ToList();
        }

        /// <summary>Starts the TCP and UDP listeners. Throws if either cannot bind its port.</summary>
        public void Start()
        {
            lock (_lifecycleLock)
            {
                if (IsRunning)
                {
                    return;
                }

                var port = ServerSettingsStore.Instance.GetServerPort();

                var voiceRouter = new UDPVoiceRouter(_connectedClients, _events, port);
                var serverSync = new ServerSync(_connectedClients, _banStore, _events, port);
                try
                {
                    voiceRouter.Start();
                    serverSync.StartListening();
                }
                catch (Exception ex)
                {
                    voiceRouter.RequestStop();
                    if (serverSync.IsStarted)
                    {
                        serverSync.RequestStop();
                    }

                    throw new ServerStartupException(
                        $"Unable to start the SRS server on port {port} (TCP and UDP). Is the port already in use? Choose a free port with SRS_SERVER_PORT (or Settings > Server Port).",
                        ex);
                }

                _voiceRouter = voiceRouter;
                _serverSync = serverSync;
                Port = port;
                StartExport();

                IsRunning = true;
                StartedAtUtc = DateTime.UtcNow;
                Logger.Info($"IL2-SRS Server {ReleaseMetadata.Version} listening on port {port} (TCP and UDP)");
            }

            _events.PublishRunningStateChanged();
            _events.PublishClientsChanged();
        }

        public void Stop()
        {
            lock (_lifecycleLock)
            {
                if (!IsRunning)
                {
                    return;
                }

                _exportCancellation?.Cancel();
                _exportCancellation = null;
                _serverSync?.RequestStop();
                _serverSync = null;
                _voiceRouter?.RequestStop();
                _voiceRouter = null;
                _connectedClients.Clear();

                IsRunning = false;
                StartedAtUtc = null;
                Logger.Info("IL2-SRS Server stopped");
            }

            _events.PublishRunningStateChanged();
            _events.PublishClientsChanged();
        }

        public bool KickClient(string clientGuid)
        {
            if (!TryGetSession(clientGuid, out var client, out var session))
            {
                return false;
            }

            try
            {
                session.Disconnect();
                return true;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Error kicking client");
                return false;
            }
        }

        /// <summary>
        /// Bans a connected client's IP address and disconnects every client from it, not only this one;
        /// returns the ban, or null.
        /// </summary>
        public BanRecord BanClient(string clientGuid, string reason, string actor, DateTime? expiresAtUtc)
        {
            if (!TryGetSession(clientGuid, out var client, out var session) ||
                !(session.Socket?.RemoteEndPoint is IPEndPoint remoteEndPoint))
            {
                return null;
            }

            var ban = _banStore.Add(remoteEndPoint.Address, client.Name, reason, actor, expiresAtUtc);
            DisconnectAddress(remoteEndPoint.Address);
            return ban;
        }

        /// <summary>Disconnects every connected client from a banned address.</summary>
        public int DisconnectAddress(IPAddress address)
        {
            address = BanStore.Normalize(address);
            var disconnected = 0;
            foreach (var client in GetClients())
            {
                if (client.ClientSession is SRSClientSession session &&
                    session.Socket?.RemoteEndPoint is IPEndPoint remoteEndPoint &&
                    BanStore.Normalize(remoteEndPoint.Address).Equals(address) &&
                    KickClient(client.ClientGuid))
                {
                    disconnected++;
                }
            }

            return disconnected;
        }

        public string GetClientAddress(string clientGuid)
        {
            return TryGetSession(clientGuid, out _, out var session) &&
                   session.Socket?.RemoteEndPoint is IPEndPoint remoteEndPoint
                ? BanStore.Normalize(remoteEndPoint.Address).ToString()
                : null;
        }

        public bool SetClientMuted(string clientGuid, bool muted)
        {
            if (clientGuid == null || !_connectedClients.TryGetValue(clientGuid, out var client) || client == null)
            {
                return false;
            }

            client.Muted = muted;
            _events.PublishClientsChanged();
            return true;
        }

        private bool TryGetSession(string clientGuid, out SRClient client, out SRSClientSession session)
        {
            session = null;
            client = null;
            if (clientGuid == null || !_connectedClients.TryGetValue(clientGuid, out client) || client == null)
            {
                return false;
            }

            session = client.ClientSession as SRSClientSession;
            return session != null;
        }

        private void StartExport()
        {
            var exportFilePath = ResolveExportFilePath();
            var cancellation = new CancellationTokenSource();
            _exportCancellation = cancellation;

            Task.Run(async () =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    if (ServerSettingsStore.Instance.GetGeneralSetting(ServerSettingsKeys.CLIENT_EXPORT_ENABLED)
                        .BoolValue)
                    {
                        var data = new ClientListExport
                            { Clients = _connectedClients.Values, ServerVersion = ReleaseMetadata.Version };
                        var json = JsonConvert.SerializeObject(data,
                            new JsonSerializerSettings { ContractResolver = new JsonNetworkPropertiesResolver() }) + "\n";
                        try
                        {
                            File.WriteAllText(exportFilePath, json);
                        }
                        catch (IOException e)
                        {
                            Logger.Error(e);
                        }
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });
        }

        private static string ResolveExportFilePath()
        {
            var defaultPath = ServerPaths.Resolve(DEFAULT_CLIENT_EXPORT_FILE);
            var configuredPath = ServerSettingsStore.Instance
                .GetServerSetting(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH).StringValue;

            string exportFilePath;
            try
            {
                exportFilePath = string.IsNullOrWhiteSpace(configuredPath)
                    ? defaultPath
                    : ServerPaths.Resolve(configuredPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Invalid client export path \"{configuredPath}\", falling back to default path");
                return defaultPath;
            }

            var exportFileDirectory = Path.GetDirectoryName(exportFilePath);
            if (!string.IsNullOrEmpty(exportFileDirectory) && !Directory.Exists(exportFileDirectory))
            {
                Logger.Warn($"Client export directory \"{exportFileDirectory}\" does not exist, trying to create it");

                try
                {
                    Directory.CreateDirectory(exportFileDirectory);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex,
                        $"Failed to create client export directory \"{exportFileDirectory}\", falling back to default path");
                    return defaultPath;
                }
            }

            return exportFilePath;
        }
    }
}
