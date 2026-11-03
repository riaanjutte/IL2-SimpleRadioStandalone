using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Events
{
    /// <summary>Every event type the server itself publishes; plugins add theirs (see <see cref="IEventTypeRegistry"/>).</summary>
    public static class SrsEventTypes
    {
        private static readonly SrsEventField[] ClientFields =
        {
            new("client.guid", "string", "SRS client id", "kq2DJtj3T0aD4l9oU1Vx3w"),
            new("client.name", "string", "Player name", "Hartmann"),
            new("client.coalition", "number", "Coalition: 0 spectators, 1 red, 2 blue", 1),
            new("client.coalitionName", "string", "Red, Blue or Spectators", "Red"),
            new("client.callsign", "string", "Assigned callsign, when the pilot roster provides one", "Karaya 1"),
            new("client.vehicle", "string", "Assigned aircraft, when the pilot roster provides one", "Bf 109 G-6"),
            new("client.airfield", "string", "Assigned airfield, when the pilot roster provides one", "Kalinovka"),
            new("client.ip", "string", "Client IP address, when known", "203.0.113.7")
        };

        private static readonly SrsEventField[] ServerFields =
        {
            new("server.port", "number", "SRS port", 6002),
            new("server.clientCount", "number", "Connected clients", 12),
            new("server.uptime", "number",
                "Seconds the server had been running (0 when it starts); " +
                "{{ timespan.from_seconds server.uptime }} shows it as 03:12:40", 11560)
        };

        private static readonly SrsEventField[] BanFields =
        {
            new("ban.id", "number", "Ban id", 7),
            new("ban.ip", "string", "Banned IP address", "203.0.113.7"),
            new("ban.playerName", "string", "Player name recorded with the ban", "Hartmann"),
            new("ban.reason", "string", "Reason given", "Team killing"),
            new("ban.expiresAt", "string", "Expiry time (UTC, ISO 8601); empty for permanent bans",
                "2026-10-13T18:00:00.0000000Z"),
            new("ban.permanent", "boolean", "True when the ban never expires", false)
        };

        private static readonly SrsEventField[] ApiKeyFields =
        {
            new("apiKey.name", "string", "Key name", "Discord bot"),
            new("apiKey.scope", "string", "read or write", "read"),
            new("apiKey.prefix", "string", "Key prefix shown in the admin UI", "a1b2c3d4")
        };

        private static readonly SrsEventField[] RemoteFields =
        {
            new("ip", "string", "Remote IP address", "203.0.113.7")
        };

        public static readonly SrsEventType ServerStarted = new("server.started", EventCategory.Server,
            "Server started", "The SRS server started listening, at startup or from the admin UI or API.",
            ServerFields);

        public static readonly SrsEventType ServerStopped = new("server.stopped", EventCategory.Server,
            "Server stopped", "The SRS server stopped; connected clients were disconnected.", ServerFields);

        public static readonly SrsEventType ServerStartFailed = new("server.start_failed", EventCategory.Server,
            "Server failed to start", "Starting the SRS server failed, for example because the port is in use.",
            ServerFields, new SrsEventField[] { new("error", "string", "Error message", "Port 6002 is already in use") });

        public static readonly SrsEventType ClientConnected = new("client.connected", EventCategory.Client,
            "Client connected", "A client completed the SRS handshake.", ClientFields);

        public static readonly SrsEventType ClientDisconnected = new("client.disconnected", EventCategory.Client,
            "Client disconnected", "A connected client left, timed out or was disconnected.", ClientFields);

        public static readonly SrsEventType ClientRejected = new("client.rejected", EventCategory.Client,
            "Connection refused", "A connection was refused: banned address, unsupported version or not an SRS client.",
            RemoteFields, new SrsEventField[] { new("reason", "string", "Why the connection was refused", "banned") });

        public static readonly SrsEventType ClientNameChanged = new("client.name_changed", EventCategory.Client,
            "Client renamed", "A connected client changed player name.",
            ClientFields, new SrsEventField[] { new("previousName", "string", "Name before the change", "Hartmann (2)") });

        public static readonly SrsEventType ClientCoalitionChanged = new("client.coalition_changed",
            EventCategory.Client, "Coalition changed", "A connected client switched coalition.",
            ClientFields, new SrsEventField[]
            {
                new("previousCoalition", "number", "Coalition before the change", 0),
                new("previousCoalitionName", "string", "Red, Blue or Spectators", "Spectators")
            });

        public static readonly SrsEventType RadioFrequencyChanged = new("radio.frequency_changed",
            EventCategory.Radio, "Radio retuned",
            "A client tuned a radio to another channel or frequency, or turned it on or off. Reported once the radio " +
            "has settled for two seconds, so turning the dial produces one event.",
            ClientFields, new SrsEventField[]
            {
                new("radio.index", "number", "Radio number (1 or 2)", 1),
                new("radio.enabled", "boolean", "False when the radio was turned off", true),
                new("radio.channel", "number", "Channel, when on an AM channel", 4),
                new("radio.channelName", "string", "Channel name from Channel Names, when set", "Tower"),
                new("radio.frequencyMhz", "number", "Frequency in MHz; empty when turned off", 254.0),
                new("radio.modulation", "string", "AM or FM", "AM"),
                new("radio.previousChannel", "number", "Channel before the change", 1),
                new("radio.previousFrequencyMhz", "number", "Frequency before the change; empty when it was off", 251.0)
            });

        public static readonly SrsEventType ClientKicked = new("admin.client_kicked", EventCategory.Admin,
            "Client kicked", "An admin kicked a client.", ClientFields);

        public static readonly SrsEventType ClientBanned = new("admin.client_banned", EventCategory.Admin,
            "Client banned", "An admin banned a connected client's IP address.", ClientFields, BanFields);

        public static readonly SrsEventType AddressBanned = new("admin.ip_banned", EventCategory.Admin,
            "IP address banned", "An admin banned an IP address directly.",
            BanFields, new SrsEventField[] { new("disconnectedClients", "number", "Clients disconnected by the ban", 1) });

        public static readonly SrsEventType BanRemoved = new("admin.ban_removed", EventCategory.Admin,
            "Ban removed", "An admin removed a ban.", BanFields);

        public static readonly SrsEventType ClientMuted = new("admin.client_muted", EventCategory.Admin,
            "Client muted", "An admin muted a client.", ClientFields);

        public static readonly SrsEventType ClientUnmuted = new("admin.client_unmuted", EventCategory.Admin,
            "Client unmuted", "An admin unmuted a client.", ClientFields);

        public static readonly SrsEventType SettingChanged = new("admin.setting_changed", EventCategory.Admin,
            "Setting changed", "An admin changed a server setting. Values of secret settings are left out.",
            new SrsEventField[]
            {
                new("setting.key", "string", "Setting key, e.g. COALITION_AUDIO_SECURITY", "COALITION_AUDIO_SECURITY"),
                new("setting.label", "string", "Setting name shown in the admin UI", "Coalition security"),
                new("setting.previousValue", "string", "Value before the change; empty for secrets", "false"),
                new("setting.value", "string", "New value; empty for secrets", "true"),
                new("setting.secret", "boolean", "True for secret settings such as passwords", false),
                new("setting.requiresRestart", "boolean", "True when the change applies after a restart", false)
            });

        public static readonly SrsEventType ChannelNamesChanged = new("admin.channel_names_changed",
            EventCategory.Admin, "Channel names changed", "An admin updated the channel names.",
            new SrsEventField[]
            {
                new("changed", "string", "Which names were saved: all, shared, allies or axis", "all"),
                new("namedChannels", "number", "Named channels in all three sets", 9),
                new("shared", "number", "Channels with a shared name (both coalitions)", 5),
                new("allies", "number", "Channels with an Allies name", 2),
                new("axis", "number", "Channels with an Axis name", 2)
            });

        public static readonly SrsEventType ApiKeyCreated = new("admin.api_key_created", EventCategory.Admin,
            "API key created", "An admin created an API key.", ApiKeyFields);

        public static readonly SrsEventType ApiKeyRevoked = new("admin.api_key_revoked", EventCategory.Admin,
            "API key revoked", "An admin revoked an API key.", ApiKeyFields);

        public static readonly SrsEventType EventLogCleared = new("admin.event_log_cleared", EventCategory.Admin,
            "Event log cleared", "An admin deleted all stored events.",
            new SrsEventField[] { new("deleted", "number", "Events deleted", 1520) });

        public static readonly SrsEventType AdminLogin = new("auth.login", EventCategory.Auth,
            "Admin login", "Someone logged in to the admin UI.", RemoteFields);

        public static readonly SrsEventType AdminLoginFailed = new("auth.login_failed", EventCategory.Auth,
            "Failed admin login", "A login to the admin UI used the wrong password.", RemoteFields);

        public static readonly SrsEventType AdminLogout = new("auth.logout", EventCategory.Auth,
            "Admin logout", "Someone logged out of the admin UI.", RemoteFields);

        public static readonly IReadOnlyList<SrsEventType> All = new[]
        {
            ServerStarted, ServerStopped, ServerStartFailed,
            ClientConnected, ClientDisconnected, ClientRejected, ClientNameChanged, ClientCoalitionChanged,
            RadioFrequencyChanged,
            ClientKicked, ClientBanned, AddressBanned, BanRemoved, ClientMuted, ClientUnmuted, SettingChanged,
            ChannelNamesChanged, ApiKeyCreated, ApiKeyRevoked, EventLogCleared,
            AdminLogin, AdminLoginFailed, AdminLogout
        };

        private static readonly Dictionary<string, SrsEventType> ByType =
            All.ToDictionary(type => type.Type, StringComparer.Ordinal);

        /// <summary>The event type with this name, or null.</summary>
        public static SrsEventType Find(string type)
        {
            return type != null && ByType.TryGetValue(type, out var eventType) ? eventType : null;
        }
    }

    /// <summary>The built-in event types and those plugins registered.</summary>
    public sealed class EventTypeRegistry : IEventTypeRegistry
    {
        private readonly Dictionary<string, SrsEventType> _byType;

        public EventTypeRegistry(IEnumerable<EventTypeRegistration> registrations)
        {
            var all = new List<SrsEventType>(SrsEventTypes.All);
            _byType = all.ToDictionary(type => type.Type, StringComparer.Ordinal);
            foreach (var type in registrations.SelectMany(registration => registration.Types))
            {
                if (_byType.TryAdd(type.Type, type))
                {
                    all.Add(type);
                }
            }

            All = all;
        }

        public IReadOnlyList<SrsEventType> All { get; }

        public SrsEventType Find(string type)
        {
            return type != null && _byType.TryGetValue(type, out var eventType) ? eventType : null;
        }
    }
}
