using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Installer
{
    [TestClass]
    public class LegacyAssemblyCleanupTests
    {
        [TestMethod]
        public void RemovesOnlyExternalCommonAssembly()
        {
            string directory = Path.Combine(Path.GetTempPath(), "il2-srs-cleanup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string oldAssembly = Path.Combine(directory, "DCS-SR-Common.dll");
                string settings = Path.Combine(directory, "global.cfg");
                File.WriteAllText(oldAssembly, "old dependency");
                File.WriteAllText(settings, "user settings");

                Assert.IsTrue(global::Installer.LegacyAssemblyCleanup.RemoveExternalCommonAssembly(directory));
                Assert.IsFalse(File.Exists(oldAssembly));
                Assert.AreEqual("user settings", File.ReadAllText(settings));
                Assert.IsFalse(global::Installer.LegacyAssemblyCleanup.RemoveExternalCommonAssembly(directory));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
