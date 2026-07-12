using System;
using System.IO;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Settings
{
    [TestClass]
    public class UserDataPathsTests
    {
        [TestMethod]
        public void LegacyMigrationUsesAppDataWithoutOverwritingAndRunsOnce()
        {
            string root = Path.Combine(Path.GetTempPath(), "il2-srs-userdata-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "legacy");
            string target = Path.Combine(root, "appdata");

            try
            {
                Directory.CreateDirectory(source);
                Directory.CreateDirectory(target);

                File.WriteAllText(Path.Combine(source, "global.cfg"), "legacy-global");
                File.WriteAllText(Path.Combine(source, "default.cfg"), "legacy-bindings");
                File.WriteAllText(Path.Combine(source, "FavouriteServers.csv"), "Legacy,server:6002,True");
                File.WriteAllText(Path.Combine(source, "RadioOne.txt"), "1.0");
                File.WriteAllText(Path.Combine(source, "clientlog.txt"), "must-not-migrate");
                File.WriteAllText(Path.Combine(target, "global.cfg"), "existing-appdata-global");

                UserDataPaths.MigrateLegacyUserDataTo(target, new[] { source }, null);

                Assert.AreEqual(
                    "existing-appdata-global",
                    File.ReadAllText(Path.Combine(target, "global.cfg")));
                Assert.AreEqual(
                    "legacy-bindings",
                    File.ReadAllText(Path.Combine(target, "default.cfg")));
                Assert.IsTrue(File.Exists(Path.Combine(target, "FavouriteServers.csv")));
                Assert.IsTrue(File.Exists(Path.Combine(target, "RadioOne.txt")));
                Assert.IsFalse(File.Exists(Path.Combine(target, "clientlog.txt")));
                Assert.IsTrue(File.Exists(Path.Combine(target, ".legacy-migration-complete")));

                string[] backups = Directory.GetFiles(
                    Path.Combine(target, "MigrationBackups"),
                    "global.cfg",
                    SearchOption.AllDirectories);
                Assert.AreEqual(1, backups.Length);
                Assert.AreEqual("legacy-global", File.ReadAllText(backups[0]));

                File.WriteAllText(Path.Combine(source, "default.cfg"), "changed-after-migration");
                UserDataPaths.MigrateLegacyUserDataTo(target, new[] { source }, null);
                Assert.AreEqual(
                    "legacy-bindings",
                    File.ReadAllText(Path.Combine(target, "default.cfg")));
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }
    }
}
