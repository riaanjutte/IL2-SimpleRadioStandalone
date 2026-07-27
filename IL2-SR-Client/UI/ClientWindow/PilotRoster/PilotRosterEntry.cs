using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster
{
    public enum PilotRosterSortColumn
    {
        Callsign,
        PilotName,
        Vehicle,
        Radio1,
        Radio2
    }

    public class PilotRosterEntry
    {
        public PilotRosterEntry(string callsign, string pilotName, string vehicle, int radio1Channel, int radio2Channel)
        {
            Callsign = Normalize(callsign, "--");
            PilotName = Normalize(pilotName, "---");
            Vehicle = Normalize(vehicle, string.Empty);
            Radio1ChannelNumber = NormalizeChannel(radio1Channel);
            Radio2ChannelNumber = NormalizeChannel(radio2Channel);
            Radio1Channel = FormatChannel(Radio1ChannelNumber);
            Radio2Channel = FormatChannel(Radio2ChannelNumber);
        }

        public string Callsign { get; }

        public string PilotName { get; }

        public string Vehicle { get; }

        public bool HasVehicle => !string.IsNullOrWhiteSpace(Vehicle);

        public string Radio1Channel { get; }

        public string Radio2Channel { get; }

        public int Radio1ChannelNumber { get; }

        public int Radio2ChannelNumber { get; }

        private static string Normalize(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value)
                ? fallback
                : value.Trim().ToUpperInvariant();
        }

        private static int NormalizeChannel(int channel)
        {
            return channel > 0 ? channel : 0;
        }

        private static string FormatChannel(int channel)
        {
            return channel > 0
                ? "CHN " + channel.ToString(CultureInfo.InvariantCulture)
                : "--";
        }
    }

    public sealed class PilotRosterEntryComparer : IComparer, IComparer<PilotRosterEntry>
    {
        private readonly PilotRosterSortColumn _column;
        private readonly ListSortDirection _direction;

        public PilotRosterEntryComparer(PilotRosterSortColumn column, ListSortDirection direction)
        {
            _column = column;
            _direction = direction;
        }

        public int Compare(PilotRosterEntry left, PilotRosterEntry right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            int comparison;
            switch (_column)
            {
                case PilotRosterSortColumn.Callsign:
                    comparison = CompareText(left.Callsign, right.Callsign, "--");
                    break;
                case PilotRosterSortColumn.Vehicle:
                    comparison = CompareText(left.Vehicle, right.Vehicle, string.Empty);
                    break;
                case PilotRosterSortColumn.Radio1:
                    comparison = CompareChannel(left.Radio1ChannelNumber, right.Radio1ChannelNumber);
                    break;
                case PilotRosterSortColumn.Radio2:
                    comparison = CompareChannel(left.Radio2ChannelNumber, right.Radio2ChannelNumber);
                    break;
                default:
                    comparison = CompareText(left.PilotName, right.PilotName, "---");
                    break;
            }

            if (comparison == 0)
            {
                comparison = System.StringComparer.OrdinalIgnoreCase.Compare(left.PilotName, right.PilotName);
            }

            return comparison;
        }

        int IComparer.Compare(object x, object y)
        {
            return Compare(x as PilotRosterEntry, y as PilotRosterEntry);
        }

        private int CompareText(string left, string right, string missingValue)
        {
            var missingComparison = CompareMissing(
                string.IsNullOrWhiteSpace(left) || left == missingValue,
                string.IsNullOrWhiteSpace(right) || right == missingValue);
            if (missingComparison.HasValue)
            {
                return missingComparison.Value;
            }

            var comparison = System.StringComparer.OrdinalIgnoreCase.Compare(left, right);
            return ApplyDirection(comparison);
        }

        private int CompareChannel(int left, int right)
        {
            var missingComparison = CompareMissing(left <= 0, right <= 0);
            if (missingComparison.HasValue)
            {
                return missingComparison.Value;
            }

            return ApplyDirection(left.CompareTo(right));
        }

        private static int? CompareMissing(bool leftMissing, bool rightMissing)
        {
            if (leftMissing == rightMissing)
            {
                return null;
            }

            return leftMissing ? 1 : -1;
        }

        private int ApplyDirection(int comparison)
        {
            return _direction == ListSortDirection.Descending ? -comparison : comparison;
        }
    }
}
