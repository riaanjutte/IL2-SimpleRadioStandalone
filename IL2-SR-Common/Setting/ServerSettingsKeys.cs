using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Setting
{
    public enum ServerSettingsKeys
    {
        SERVER_PORT,
        COALITION_AUDIO_SECURITY,
        SPECTATORS_AUDIO_DISABLED,
        CLIENT_EXPORT_ENABLED,
        IRL_RADIO_TX,
        CLIENT_EXPORT_FILE_PATH,
        CHECK_FOR_BETA_UPDATES,
        SERVER_UI_THEME,
        SHOW_TUNED_COUNT,
        GLOBAL_LOBBY_FREQUENCIES,
        SHOW_TRANSMITTER_NAME,
        UPNP_ENABLED,
        SECOND_RADIO_ENABLED,
        CHANNEL_LIMIT,
        SHOW_SQUAD_CHANNEL_LABELS,
        RADIO_COLLISION_EFFECTS,
        PRIORITY_TRANSMITTER_NAMES,
        ASSIGNED_CALLSIGNS_JSON_FILE,
        PILOT_ROSTER_DATA_AVAILABLE,
        DSERVER_RCON_ADDRESS,
        DSERVER_RCON_USERNAME,
        DSERVER_RCON_PASSWORD
    }

    public class DefaultServerSettings
    {
        public static readonly HashSet<string> ServerSectionSettings = new HashSet<string>()
        {
            ServerSettingsKeys.SERVER_PORT.ToString(),
            ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH.ToString(),
            ServerSettingsKeys.CHECK_FOR_BETA_UPDATES.ToString(),
            ServerSettingsKeys.SERVER_UI_THEME.ToString(),
            ServerSettingsKeys.UPNP_ENABLED.ToString()
        };

        public static readonly Dictionary<string, string> Defaults = new Dictionary<string, string>()
        {
            { ServerSettingsKeys.CLIENT_EXPORT_ENABLED.ToString(), "false" },
            { ServerSettingsKeys.COALITION_AUDIO_SECURITY.ToString(), "false" },
            { ServerSettingsKeys.IRL_RADIO_TX.ToString(), "false" },
            { ServerSettingsKeys.SERVER_PORT.ToString(), "6002" },
            { ServerSettingsKeys.SPECTATORS_AUDIO_DISABLED.ToString(), "false" },
            { ServerSettingsKeys.CLIENT_EXPORT_FILE_PATH.ToString(), "clients-list.json" },
            { ServerSettingsKeys.CHECK_FOR_BETA_UPDATES.ToString(), "false" },
            { ServerSettingsKeys.SERVER_UI_THEME.ToString(), "White" },
            { ServerSettingsKeys.SHOW_TUNED_COUNT.ToString(), "true" },
            { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString(), "248.22" },
            { ServerSettingsKeys.UPNP_ENABLED.ToString(), "false" },
            { ServerSettingsKeys.SHOW_TRANSMITTER_NAME.ToString(), "true" },
            { ServerSettingsKeys.SECOND_RADIO_ENABLED.ToString(), "true" },
            { ServerSettingsKeys.CHANNEL_LIMIT.ToString(), "5" },
            { ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS.ToString(), "false" },
            { ServerSettingsKeys.RADIO_COLLISION_EFFECTS.ToString(), "false" },
            { ServerSettingsKeys.PRIORITY_TRANSMITTER_NAMES.ToString(), "Axis Command,Allies Command,Axis Airfield,Allies Airfield" },
            { ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE.ToString(), "" },
            { ServerSettingsKeys.DSERVER_RCON_ADDRESS.ToString(), "" },
            { ServerSettingsKeys.DSERVER_RCON_USERNAME.ToString(), "" },
            { ServerSettingsKeys.DSERVER_RCON_PASSWORD.ToString(), "" },
        };
    }
}
