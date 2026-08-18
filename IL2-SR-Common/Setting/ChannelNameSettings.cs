using System;
using System.Globalization;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Setting
{
    public static class ChannelNameSettings
    {
        public const string SectionName = "Channel Names";
        public const string SyncedSettingPrefix = "CHANNEL_NAME_";
        public const int MaximumChannel = 25;
        public const int MaximumNameLength = 32;

        public static string GetSyncedSettingName(int channel)
        {
            return SyncedSettingPrefix + channel.ToString(CultureInfo.InvariantCulture);
        }

        public static bool TryParseSyncedSettingName(string settingName, out int channel)
        {
            channel = 0;
            if (string.IsNullOrWhiteSpace(settingName) ||
                !settingName.StartsWith(SyncedSettingPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(settingName.Substring(SyncedSettingPrefix.Length), NumberStyles.Integer,
                       CultureInfo.InvariantCulture, out channel) &&
                   channel >= 1 && channel <= MaximumChannel;
        }

        public static string NormalizeName(string name)
        {
            var normalized = (name ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

            return normalized.Length > MaximumNameLength
                ? normalized.Substring(0, MaximumNameLength).TrimEnd()
                : normalized;
        }

    }
}
