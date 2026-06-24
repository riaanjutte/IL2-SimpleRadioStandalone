using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.RadioOverlayWindow
{
    public class RciOverlayMessageDelay
    {
        public static readonly TimeSpan DefaultDelay = TimeSpan.FromSeconds(60);

        private readonly TimeSpan _delay;
        private readonly Func<DateTime> _utcNow;
        private string _trackedCallsigns = string.Empty;
        private DateTime _detectedAtUtc = DateTime.MinValue;

        public RciOverlayMessageDelay()
            : this(DefaultDelay, () => DateTime.UtcNow)
        {
        }

        public RciOverlayMessageDelay(TimeSpan delay, Func<DateTime> utcNow)
        {
            _delay = delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public string GetVisibleCallsigns(string currentCallsigns)
        {
            currentCallsigns = (currentCallsigns ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(currentCallsigns))
            {
                Reset();
                return string.Empty;
            }

            if (!string.Equals(_trackedCallsigns, currentCallsigns, StringComparison.OrdinalIgnoreCase))
            {
                _trackedCallsigns = currentCallsigns;
                _detectedAtUtc = _utcNow();
                return string.Empty;
            }

            return _utcNow() - _detectedAtUtc >= _delay
                ? currentCallsigns
                : string.Empty;
        }

        public void Reset()
        {
            _trackedCallsigns = string.Empty;
            _detectedAtUtc = DateTime.MinValue;
        }
    }
}
