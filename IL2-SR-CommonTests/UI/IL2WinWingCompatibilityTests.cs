using System;
using System.IO;
using System.Text;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class IL2WinWingCompatibilityTests
    {
        private const string OriginalConfig =
            "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n" +
            "<configuration>\r\n" +
            "  <applicationSettings>\r\n" +
            "    <IL2WinWing.Properties.Settings>\r\n" +
            "      <setting name=\"IL2TelemetryPort\" serializeAs=\"String\"><value>4322</value></setting>\r\n" +
            "      <setting name=\"WWPort\" serializeAs=\"String\"><value>29373</value></setting>\r\n" +
            "      <setting name=\"UnrelatedSetting\" serializeAs=\"String\"><value>preserve-me</value></setting>\r\n" +
            "    </IL2WinWing.Properties.Settings>\r\n" +
            "  </applicationSettings>\r\n" +
            "</configuration>\r\n";

        [TestMethod]
        public void RepairConfigSeparatesSrsAndWinWingPortsAndCreatesBackup()
        {
            WithTemporaryConfig(delegate(string path)
            {
                byte[] original = File.ReadAllBytes(path);

                bool changed = IL2WinWingCompatibilityRepair.RepairConfigFile(path, null);

                Assert.IsTrue(changed);
                Assert.AreEqual(29373, IL2WinWingTelemetryDiagnosticProvider.ReadPort(path, "IL2TelemetryPort"));
                Assert.AreEqual(16536, IL2WinWingTelemetryDiagnosticProvider.ReadPort(path, "WWPort"));
                StringAssert.Contains(File.ReadAllText(path), "preserve-me");
                CollectionAssert.AreEqual(original, File.ReadAllBytes(path + ".il2srs.bak"));
            });
        }

        [TestMethod]
        public void RepairConfigRestoresReadOnlyAttribute()
        {
            WithTemporaryConfig(delegate(string path)
            {
                File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

                IL2WinWingCompatibilityRepair.RepairConfigFile(path, null);

                Assert.AreNotEqual(0, File.GetAttributes(path) & FileAttributes.ReadOnly);
                File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
            });
        }

        [TestMethod]
        public void RepairConfigDoesNothingWhenPortsAreAlreadyCorrect()
        {
            WithTemporaryConfig(delegate(string path)
            {
                string text = File.ReadAllText(path)
                    .Replace("<value>29373</value>", "<value>16536</value>")
                    .Replace("<value>4322</value>", "<value>29373</value>");
                File.WriteAllText(path, text, new UTF8Encoding(false));

                bool changed = IL2WinWingCompatibilityRepair.RepairConfigFile(path, null);

                Assert.IsFalse(changed);
                Assert.IsFalse(File.Exists(path + ".il2srs.bak"));
            });
        }

        private static void WithTemporaryConfig(Action<string> test)
        {
            string directory = Path.Combine(Path.GetTempPath(), "il2-srs-winwing-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "IL2WinWing.dll.config");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, OriginalConfig, new UTF8Encoding(false));
                test(path);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }

                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }
    }
}
