using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Settings
{
    public enum SettingKind
    {
        Toggle,
        Number,
        Text,
        FrequencyList
    }

    /// <summary>How a setting is shown, validated and applied by the admin UI and API.</summary>
    public sealed class SettingDefinition
    {
        private SettingDefinition(ServerSettingsKeys key, string group, string label, string description,
            SettingKind kind, int minimum = 0, int maximum = 0, bool inverted = false,
            bool requiresRestart = false, bool hidden = false, bool secret = false)
        {
            Key = key;
            Group = group;
            Label = label;
            Description = description;
            Kind = kind;
            Minimum = minimum;
            Maximum = maximum;
            Inverted = inverted;
            RequiresRestart = requiresRestart;
            Hidden = hidden;
            Secret = secret;
        }

        public ServerSettingsKeys Key { get; }
        public string Name => Key.ToString();
        public string Group { get; }
        public string Label { get; }
        public string Description { get; }
        public SettingKind Kind { get; }
        public int Minimum { get; }
        public int Maximum { get; }

        /// <summary>The stored value means the opposite of the label (SPECTATORS_AUDIO_DISABLED is shown as "Spectator Audio").</summary>
        public bool Inverted { get; }

        /// <summary>Only applied when the SRS server is (re)started.</summary>
        public bool RequiresRestart { get; }

        /// <summary>Not used by this server (WPF-only or computed), so not shown in the UI.</summary>
        public bool Hidden { get; }

        /// <summary>Never returned by the UI or API.</summary>
        public bool Secret { get; }

        public const string RadioGroup = "Radio";
        public const string ClientGroup = "Client display";
        public const string ServerGroup = "Server";

        public static readonly IReadOnlyList<SettingDefinition> All = new[]
        {
            new SettingDefinition(ServerSettingsKeys.COALITION_AUDIO_SECURITY, RadioGroup, "Secure Coalition Radios",
                "Only players on the same coalition hear each other, except on global lobby frequencies.",
                SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED, RadioGroup, "Spectator Audio",
                "Allow spectators to transmit.", SettingKind.Toggle, inverted: true),
            new SettingDefinition(ServerSettingsKeys.IRL_RADIO_TX, RadioGroup, "Realistic TX Behaviour",
                "Clients cannot receive while they are transmitting.", SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.RADIO_COLLISION_EFFECTS, RadioGroup, "RX Collision Effects",
                "Simultaneous transmissions on one frequency are heard as a collision.", SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.SECOND_RADIO_ENABLED, RadioGroup, "Enable Second Radio",
                "Clients may use a second radio.", SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.CHANNEL_LIMIT, RadioGroup, "Channel Limit",
                "Highest radio channel clients can select.", SettingKind.Number, 5, ChannelNameSettings.MaximumChannel),
            new SettingDefinition(ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES, RadioGroup, "Global Lobby Freq. AM (MHz)",
                "Comma-separated frequencies everyone can hear, regardless of coalition.", SettingKind.FrequencyList),
            new SettingDefinition(ServerSettingsKeys.PRIORITY_TRANSMITTER_NAMES, RadioGroup, "Priority Transmitters",
                "Comma-separated client names whose transmissions win radio collisions. Clients also leave them out of the Pilot Roster.", SettingKind.Text,
                requiresRestart: true),

            new SettingDefinition(ServerSettingsKeys.SHOW_TUNED_COUNT, ClientGroup, "Show Tuned/Client Count",
                "Clients see how many players are tuned to each frequency.", SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.SHOW_TRANSMITTER_NAME, ClientGroup, "Show Transmitter Name",
                "Clients see the name of whoever is transmitting.", SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS, ClientGroup, "Squad Channel Labels",
                "Append the majority squad tag to channel names above channel 2.", SettingKind.Toggle),

            new SettingDefinition(ServerSettingsKeys.SERVER_PORT, ServerGroup, "Server Port",
                "TCP and UDP port clients connect to.", SettingKind.Number, 1, 65535, requiresRestart: true),
            new SettingDefinition(ServerSettingsKeys.CLIENT_EXPORT_ENABLED, ServerGroup, "Auto Export List",
                "Write the connected client list to the export file every 5 seconds.", SettingKind.Toggle),
            new SettingDefinition(ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH, ServerGroup, "Export File",
                "Client list export file. Relative paths are inside the data directory.", SettingKind.Text,
                requiresRestart: true),
            new SettingDefinition(ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE, ServerGroup, "Pilot Roster JSON",
                "Assigned-callsign JSON file for the Pilot Roster. Relative paths are inside the data directory.",
                SettingKind.Text),

            new SettingDefinition(ServerSettingsKeys.SERVER_UI_THEME, ServerGroup, "Server Theme",
                "WPF server only.", SettingKind.Text, hidden: true),
            new SettingDefinition(ServerSettingsKeys.CHECK_FOR_BETA_UPDATES, ServerGroup, "Check for beta updates",
                "WPF server only.", SettingKind.Toggle, hidden: true),
            new SettingDefinition(ServerSettingsKeys.UPNP_ENABLED, ServerGroup, "UPnP",
                "Not supported; forward the port on the host or router.", SettingKind.Toggle, hidden: true),
            new SettingDefinition(ServerSettingsKeys.PILOT_ROSTER_DATA_AVAILABLE, ServerGroup, "Pilot Roster data available",
                "Computed by the server.", SettingKind.Toggle, hidden: true),
            new SettingDefinition(ServerSettingsKeys.DSERVER_RCON_ADDRESS, ServerGroup, "dserver RCon address",
                "Not used yet.", SettingKind.Text, hidden: true),
            new SettingDefinition(ServerSettingsKeys.DSERVER_RCON_USERNAME, ServerGroup, "dserver RCon user",
                "Not used yet.", SettingKind.Text, hidden: true),
            new SettingDefinition(ServerSettingsKeys.DSERVER_RCON_PASSWORD, ServerGroup, "dserver RCon password",
                "Not used yet.", SettingKind.Text, hidden: true, secret: true)
        };

        private static readonly Dictionary<ServerSettingsKeys, SettingDefinition> ByKey = All.ToDictionary(d => d.Key);

        public static SettingDefinition For(ServerSettingsKeys key)
        {
            return ByKey[key];
        }

        public static bool TryFind(string name, out SettingDefinition definition)
        {
            definition = null;
            return Enum.TryParse(name, false, out ServerSettingsKeys key) && ByKey.TryGetValue(key, out definition);
        }

        /// <summary>Validates and normalises a value for storage; returns an error message or null.</summary>
        public string Normalize(string value, out string normalized)
        {
            var expected = Check(value, out normalized);
            return expected == null ? null : $"{Name} must be {expected}.";
        }

        /// <summary>Checks a value typed in the admin UI; returns an error message for the field or null.</summary>
        public string Validate(string value)
        {
            var expected = Check(value, out _);
            return expected == null ? null : $"Enter {expected}.";
        }

        /// <summary>Normalises a value; returns what was expected instead (e.g. "true or false") or null.</summary>
        private string Check(string value, out string normalized)
        {
            value = (value ?? string.Empty).Trim();
            normalized = value;

            switch (Kind)
            {
                case SettingKind.Toggle:
                    if (!SettingValue.TryParseBool(value, out var flag))
                    {
                        return "true or false";
                    }

                    normalized = flag ? "true" : "false";
                    return null;

                case SettingKind.Number:
                    if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ||
                        number < Minimum || number > Maximum)
                    {
                        return $"a whole number from {Minimum} to {Maximum}";
                    }

                    normalized = number.ToString(CultureInfo.InvariantCulture);
                    return null;

                case SettingKind.FrequencyList:
                    var frequencies = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var frequency in frequencies)
                    {
                        if (!double.TryParse(frequency, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) ||
                            mhz <= 0)
                        {
                            return "a comma-separated list of frequencies in MHz, e.g. 248.22,122.3";
                        }
                    }

                    normalized = string.Join(",", frequencies);
                    return null;

                default:
                    if (value.IndexOfAny(new[] { '\r', '\n' }) >= 0)
                    {
                        return "a single line of text";
                    }

                    return null;
            }
        }
    }
}
