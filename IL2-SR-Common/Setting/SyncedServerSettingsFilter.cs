using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Setting
{
    public static class SyncedServerSettingsFilter
    {
        public static void RemoveServerOnlySettings(Dictionary<string, string> settings)
        {
            if (settings == null)
            {
                return;
            }

            settings.Remove(ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE.ToString());
            settings.Remove(ServerSettingsKeys.DSERVER_RCON_ADDRESS.ToString());
            settings.Remove(ServerSettingsKeys.DSERVER_RCON_USERNAME.ToString());
            settings.Remove(ServerSettingsKeys.DSERVER_RCON_PASSWORD.ToString());
            settings.Remove(ServerSettingsKeys.LOBBY_MUSIC_ENABLED.ToString());
            settings.Remove(ServerSettingsKeys.LOBBY_MUSIC_DIRECTORY.ToString());
            settings.Remove(ServerSettingsKeys.LOBBY_MUSIC_VOLUME.ToString());
        }
    }
}
