using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using Caliburn.Micro;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using NetCoreServer;
using NLog;
using LogManager = NLog.LogManager;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    public class ServerSync : TcpServer, IHandle<ServerSettingsChangedMessage>
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        private readonly HashSet<IPAddress> _bannedIps;

        private readonly ConcurrentDictionary<string, SRClient> _clients = new ConcurrentDictionary<string, SRClient>();
        private readonly IEventAggregator _eventAggregator;

        private readonly ServerSettingsStore _serverSettings;
        private readonly CombatBoxCallsignProvider _callsignProvider;
        private readonly object _callsignRefreshLock = new object();
        private Timer _callsignRefreshTimer;
        private NatHandler _natHandler;


        public ServerSync(ConcurrentDictionary<string, SRClient> connectedClients, HashSet<IPAddress> _bannedIps,
            IEventAggregator eventAggregator) : base(IPAddress.Any, ServerSettingsStore.Instance.GetServerPort())
        {
            _clients = connectedClients;
            this._bannedIps = _bannedIps;
            _eventAggregator = eventAggregator;
            _eventAggregator.Subscribe(this);
            _serverSettings = ServerSettingsStore.Instance;
            _callsignProvider = new CombatBoxCallsignProvider(_serverSettings);

            OptionKeepAlive = true;

            if (_serverSettings.GetServerSetting(ServerSettingsKeys.UPNP_ENABLED).BoolValue)
            {
                _natHandler = new NatHandler(_serverSettings.GetServerPort());
                _natHandler.OpenNAT();
            }
            
        }

        public void Handle(ServerSettingsChangedMessage message)
        {
            try
            {
                HandleServerSettingsMessage();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception Sending Server Settings ");
            }
        }

        protected override TcpSession CreateSession() { return new SRSClientSession(this, _clients, _bannedIps); }

        protected override void OnError(SocketError error)
        {
            Logger.Error($"TCP SERVER ERROR: {error} ");
        }

        public void StartListening()
        {
            OptionKeepAlive = true;
            try
            {
                Start();
                StartCallsignRefreshTimer();
            }
            catch(Exception ex)
            {
                try
                {
                    _natHandler?.CloseNAT();
                }
                catch
                {
                }

                Logger.Error(ex,$"Unable to start the SRS Server");

                MessageBox.Show($"Unable to start the SRS Server\n\nPort {_serverSettings.GetServerPort()} in use\n\nChange the port by editing the .cfg", "Port in use",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);
            }
        }

        public void HandleDisconnect(SRSClientSession state)
        {
            Logger.Info("Disconnecting Client: {0}", state.SRSGuid);

            if ((state != null) && (state.SRSGuid != null))
            {

                //removed
                SRClient client;
                _clients.TryRemove(state.SRSGuid, out client);

                if (client != null)
                {
                    Logger.Info("Removed Disconnected Client: {0}", state.SRSGuid);
                    client.ClientSession = null;

                   
                    HandleClientDisconnect(state, client);
                }

                try
                {
                    _eventAggregator.PublishOnUIThread(
                        new ServerStateMessage(true, new List<SRClient>(_clients.Values)));
                }
                catch (Exception ex)
                {
                    Logger.Info(ex, "Exception Publishing Client Update After Disconnect");
                }
            }
            else
            {
                Logger.Info("Removed Disconnected Unknown Client");
            }

        }



        public void HandleMessage(SRSClientSession state, NetworkMessage message)
        {
            try
            {
                Logger.Trace($"Received:  Msg - {message.MsgType} from {state.SRSGuid}");

                if (!HandleConnectedClient(state, message))
                {
                    Logger.Info($"Invalid Client - disconnecting {state.SRSGuid}");
                }

                switch (message.MsgType)
                {
                    case NetworkMessage.MessageType.PING:
                        // Do nothing for now
                        break;
                    case NetworkMessage.MessageType.UPDATE:
                        HandleClientMetaDataUpdate(state, message, true);
                        break;
                    case NetworkMessage.MessageType.RADIO_UPDATE:
                        bool showTuned = _serverSettings.GetGeneralSetting(ServerSettingsKeys.SHOW_TUNED_COUNT)
                            .BoolValue;
                        HandleClientMetaDataUpdate(state, message, !showTuned);
                        HandleClientRadioUpdate(state, message, showTuned);
                        break;
                    case NetworkMessage.MessageType.SYNC:
                        HandleRadioClientsSync(state, message);
                        break;
                    case NetworkMessage.MessageType.SERVER_SETTINGS:
                        HandleServerSettingsMessage();
                        break;
                    default:
                        Logger.Warn("Recevied unknown message type");
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Exception Handling Message " + ex.Message);
            }
        }

        private bool HandleConnectedClient(SRSClientSession state, NetworkMessage message)
        {
            var srClient = message.Client;
            if (!_clients.ContainsKey(srClient.ClientGuid))
            {
                var clientIp = (IPEndPoint)state.Socket.RemoteEndPoint;
                if (message.Version == null)
                {
                    Logger.Warn("Disconnecting Unversioned Client -  " + clientIp.Address + " " +
                                clientIp.Port);
                    state.Disconnect();
                    return false;
                }

                //check server version and type
                if (message.ServerType == null || message.ServerType != "IL2-SRS")
                {
                    Logger.Warn("Disconnecting non IL2-SRS Client -  " + clientIp.Address + " " +
                                clientIp.Port);
                    state.Disconnect();
                    return false;

                }

                var clientVersion = Version.Parse(message.Version);
                var protocolVersion = Version.Parse(UpdaterChecker.MINIMUM_PROTOCOL_VERSION);

                if (clientVersion < protocolVersion)
                {
                    Logger.Warn(
                        $"Disconnecting Unsupported  Client Version - Version {clientVersion} IP {clientIp.Address} Port {clientIp.Port}");
                    HandleVersionMismatch(state);

                    //close socket after
                    state.Disconnect();

                    return false;
                }

                srClient.ClientSession = state;
                srClient.AssignedCallsign = _callsignProvider.GetAssignedCallsign(srClient.Name, srClient.Coalition);

                // add to proper list
                _clients[srClient.ClientGuid] = srClient;

                state.SRSGuid = srClient.ClientGuid;
                Logger.Debug($"Client connected: {state.SRSGuid} ({srClient.Name}) from {clientIp}");

                _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                    new List<SRClient>(_clients.Values)));
            }

            return true;
        }

        private void HandleServerSettingsMessage()
        {
            //send server settings
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.SERVER_SETTINGS,
                ServerSettings = _serverSettings.ToDictionary()
            };

            Multicast(replyMessage.Encode());

        }

        private void HandleVersionMismatch(SRSClientSession session)
        {
            //send server settings
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.VERSION_MISMATCH,
            };
            session.Send(replyMessage.Encode());
        }

        private void HandleClientMetaDataUpdate(SRSClientSession session, NetworkMessage message, bool send)
        {
            if (_clients.ContainsKey(message.Client.ClientGuid))
            {
                var client = _clients[message.Client.ClientGuid];

                if (client != null)
                {
                    bool redrawClientAdminList = client.Name != message.Client.Name || client.Coalition != message.Client.Coalition;

                    //copy the data we need
                    client.LastUpdate = DateTime.Now.Ticks;
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.Seat = message.Client.Seat;
                    client.AssignedCallsign = _callsignProvider.GetAssignedCallsign(client.Name, client.Coalition);

                    Logger.Debug($"Client metadata update: {message.Client.ClientGuid} ({message.Client.Name}) coalition {message.Client.Coalition} callsign {client.AssignedCallsign}");

                    //send update to everyone
                    //Remove Client Radio Info
                    var replyMessage = new NetworkMessage
                    {
                        MsgType = NetworkMessage.MessageType.UPDATE,
                        Client = new SRClient
                        {
                            ClientGuid = client.ClientGuid,
                            Coalition = client.Coalition,
                            Name = client.Name,
                            Seat = client.Seat,
                            AssignedCallsign = client.AssignedCallsign
                        }
                    };

                    if (send)
                        MulticastAllExeceptOne(replyMessage.Encode(),session.Id);

                    // Only redraw client admin UI of server if really needed
                    if (redrawClientAdminList)
                    {
                        _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                            new List<SRClient>(_clients.Values)));
                    }
                }
            }
        }

        private void HandleClientDisconnect(SRSClientSession srsSession, SRClient client)
        {
            var message = new NetworkMessage()
            {
                Client = client,
                MsgType = NetworkMessage.MessageType.CLIENT_DISCONNECT
            };

            MulticastAllExeceptOne(message.Encode(), srsSession.Id);
            try
            {
                srsSession.Dispose();
            }
            catch (Exception) { }
          
        }

        private void HandleClientRadioUpdate(SRSClientSession session, NetworkMessage message, bool send)
        {
            if (_clients.ContainsKey(message.Client.ClientGuid))
            {
                var client = _clients[message.Client.ClientGuid];

                if (client != null)
                {
                    //shouldnt be the case but just incase...
                    if (message.Client.GameState == null)
                    {
                        message.Client.GameState = new PlayerGameState();
                    }
                    //update to local ticks
                    message.Client.GameState.LastUpdate = DateTime.Now.Ticks;
                    var assignedCallsign = _callsignProvider.GetAssignedCallsign(message.Client.Name, message.Client.Coalition);

                    var changed = false;

                    if (client.GameState == null)
                    {
                        client.GameState = message.Client.GameState;
                        changed = true;
                    }
                    else
                    {
                        changed = !client.GameState.Equals(message.Client.GameState) ||
                                  client.Coalition != message.Client.Coalition ||
                                  !string.Equals(client.AssignedCallsign, assignedCallsign, StringComparison.Ordinal);
                    }

                    client.LastUpdate = DateTime.Now.Ticks;
                    client.Name = message.Client.Name;
                    client.Coalition = message.Client.Coalition;
                    client.Seat = message.Client.Seat;
                    client.GameState = message.Client.GameState;
                    client.Seat = message.Client.Seat;
                    client.AssignedCallsign = assignedCallsign;

                    Logger.Debug($"Client radio update: {message.Client.ClientGuid} ({message.Client.Name}) Coalition {message.Client.Coalition}, Callsign {client.AssignedCallsign}, Radios {PrettyPrint(message.Client.GameState.radios)}");

                    TimeSpan lastSent = new TimeSpan(DateTime.Now.Ticks - client.LastRadioUpdateSent);

                    //send update to everyone
                    //Remove Client Radio Info
                    if (send)
                    {
                        NetworkMessage replyMessage;
                        if ((changed || lastSent.TotalSeconds > 180))
                        {
                            client.LastRadioUpdateSent = DateTime.Now.Ticks;
                            replyMessage = new NetworkMessage
                            {
                                MsgType = NetworkMessage.MessageType.RADIO_UPDATE,
                                Client = new SRClient
                                {
                                    ClientGuid = client.ClientGuid,
                                    Coalition = client.Coalition,
                                    Name = client.Name,
                                    GameState = client.GameState, //send radio info
                                    Seat = client.Seat,
                                    AssignedCallsign = client.AssignedCallsign
                                }
                            };
                            Multicast(replyMessage.Encode());
                        }
                    }
                }
            }
        }

        private string PrettyPrint(RadioInformation[] radios)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var radio in radios)
            {
                sb.Append($"CH-{radio.channel} ({radio.freq}) ");
            }
            return sb.ToString(); 
        }

        private void HandleRadioClientsSync(SRSClientSession session, NetworkMessage message)
        {
            RefreshAssignedCallsigns();

            //store new client
            var replyMessage = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.SYNC,
                Clients = new List<SRClient>(_clients.Values),
                ServerSettings = _serverSettings.ToDictionary(),
                Version = UpdaterChecker.VERSION
            };

            session.Send(replyMessage.Encode());

            //send update to everyone
            //Remove Client Radio Info
            var update = new NetworkMessage
            {
                MsgType = NetworkMessage.MessageType.UPDATE,
                Client = new SRClient
                {
                    ClientGuid = message.Client.ClientGuid,
                    Coalition = message.Client.Coalition,
                    GameState = message.Client.GameState,
                    Name = message.Client.Name,
                    AssignedCallsign = _callsignProvider.GetAssignedCallsign(message.Client.Name, message.Client.Coalition)
                }
            };

            Multicast(update.Encode());
            Logger.Trace($"Client sync: Guid {message.Client.ClientGuid}, name {message.Client.Name}, coalition {message.Client.Coalition}");
        }

        private void StartCallsignRefreshTimer()
        {
            _callsignRefreshTimer?.Dispose();
            _callsignRefreshTimer = new Timer(
                RefreshAssignedCallsignsAndBroadcastChanges,
                null,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(1));
        }

        private void RefreshAssignedCallsigns()
        {
            _callsignProvider.RefreshIfNeeded();

            foreach (var client in _clients.Values)
            {
                if (client == null)
                {
                    continue;
                }

                client.AssignedCallsign = _callsignProvider.GetAssignedCallsign(client.Name, client.Coalition);
            }
        }

        private void RefreshAssignedCallsignsAndBroadcastChanges(object state)
        {
            try
            {
                lock (_callsignRefreshLock)
                {
                    _callsignProvider.RefreshIfNeeded();
                    var changedClients = new List<SRClient>();

                    foreach (var client in _clients.Values)
                    {
                        if (client == null)
                        {
                            continue;
                        }

                        var assignedCallsign = _callsignProvider.GetAssignedCallsign(client.Name, client.Coalition);
                        if (string.Equals(client.AssignedCallsign, assignedCallsign, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        client.AssignedCallsign = assignedCallsign;
                        changedClients.Add(client);
                    }

                    foreach (var client in changedClients)
                    {
                        var replyMessage = new NetworkMessage
                        {
                            MsgType = NetworkMessage.MessageType.UPDATE,
                            Client = new SRClient
                            {
                                ClientGuid = client.ClientGuid,
                                Coalition = client.Coalition,
                                Name = client.Name,
                                Seat = client.Seat,
                                AssignedCallsign = client.AssignedCallsign
                            }
                        };

                        Multicast(replyMessage.Encode());
                    }

                    if (changedClients.Count > 0)
                    {
                        Logger.Info($"Broadcast assigned callsign updates for {changedClients.Count} client(s)");
                        _eventAggregator.PublishOnUIThread(new ServerStateMessage(true,
                            new List<SRClient>(_clients.Values)));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Exception refreshing assigned callsigns");
            }
        }

        public void RequestStop()
        {
            try
            {
                _callsignRefreshTimer?.Dispose();
                _callsignRefreshTimer = null;
                _natHandler?.CloseNAT();
            }
            catch
            {
            }

            try
            {
                DisconnectAll();
                Stop();
                _clients.Clear();
            }
            catch (Exception ex)
            {
            }
        }
    }
}
