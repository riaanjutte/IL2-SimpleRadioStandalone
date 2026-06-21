using System;
using System.Collections.Generic;
using System.IO;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Network
{
    public class CombatBoxCallsignProvider
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MissingFileLogInterval = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ReadErrorLogInterval = TimeSpan.FromMinutes(1);

        private readonly ServerSettingsStore _serverSettings;
        private Dictionary<CallsignRosterKey, CombatBoxRosterAssignment> _assignments = new Dictionary<CallsignRosterKey, CombatBoxRosterAssignment>();
        private DateTime _lastRefreshUtc = DateTime.MinValue;
        private DateTime _lastMissingFileLogUtc = DateTime.MinValue;
        private DateTime _lastReadErrorLogUtc = DateTime.MinValue;

        public CombatBoxCallsignProvider(ServerSettingsStore serverSettings)
        {
            _serverSettings = serverSettings;
        }

        public string GetAssignedCallsign(string playerName, int coalition)
        {
            return GetAssignment(playerName, coalition)?.Callsign ?? string.Empty;
        }

        public string GetAssignedVehicle(string playerName, int coalition)
        {
            return GetAssignment(playerName, coalition)?.Vehicle ?? string.Empty;
        }

        public void RefreshIfNeeded()
        {
            if ((DateTime.UtcNow - _lastRefreshUtc) < RefreshInterval)
            {
                return;
            }

            _lastRefreshUtc = DateTime.UtcNow;
            var path = ResolveCurrentStatePath();
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!File.Exists(path))
            {
                _assignments = new Dictionary<CallsignRosterKey, CombatBoxRosterAssignment>();
                LogMissingFile(path);
                return;
            }

            try
            {
                string text;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }

                _assignments = CombatBoxCallsignRoster.ParseAssignments(text);
            }
            catch (Exception ex)
            {
                LogReadError(path, ex);
            }
        }

        private CombatBoxRosterAssignment GetAssignment(string playerName, int coalition)
        {
            RefreshIfNeeded();

            if (string.IsNullOrWhiteSpace(playerName) || coalition <= 0)
            {
                return null;
            }

            return _assignments.TryGetValue(new CallsignRosterKey(playerName, coalition), out var assignment)
                ? assignment
                : null;
        }

        private string ResolveCurrentStatePath()
        {
            var configuredPath = _serverSettings
                .GetGeneralSetting(ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE)
                .StringValue;

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return string.Empty;
            }

            configuredPath = configuredPath.Trim();
            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configuredPath);
        }

        private void LogMissingFile(string path)
        {
            if ((DateTime.UtcNow - _lastMissingFileLogUtc) < MissingFileLogInterval)
            {
                return;
            }

            _lastMissingFileLogUtc = DateTime.UtcNow;
            Logger.Info($"Assigned callsigns JSON file not found: {path}");
        }

        private void LogReadError(string path, Exception ex)
        {
            if ((DateTime.UtcNow - _lastReadErrorLogUtc) < ReadErrorLogInterval)
            {
                return;
            }

            _lastReadErrorLogUtc = DateTime.UtcNow;
            Logger.Warn(ex, $"Unable to read assigned callsigns JSON file. Keeping previous callsign map. Path: {path}");
        }
    }
}
