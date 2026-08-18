using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Settings
{
    [TestClass]
    public class DefaultServerSettingsTests
    {
        [TestMethod]
        public void AssignedCallsignRosterPathIsNotConfiguredByDefault()
        {
            string settingName = ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE.ToString();

            Assert.IsTrue(DefaultServerSettings.Defaults.ContainsKey(settingName));
            Assert.AreEqual(string.Empty, DefaultServerSettings.Defaults[settingName]);
        }

        [TestMethod]
        public void ChannelNameSettingsBuildAndParseIndependentKeys()
        {
            var settingName = ChannelNameSettings.GetSyncedSettingName(2);

            Assert.AreEqual("CHANNEL_NAME_2", settingName);
            Assert.IsTrue(ChannelNameSettings.TryParseSyncedSettingName(settingName, out var channel));
            Assert.AreEqual(2, channel);
            Assert.IsFalse(ChannelNameSettings.TryParseSyncedSettingName("CHANNEL_NAME_26", out _));
            Assert.IsFalse(ChannelNameSettings.TryParseSyncedSettingName("CHANNEL_NAME_INVALID", out _));
        }

        [TestMethod]
        public void SquadChannelLabelsAreOptInByDefault()
        {
            Assert.AreEqual("false",
                DefaultServerSettings.Defaults[ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS.ToString()]);
        }

        [TestMethod]
        public void ServerUiThemeDefaultsToWhiteAndIsServerOnly()
        {
            var settingName = ServerSettingsKeys.SERVER_UI_THEME.ToString();

            Assert.AreEqual("White", DefaultServerSettings.Defaults[settingName]);
            Assert.IsTrue(DefaultServerSettings.ServerSectionSettings.Contains(settingName));
        }

        [TestMethod]
        public void ChannelNamesAreNormalizedAndLimited()
        {
            var name = ChannelNameSettings.NormalizeName(
                "  Command\r\n" + new string('A', ChannelNameSettings.MaximumNameLength + 10));

            Assert.AreEqual(ChannelNameSettings.MaximumNameLength, name.Length);
            Assert.IsFalse(name.Contains("\r"));
            Assert.IsFalse(name.Contains("\n"));
        }

        [TestMethod]
        public void SyncedChannelNamesAreIndependentAndDoNotCarryAcrossServers()
        {
            var settings = new SyncedServerSettings();
            settings.Decode(new Dictionary<string, string>
            {
                {ChannelNameSettings.GetSyncedSettingName(1), "Command"},
                {ChannelNameSettings.GetSyncedSettingName(2), "Tower/ATC"}
            });

            Assert.AreEqual("Command", settings.GetChannelName(1));
            Assert.AreEqual("Tower/ATC", settings.GetChannelName(2));

            settings.Decode(new Dictionary<string, string>());

            Assert.IsNull(settings.GetChannelName(1));
            Assert.IsNull(settings.GetChannelName(2));
        }

        [TestMethod]
        public void SquadChannelLabelCapabilityDoesNotCarryAcrossServers()
        {
            var settings = new SyncedServerSettings();
            settings.Decode(new Dictionary<string, string>
            {
                {ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS.ToString(), "true"}
            });

            Assert.IsTrue(settings.GetOptionalSettingAsBool(ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS));

            settings.Decode(new Dictionary<string, string>());

            Assert.IsFalse(settings.GetOptionalSettingAsBool(ServerSettingsKeys.SHOW_SQUAD_CHANNEL_LABELS));
        }

        [TestMethod]
        public void EveryPersistentServerSettingHasADefault()
        {
            foreach (ServerSettingsKeys key in Enum.GetValues(typeof(ServerSettingsKeys)))
            {
                if (key == ServerSettingsKeys.PILOT_ROSTER_DATA_AVAILABLE)
                {
                    continue;
                }

                Assert.IsTrue(DefaultServerSettings.Defaults.ContainsKey(key.ToString()),
                    $"Missing persistent default for {key}");
            }
        }

        [TestMethod]
        public void ServerSectionSettingsContainOnlyPersistentDefaults()
        {
            foreach (string key in DefaultServerSettings.ServerSectionSettings)
            {
                Assert.IsTrue(DefaultServerSettings.Defaults.ContainsKey(key),
                    $"Server-section setting {key} has no default");
            }
        }

        [TestMethod]
        public void ServerOnlyPathsAndCredentialsAreNotSyncedToClients()
        {
            var settings = new Dictionary<string, string>
            {
                { ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString(), "248.22" },
                { ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE.ToString(), @"C:\private\roster.json" },
                { ServerSettingsKeys.DSERVER_RCON_ADDRESS.ToString(), "127.0.0.1:8991" },
                { ServerSettingsKeys.DSERVER_RCON_USERNAME.ToString(), "admin" },
                { ServerSettingsKeys.DSERVER_RCON_PASSWORD.ToString(), "secret" }
            };

            SyncedServerSettingsFilter.RemoveServerOnlySettings(settings);

            Assert.AreEqual(1, settings.Count);
            Assert.AreEqual("248.22", settings[ServerSettingsKeys.GLOBAL_LOBBY_FREQUENCIES.ToString()]);
        }
    }
}
