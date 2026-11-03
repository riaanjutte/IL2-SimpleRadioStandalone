using System;
using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Integrations
{
    /// <summary>
    /// Keeps each webhook under a number of requests per minute (Discord allows about 30 per channel), so a burst
    /// of events waits instead of being rejected.
    /// </summary>
    public sealed class WebhookRateLimiter
    {
        public const int DefaultPerMinute = 30;
        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private readonly Dictionary<string, Queue<DateTime>> _sent = new(StringComparer.Ordinal);
        private DateTime _lastSweep = DateTime.MinValue;
        private readonly Func<DateTime> _clock;
        private readonly int _perMinute;

        public WebhookRateLimiter() : this(DefaultPerMinute, () => DateTime.UtcNow)
        {
        }

        internal WebhookRateLimiter(int perMinute, Func<DateTime> clock)
        {
            _perMinute = perMinute;
            _clock = clock;
        }

        /// <summary>Webhooks with a message sent in the last minute; the others are forgotten.</summary>
        internal int Tracked
        {
            get
            {
                lock (_sent)
                {
                    return _sent.Count;
                }
            }
        }

        /// <summary>
        /// Takes a slot for the webhook; returns null when it may send now, otherwise how long to wait.
        /// <paramref name="webhook"/> identifies the webhook, e.g. a hash of its URL: it is kept in memory, so it
        /// should not be the secret URL itself.
        /// </summary>
        public TimeSpan? TryAcquire(string webhook)
        {
            var now = _clock();
            lock (_sent)
            {
                // At most once a minute, forget webhooks with nothing sent in the last minute, e.g. deleted
                // integrations or changed URLs.
                if (now - _lastSweep >= Window)
                {
                    _lastSweep = now;
                    foreach (var (key, queue) in _sent)
                    {
                        Expire(queue, now);
                        if (queue.Count == 0)
                        {
                            _sent.Remove(key);
                        }
                    }
                }

                if (!_sent.TryGetValue(webhook, out var sent))
                {
                    sent = new Queue<DateTime>();
                    _sent[webhook] = sent;
                }

                Expire(sent, now);

                if (sent.Count >= _perMinute)
                {
                    return sent.Peek() + Window - now;
                }

                sent.Enqueue(now);
                return null;
            }
        }

        private static void Expire(Queue<DateTime> sent, DateTime now)
        {
            while (sent.Count > 0 && now - sent.Peek() >= Window)
            {
                sent.Dequeue();
            }
        }
    }
}
