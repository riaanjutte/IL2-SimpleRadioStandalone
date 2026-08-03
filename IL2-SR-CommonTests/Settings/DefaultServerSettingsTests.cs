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
