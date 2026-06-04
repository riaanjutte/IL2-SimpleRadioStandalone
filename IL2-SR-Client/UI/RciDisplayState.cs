using System;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    public class RciDisplayState
    {
        private static readonly Brush EnemyBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D60000"));
        private static readonly Brush BothBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFB000"));
        private static readonly Brush NeutralBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"));
        private static readonly Brush NoneBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
        private static readonly Brush OverlayEnemyForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3030"));
        private static readonly Brush OverlayNeutralForeground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8D8D8"));

        public RciDisplayState(RciStatus status, string statusText, string rcoOnDutyText)
        {
            Status = status;
            StatusText = statusText;
            RcoOnDutyText = rcoOnDutyText;
        }

        public RciStatus Status { get; }
        public string StatusText { get; }
        public string RcoOnDutyText { get; }
        public bool HasRcoOnDuty => !string.IsNullOrWhiteSpace(RcoOnDutyText);

        public static RciDisplayState Create(RciStatus status, string friendlyRciCallsigns)
        {
            return new RciDisplayState(
                status,
                GetStatusText(status),
                FormatRcoOnDutyCallsign(friendlyRciCallsigns));
        }

        public static string FormatAssignedCallsign(string assignedCallsign)
        {
            if (string.IsNullOrWhiteSpace(assignedCallsign))
            {
                return string.Empty;
            }

            return FormatLocalized("Callsign: {0}", "Callsign: " + assignedCallsign.Trim(), assignedCallsign.Trim());
        }

        public static string GetRequestCallsignText()
        {
            return LocalizationManager.Get("Request callsign CHN 2");
        }

        public Brush StatusBackground => GetStatusBackground(Status);
        public Brush MainWindowStatusForeground => GetMainWindowStatusForeground(Status);
        public Brush OverlayStatusForeground => GetOverlayStatusForeground(Status);

        private static string FormatRcoOnDutyCallsign(string callsigns)
        {
            if (string.IsNullOrWhiteSpace(callsigns))
            {
                return string.Empty;
            }

            return FormatLocalized("RCO On Duty : {0}", "RCO On Duty : " + callsigns.Trim(), callsigns.Trim());
        }

        private static string FormatLocalized(string key, string fallback, string value)
        {
            var format = LocalizationManager.Get(key);
            try
            {
                return string.Format(format, value);
            }
            catch (FormatException)
            {
                return fallback;
            }
        }

        private static Brush GetStatusBackground(RciStatus status)
        {
            switch (status)
            {
                case RciStatus.FriendlyOnly:
                    return Brushes.Lime;
                case RciStatus.EnemyOnly:
                    return EnemyBrush;
                case RciStatus.Both:
                    return BothBrush;
                case RciStatus.Neutral:
                    return NeutralBrush;
                default:
                    return NoneBrush;
            }
        }

        private static Brush GetMainWindowStatusForeground(RciStatus status)
        {
            switch (status)
            {
                case RciStatus.FriendlyOnly:
                case RciStatus.Both:
                    return Brushes.Black;
                default:
                    return Brushes.White;
            }
        }

        private static Brush GetOverlayStatusForeground(RciStatus status)
        {
            switch (status)
            {
                case RciStatus.FriendlyOnly:
                    return Brushes.Lime;
                case RciStatus.Both:
                    return BothBrush;
                case RciStatus.EnemyOnly:
                    return OverlayEnemyForeground;
                default:
                    return OverlayNeutralForeground;
            }
        }

        private static string GetStatusText(RciStatus status)
        {
            switch (status)
            {
                case RciStatus.FriendlyOnly:
                    return LocalizationManager.Get("Friendly RCI active");
                case RciStatus.EnemyOnly:
                    return LocalizationManager.Get("Enemy RCI active");
                case RciStatus.Both:
                    return LocalizationManager.Get("Both sides have RCI active");
                case RciStatus.Neutral:
                    return LocalizationManager.Get("RCI active");
                default:
                    return LocalizationManager.Get("No RCI active");
            }
        }
    }
}
