using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Installer
{
    [TestClass]
    public class SrsInstallConsolidatorTests
    {
        [TestMethod]
        public void MigrationPreservesProfilesConflictsFavouritesAndBackups()
        {
            string root = Path.Combine(Path.GetTempPath(), "il2-srs-consolidation-" + Guid.NewGuid().ToString("N"));
            string sourceA = Path.Combine(root, "sourceA");
            string sourceB = Path.Combine(root, "sourceB");
            string destination = Path.Combine(root, "destination");
            string userData = Path.Combine(root, "userdata");

            try
            {
                Directory.CreateDirectory(sourceA);
                Directory.CreateDirectory(sourceB);
                Directory.CreateDirectory(destination);

                Write(Path.Combine(sourceA, "IL2-SR-ClientRadio.exe"), "dummy");
                Write(Path.Combine(sourceB, "IL2-SR-ClientRadio.exe"), "dummy");
                Write(Path.Combine(sourceA, "global.cfg"),
                    "[Client Settings]\r\nSettingsProfiles={default,pilot}\r\nTheme=A");
                Write(Path.Combine(sourceA, "default.cfg"), "A-default");
                Write(Path.Combine(sourceA, "pilot.cfg"), "A-pilot-bindings");
                Write(Path.Combine(sourceA, "FavouriteServers.csv"), "A,a.example:6002,True\r\n");

                Write(Path.Combine(sourceB, "global.cfg"),
                    "[Client Settings]\r\nSettingsProfiles={default,korea}\r\nTheme=B");
                Write(Path.Combine(sourceB, "default.cfg"), "B-default");
                Write(Path.Combine(sourceB, "korea.cfg"), "B-korea-bindings");
                Write(Path.Combine(sourceB, "FavouriteServers.csv"), "B,b.example:7002,False\r\n");

                var plan = new global::Installer.SrsConsolidationPlan(
                    destination,
                    new[] { sourceA, sourceB });

                global::Installer.SrsConsolidationResult result =
                    global::Installer.SrsInstallConsolidator.MigrateUserDataTo(
                        plan,
                        sourceA,
                        userData,
                        null);

                string globalConfig = File.ReadAllText(Path.Combine(userData, "global.cfg"));
                StringAssert.Contains(globalConfig, "Theme=A");
                StringAssert.Contains(globalConfig, "default");
                StringAssert.Contains(globalConfig, "pilot");
                StringAssert.Contains(globalConfig, "korea");
                StringAssert.Contains(globalConfig, "default-from-sourceB");

                Assert.AreEqual("A-pilot-bindings", File.ReadAllText(Path.Combine(userData, "pilot.cfg")));
                Assert.AreEqual("B-korea-bindings", File.ReadAllText(Path.Combine(userData, "korea.cfg")));
                Assert.AreEqual("B-default", File.ReadAllText(Path.Combine(userData, "default-from-sourceB.cfg")));
                Assert.AreEqual(2, File.ReadAllLines(Path.Combine(userData, "FavouriteServers.csv")).Length);
                Assert.IsTrue(Directory.Exists(result.BackupPath));
                Assert.IsTrue(File.Exists(Path.Combine(userData, ".legacy-migration-complete")));

                int retired = global::Installer.SrsInstallConsolidator.RetireDuplicateInstallations(plan, null);
                Assert.AreEqual(2, retired);
                Assert.IsFalse(File.Exists(Path.Combine(sourceA, "IL2-SR-ClientRadio.exe")));
                Assert.IsFalse(File.Exists(Path.Combine(sourceB, "IL2-SR-ClientRadio.exe")));
                Assert.IsTrue(File.Exists(Path.Combine(sourceA, "global.cfg")));
                Assert.IsTrue(File.Exists(Path.Combine(sourceB, "global.cfg")));

                var subsequentUpdate = new global::Installer.SrsConsolidationPlan(
                    destination,
                    new string[0]);
                global::Installer.SrsConsolidationResult subsequentResult =
                    global::Installer.SrsInstallConsolidator.MigrateUserDataTo(
                        subsequentUpdate,
                        destination,
                        userData,
                        null);
                Assert.IsNull(subsequentResult.BackupPath);
                Assert.AreEqual(1, File.ReadAllText(Path.Combine(userData, "global.cfg"))
                    .Split(new[] { "default-from-sourceB" }, StringSplitOptions.None).Length - 1);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, true);
                }
            }
        }

        private static void Write(string path, string value)
        {
            File.WriteAllText(path, value);
        }
    }
}
