using System;
using System.Collections.Generic;
using System.Linq;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Network
{
    public sealed class RadioCollisionDetector
    {
        public static readonly TimeSpan DefaultActivityWindow = TimeSpan.FromMilliseconds(120);

        private readonly object _sync = new object();
        private readonly TimeSpan _activityWindow;
        private readonly List<ActiveTransmission> _activeTransmissions = new List<ActiveTransmission>();

        public RadioCollisionDetector() : this(DefaultActivityWindow)
        {
        }

        public RadioCollisionDetector(TimeSpan activityWindow)
        {
            if (activityWindow <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(activityWindow));
            }

            _activityWindow = activityWindow;
        }

        public bool RegisterPacket(string senderGuid, int coalition, double[] frequencies, byte[] modulations,
            bool isolateCoalitions)
        {
            return RegisterPacket(senderGuid, coalition, frequencies, modulations, isolateCoalitions,
                DateTime.UtcNow);
        }

        public bool RegisterPacket(string senderGuid, int coalition, double[] frequencies, byte[] modulations,
            bool isolateCoalitions, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(senderGuid))
            {
                return false;
            }

            var channels = BuildChannels(frequencies, modulations);

            lock (_sync)
            {
                _activeTransmissions.RemoveAll(active =>
                    utcNow - active.LastPacketAtUtc > _activityWindow);

                var collision = channels.Count > 0 &&
                                _activeTransmissions.Any(active =>
                                    !string.Equals(active.SenderGuid, senderGuid, StringComparison.Ordinal) &&
                                    (!isolateCoalitions || active.Coalition == coalition) &&
                                    ChannelsOverlap(active.Channels, channels));

                _activeTransmissions.RemoveAll(active =>
                    string.Equals(active.SenderGuid, senderGuid, StringComparison.Ordinal));

                if (channels.Count > 0)
                {
                    _activeTransmissions.Add(new ActiveTransmission(senderGuid, coalition, channels, utcNow));
                }

                return collision;
            }
        }

        public void Reset()
        {
            lock (_sync)
            {
                _activeTransmissions.Clear();
            }
        }

        private static List<RadioChannel> BuildChannels(double[] frequencies, byte[] modulations)
        {
            var channels = new List<RadioChannel>();
            if (frequencies == null)
            {
                return channels;
            }

            for (var i = 0; i < frequencies.Length; i++)
            {
                var modulation = modulations != null && i < modulations.Length
                    ? (RadioInformation.Modulation)modulations[i]
                    : RadioInformation.Modulation.DISABLED;

                if (frequencies[i] <= 10000 ||
                    modulation == RadioInformation.Modulation.INTERCOM ||
                    modulation == RadioInformation.Modulation.DISABLED)
                {
                    continue;
                }

                channels.Add(new RadioChannel(frequencies[i], modulation));
            }

            return channels;
        }

        private static bool ChannelsOverlap(IEnumerable<RadioChannel> first, IEnumerable<RadioChannel> second)
        {
            return first.Any(left => second.Any(right =>
                left.Modulation == right.Modulation &&
                PlayerGameState.FreqCloseEnough(left.Frequency, right.Frequency)));
        }

        private sealed class ActiveTransmission
        {
            public ActiveTransmission(string senderGuid, int coalition, List<RadioChannel> channels,
                DateTime lastPacketAtUtc)
            {
                SenderGuid = senderGuid;
                Coalition = coalition;
                Channels = channels;
                LastPacketAtUtc = lastPacketAtUtc;
            }

            public string SenderGuid { get; }
            public int Coalition { get; }
            public List<RadioChannel> Channels { get; }
            public DateTime LastPacketAtUtc { get; }
        }

        private sealed class RadioChannel
        {
            public RadioChannel(double frequency, RadioInformation.Modulation modulation)
            {
                Frequency = frequency;
                Modulation = modulation;
            }

            public double Frequency { get; }
            public RadioInformation.Modulation Modulation { get; }
        }
    }
}
