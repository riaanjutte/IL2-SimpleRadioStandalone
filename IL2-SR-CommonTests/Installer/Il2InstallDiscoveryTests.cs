using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Installer
{
    [TestClass]
    public class Il2InstallDiscoveryTests
    {
        [TestMethod]
        public void InstalledGameIsDetectedWhenStartupConfigIsMissing()
        {
            string root = CreateInstallRoot("Il-2.exe");

            try
            {
                var installs = global::Installer.Il2InstallDiscovery.FindInstalledGames(root);

                Assert.IsTrue(installs.Exists(install =>
                    string.Equals(install.InstallPath, root, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void InstalledKoreaGameIsDetectedWhenStartupConfigIsMissing()
        {
            string root = CreateInstallRoot("IL2Series.exe");

            try
            {
                var installs = global::Installer.Il2InstallDiscovery.FindInstalledGames(root);

                Assert.IsTrue(installs.Exists(install =>
                    string.Equals(install.InstallPath, root, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [TestMethod]
        public void ArbitraryDataDirectoryIsNotDetectedAsInstalledGame()
        {
            string root = Path.Combine(Path.GetTempPath(), "IL2-SRS-Discovery-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "data"));

            try
            {
                var installs = global::Installer.Il2InstallDiscovery.FindInstalledGames(root);

                Assert.IsFalse(installs.Exists(install =>
                    string.Equals(install.InstallPath, root, StringComparison.OrdinalIgnoreCase)));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        private static string CreateInstallRoot(string executableName)
        {
            string root = Path.Combine(Path.GetTempPath(), "IL2-SRS-Discovery-" + Guid.NewGuid().ToString("N"));
            string gameDirectory = Path.Combine(root, "bin", "game");
            Directory.CreateDirectory(Path.Combine(root, "data"));
            Directory.CreateDirectory(gameDirectory);
            File.WriteAllBytes(Path.Combine(gameDirectory, executableName), new byte[] { 0 });
            return root;
        }
    }
}
