using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Diagnostics
{
    public sealed class IncomingAudioQualityMonitor
    {
        private enum EventType
        {
            PacketDecoded,
            JitterSpike,
            MissingPacket,
            DuplicatePacket,
            LatePacket,
            DecodeFailure,
            PlaybackUnderrun,
            QueueDrop,
            BufferClear
        }

        private sealed class QualityEvent
        {
            public long Ticks { get; set; }
            public EventType Type { get; set; }
            public int Count { get; set; }
            public int Radio { get; set; }
            public string ClientGuid { get; set; }
            public int Milliseconds { get; set; }
        }

        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan Window = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MinimumLogInterval = TimeSpan.FromSeconds(15);

        private const int MinimumPacketsForJitterLog = 10;
        private const int JitterSpikeThresholdMs = 140;
        private const int JitterSpikeLogThreshold = 5;
        private const int MissingPacketLogThreshold = 4;
        private const int DecodeFailureLogThreshold = 2;
        private const int PlaybackUnderrunLogThreshold = 10;

        private readonly object _lock = new object();
        private readonly Queue<QualityEvent> _events = new Queue<QualityEvent>();
        private long _lastLogTicks;

        private IncomingAudioQualityMonitor()
        {
        }

        public static IncomingAudioQualityMonitor Instance { get; } = new IncomingAudioQualityMonitor();

        public void Reset()
        {
            lock (_lock)
            {
                _events.Clear();
                _lastLogTicks = 0;
            }
        }

        public void RecordDecodedPacket(string clientGuid, int radio, ulong packetNumber, long previousUpdateTicks, bool newTransmission)
        {
            var now = DateTime.UtcNow.Ticks;
            lock (_lock)
            {
                AddEvent(now, EventType.PacketDecoded, 1, radio, clientGuid, 0);

                if (!newTransmission && previousUpdateTicks > 0)
                {
                    var interArrivalMs = (int) TimeSpan.FromTicks(DateTime.Now.Ticks - previousUpdateTicks).TotalMilliseconds;
                    if (interArrivalMs >= JitterSpikeThresholdMs)
                    {
                        AddEvent(now, EventType.JitterSpike, 1, radio, clientGuid, interArrivalMs);
                    }
                }

                TryLogIfDegraded(now);
            }
        }

        public void RecordDecodeFailure(string clientGuid, int radio, ulong packetNumber)
        {
            Record(EventType.DecodeFailure, 1, radio, clientGuid, 0);
        }

        public void RecordMissingPackets(string clientGuid, int radio, ulong previousPacket, ulong currentPacket, ulong missingPackets)
        {
            if (missingPackets == 0)
            {
                return;
            }

            Record(EventType.MissingPacket, CapToInt(missingPackets), radio, clientGuid, 0);
        }

        public void RecordDuplicatePacket(string clientGuid, int radio, ulong packetNumber)
        {
            Record(EventType.DuplicatePacket, 1, radio, clientGuid, 0);
        }

        public void RecordLatePacket(string clientGuid, int radio, ulong packetNumber)
        {
            Record(EventType.LatePacket, 1, radio, clientGuid, 0);
        }

        public void RecordPlaybackUnderrun(string clientGuid, int radio)
        {
            Record(EventType.PlaybackUnderrun, 1, radio, clientGuid, 0);
        }

        public void RecordQueueDrop(int droppedPackets)
        {
            Record(EventType.QueueDrop, Math.Max(1, droppedPackets), -1, string.Empty, 0);
        }

        public void RecordBufferClear(int bufferedMilliseconds)
        {
            Record(EventType.BufferClear, 1, -1, string.Empty, bufferedMilliseconds);
        }

        private void Record(EventType type, int count, int radio, string clientGuid, int milliseconds)
        {
            var now = DateTime.UtcNow.Ticks;
            lock (_lock)
            {
                AddEvent(now, type, count, radio, clientGuid, milliseconds);
                TryLogIfDegraded(now);
            }
        }

        private void AddEvent(long now, EventType type, int count, int radio, string clientGuid, int milliseconds)
        {
            _events.Enqueue(new QualityEvent
            {
                Ticks = now,
                Type = type,
                Count = count,
                Radio = radio,
                ClientGuid = clientGuid ?? string.Empty,
                Milliseconds = milliseconds
            });

            Prune(now);
        }

        private void TryLogIfDegraded(long now)
        {
            if (_events.Count == 0 || (_lastLogTicks > 0 && new TimeSpan(now - _lastLogTicks) < MinimumLogInterval))
            {
                return;
            }

            var events = _events.ToArray();
            var decodedPackets = Sum(events, EventType.PacketDecoded);
            var jitterSpikes = Sum(events, EventType.JitterSpike);
            var missingPackets = Sum(events, EventType.MissingPacket);
            var duplicatePackets = Sum(events, EventType.DuplicatePacket);
            var latePackets = Sum(events, EventType.LatePacket);
            var decodeFailures = Sum(events, EventType.DecodeFailure);
            var playbackUnderruns = Sum(events, EventType.PlaybackUnderrun);
            var queueDrops = Sum(events, EventType.QueueDrop);
            var bufferClears = Sum(events, EventType.BufferClear);

            if (queueDrops == 0 &&
                bufferClears == 0 &&
                decodeFailures < DecodeFailureLogThreshold &&
                missingPackets < MissingPacketLogThreshold &&
                playbackUnderruns < PlaybackUnderrunLogThreshold &&
                (decodedPackets < MinimumPacketsForJitterLog || jitterSpikes < JitterSpikeLogThreshold))
            {
                return;
            }

            _lastLogTicks = now;

            Logger.Warn(
                "Incoming audio potentially degraded over last {0}s: reason={1}; packets={2}; missingPackets={3}; jitterSpikes={4}; maxJitterMs={5}; playbackUnderruns={6}; decodeFailures={7}; queueDrops={8}; bufferClears={9}; duplicatePackets={10}; latePackets={11}; radios={12}",
                (int) Window.TotalSeconds,
                BuildReason(queueDrops, bufferClears, decodeFailures, missingPackets, playbackUnderruns, jitterSpikes),
                decodedPackets,
                missingPackets,
                jitterSpikes,
                MaxMilliseconds(events, EventType.JitterSpike),
                playbackUnderruns,
                decodeFailures,
                queueDrops,
                bufferClears,
                duplicatePackets,
                latePackets,
                BuildRadioSummary(events));
        }

        private void Prune(long now)
        {
            var oldest = now - Window.Ticks;
            while (_events.Count > 0 && _events.Peek().Ticks < oldest)
            {
                _events.Dequeue();
            }
        }

        private static int Sum(IEnumerable<QualityEvent> events, EventType type)
        {
            return events.Where(item => item.Type == type).Sum(item => item.Count);
        }

        private static int MaxMilliseconds(IEnumerable<QualityEvent> events, EventType type)
        {
            return events.Where(item => item.Type == type).Select(item => item.Milliseconds).DefaultIfEmpty(0).Max();
        }

        private static string BuildReason(int queueDrops, int bufferClears, int decodeFailures, int missingPackets, int playbackUnderruns, int jitterSpikes)
        {
            if (queueDrops > 0)
            {
                return "local voice queue pressure";
            }

            if (bufferClears > 0)
            {
                return "receive buffer backlog";
            }

            if (decodeFailures >= DecodeFailureLogThreshold)
            {
                return "Opus decode failures";
            }

            if (missingPackets >= MissingPacketLogThreshold)
            {
                return "network packet loss";
            }

            if (playbackUnderruns >= PlaybackUnderrunLogThreshold)
            {
                return "local playback underruns";
            }

            if (jitterSpikes >= JitterSpikeLogThreshold)
            {
                return "network jitter";
            }

            return "mixed indicators";
        }

        private static string BuildRadioSummary(IEnumerable<QualityEvent> events)
        {
            var radios = events
                .Where(item => item.Radio >= 0 && item.Type != EventType.PacketDecoded)
                .GroupBy(item => item.Radio)
                .OrderBy(group => group.Key)
                .Select(group => "R" + group.Key + ":" + group.Sum(item => item.Count))
                .ToArray();

            return radios.Length == 0 ? "n/a" : string.Join(",", radios);
        }

        private static int CapToInt(ulong value)
        {
            return value > int.MaxValue ? int.MaxValue : (int) value;
        }
    }
}
