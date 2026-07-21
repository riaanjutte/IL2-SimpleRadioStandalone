namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public static class PilotRosterAccessPolicy
    {
        public const string UnavailableSummary = "Pilot roster is only available on servers that provide Pilot Roster data.";
        public const string ReconnectInstruction = "If this server does not currently provide the data, contact the server owners and ask them to provide it.";

        public static bool IsAvailable(bool isConnected, bool serverProvidesRosterData)
        {
            return isConnected && serverProvidesRosterData;
        }

        public static bool ShouldAutoStart(bool isConnected, bool serverProvidesRosterData, bool autoStartEnabled,
            bool alreadyStarted)
        {
            return IsAvailable(isConnected, serverProvidesRosterData) && autoStartEnabled && !alreadyStarted;
        }
    }
}
