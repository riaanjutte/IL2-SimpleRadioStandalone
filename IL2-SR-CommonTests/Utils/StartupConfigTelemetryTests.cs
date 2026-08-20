using System;
using System.IO;
using System.Text;
using ClientStartupConfigTelemetry =
    Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.StartupConfigTelemetry;
using InstallerStartupConfigTelemetry = Installer.StartupConfigTelemetry;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Utils
{
    [TestClass]
    public class StartupConfigTelemetryTests
    {
        private const string OriginalConfig =
            "[KEY = account]\r\n" +
            "\tlogin = \"pilot@example.com\"\r\n" +
            "[END]\r\n\r\n" +
            "[KEY = graphics]\r\n" +
            "\tbloom_enable = 0\r\n" +
            "\tfullscreen = 1\r\n" +
            "[END]\r\n";

        [TestMethod]
        public void ClientRepairPreservesNoBomAndCreatesExactBackup()
        {
            WithTemporaryConfig(new UTF8Encoding(false).GetBytes(OriginalConfig), delegate(string path)
            {
                bool changed = ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => true);

                Assert.IsTrue(changed);
                CollectionAssert.AreEqual(
                    new UTF8Encoding(false).GetBytes(OriginalConfig),
                    File.ReadAllBytes(path + ".il2srs.bak"));

                byte[] updated = File.ReadAllBytes(path);
                Assert.IsFalse(HasUtf8Bom(updated));
                StringAssert.StartsWith(Encoding.UTF8.GetString(updated), OriginalConfig);
                Assert.IsTrue(ClientStartupConfigTelemetry.IsEnabled(path));
            });
        }

        [TestMethod]
        public void ClientRepairPreservesExistingUtf8Bom()
        {
            byte[] preamble = new UTF8Encoding(true).GetPreamble();
            byte[] body = new UTF8Encoding(false).GetBytes(OriginalConfig);
            byte[] original = Combine(preamble, body);

            WithTemporaryConfig(original, delegate(string path)
            {
                ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => true);

                Assert.IsTrue(HasUtf8Bom(File.ReadAllBytes(path)));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path + ".il2srs.bak"));
            });
        }

        [TestMethod]
        public void ClientRepairDoesNotTouchFileWhenWritesAreBlocked()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(OriginalConfig);

            WithTemporaryConfig(original, delegate(string path)
            {
                try
                {
                    ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => false);
                    Assert.Fail("Expected the repair to be deferred.");
                }
                catch (InvalidOperationException)
                {
                }

                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.IsFalse(File.Exists(path + ".il2srs.bak"));
            });
        }

        [TestMethod]
        public void ClientRepairRestoresReadOnlyAttribute()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(OriginalConfig);

            WithTemporaryConfig(original, delegate(string path)
            {
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
                ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => true);

                Assert.AreNotEqual(
                    0,
                    File.GetAttributes(path) & FileAttributes.ReadOnly,
                    "SRS must restore a user's read-only protection.");

                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            });
        }

        [TestMethod]
        public void ClientRepairAddsSeparateThirdPartyEndpointWithoutReplacingSrs()
        {
            const string config =
                "[KEY = telemetrydevice]\r\n" +
                "\taddr = \"127.0.0.1\"\r\n" +
                "\tdecimation = 2\r\n" +
                "\tenable = true\r\n" +
                "\tport = 4322\r\n" +
                "[END]\r\n";

            bool changed;
            string updated = ClientStartupConfigTelemetry.EnsureEndpointInText(
                config,
                "127.0.0.1",
                29373,
                out changed);

            Assert.IsTrue(changed);
            StringAssert.Contains(updated, "addr = \"127.0.0.1\"");
            StringAssert.Contains(updated, "port = 4322");
            StringAssert.Contains(updated, "addr1 = \"127.0.0.1:29373\"");
        }

        [TestMethod]
        public void ClientRepairDoesNotDuplicateSeparateThirdPartyEndpoint()
        {
            const string config =
                "[KEY = telemetrydevice]\r\n" +
                "\taddr = \"127.0.0.1\"\r\n" +
                "\taddr1 = \"127.0.0.1:29373\"\r\n" +
                "\tdecimation = 2\r\n" +
                "\tenable = true\r\n" +
                "\tport = 4322\r\n" +
                "[END]\r\n";

            bool changed;
            string updated = ClientStartupConfigTelemetry.EnsureEndpointInText(
                config,
                "127.0.0.1",
                29373,
                out changed);

            Assert.IsFalse(changed);
            Assert.AreEqual(config, updated);
        }

        [TestMethod]
        public void InstallerRepairAlsoPreservesNoBomAndBackup()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(OriginalConfig);

            WithTemporaryConfig(original, delegate(string path)
            {
                InstallerStartupConfigTelemetry.EnsureEnabled(path, null, () => true);

                Assert.IsFalse(HasUtf8Bom(File.ReadAllBytes(path)));
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path + ".il2srs.bak"));
                Assert.IsTrue(InstallerStartupConfigTelemetry.IsEnabled(path));
            });
        }

        [TestMethod]
        public void ClientRepairRejectsEmptyConfigWithoutWriting()
        {
            WithTemporaryConfig(new byte[0], delegate(string path)
            {
                AssertInvalidData(delegate
                {
                    ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => true);
                });

                Assert.AreEqual(0, new FileInfo(path).Length);
                Assert.IsFalse(File.Exists(path + ".il2srs.bak"));
                Assert.IsFalse(File.Exists(path + ".il2srs.lastgood"));
            });
        }

        [TestMethod]
        public void InstallerRepairRejectsIncompleteConfigWithoutWriting()
        {
            byte[] incomplete = new UTF8Encoding(false).GetBytes(
                "[KEY = account]\r\n\tlogin = \"pilot@example.com\"\r\n[END]\r\n");

            WithTemporaryConfig(incomplete, delegate(string path)
            {
                AssertInvalidData(delegate
                {
                    InstallerStartupConfigTelemetry.EnsureEnabled(path, null, () => true);
                });

                CollectionAssert.AreEqual(incomplete, File.ReadAllBytes(path));
                Assert.IsFalse(File.Exists(path + ".il2srs.lastgood"));
            });
        }

        [TestMethod]
        public void ClientRepairCreatesAndRefreshesLastGoodBackup()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(OriginalConfig);

            WithTemporaryConfig(original, delegate(string path)
            {
                ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => true);
                CollectionAssert.AreEqual(File.ReadAllBytes(path), File.ReadAllBytes(path + ".il2srs.lastgood"));

                string current = File.ReadAllText(path)
                                 + "\r\n[KEY = sound]\r\n\tvolume = 0.75\r\n[END]\r\n";
                File.WriteAllText(path, current, new UTF8Encoding(false));

                bool changed = ClientStartupConfigTelemetry.EnsureEnabled(path, null, () => true);

                Assert.IsFalse(changed);
                CollectionAssert.AreEqual(File.ReadAllBytes(path), File.ReadAllBytes(path + ".il2srs.lastgood"));
            });
        }

        [TestMethod]
        public void ClientRecoveryRestoresEmptyReadOnlyConfigAndPreservesDamagedFile()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(OriginalConfig);

            WithTemporaryConfig(new byte[0], delegate(string path)
            {
                File.WriteAllBytes(path + ".il2srs.lastgood", original);
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

                string restoredFrom = ClientStartupConfigTelemetry.RestoreRecoveryBackup(path, null, () => true);

                Assert.AreEqual(path + ".il2srs.lastgood", restoredFrom);
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
                Assert.AreNotEqual(0, File.GetAttributes(path) & FileAttributes.ReadOnly);
                Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(path), "startup.cfg.il2srs.damaged-*.bak").Length);

                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            });
        }

        [TestMethod]
        public void ClientRecoveryRestoresMissingConfig()
        {
            byte[] original = new UTF8Encoding(false).GetBytes(OriginalConfig);

            WithTemporaryConfig(original, delegate(string path)
            {
                File.WriteAllBytes(path + ".il2srs.lastgood", original);
                File.Delete(path);

                ClientStartupConfigTelemetry.RestoreRecoveryBackup(path, null, () => true);

                CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void WithTemporaryConfig(byte[] contents, Action<string> test)
        {
            string directory = Path.Combine(
                Path.GetTempPath(),
                "il2-srs-startup-config-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "startup.cfg");

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, contents);
                test(path);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3
                   && bytes[0] == 0xEF
                   && bytes[1] == 0xBB
                   && bytes[2] == 0xBF;
        }

        private static void AssertInvalidData(Action action)
        {
            try
            {
                action();
                Assert.Fail("Expected startup.cfg validation to reject the file.");
            }
            catch (InvalidDataException)
            {
            }
        }

        private static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] combined = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, combined, 0, first.Length);
            Buffer.BlockCopy(second, 0, combined, first.Length, second.Length);
            return combined;
        }
    }
}
