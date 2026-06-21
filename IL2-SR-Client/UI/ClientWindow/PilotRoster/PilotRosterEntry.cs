namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public class PilotRosterEntry
    {
        public PilotRosterEntry(string callsign, string pilotName, string vehicle, string radio1Channel, string radio2Channel)
        {
            Callsign = Normalize(callsign, "--");
            PilotName = Normalize(pilotName, "---");
            Vehicle = Normalize(vehicle, string.Empty);
            Radio1Channel = Normalize(radio1Channel, "--");
            Radio2Channel = Normalize(radio2Channel, "--");
        }

        public string Callsign { get; }

        public string PilotName { get; }

        public string Vehicle { get; }

        public bool HasVehicle => !string.IsNullOrWhiteSpace(Vehicle);

        public string Radio1Channel { get; }

        public string Radio2Channel { get; }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().ToUpperInvariant();
        }
    }
}
