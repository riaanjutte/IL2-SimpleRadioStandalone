using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Installer
{
    [TestClass]
    public class SteamGameDiscoveryTests
    {
        private string _library;

        [TestInitialize]
        public void CreateLibrary()
        {
            _library = Path.Combine(Path.GetTempPath(), "SrsSteam-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_library, "steamapps", "common"));
        }

        [TestCleanup]
        public void RemoveLibrary()
        {
            Directory.Delete(_library, true);
        }

        [TestMethod]
        public void SteamIl2SeriesIsDetectedAndClassifiedByInstallerAndClientWithoutManifestOrConfig()
        {
            string root = CreateGame("IL2Series", "IL2Series.exe");
            AssertBothDiscoverKorea(root);
        }

        [TestMethod]
        public void ManifestFindsRenamedKoreaFolderUsingExecutable()
        {
            string root = CreateGame("Flight Simulation", "IL2Series.exe");
            WriteManifest("1", "Flight Simulation");
            AssertBothDiscoverKorea(root);
        }

        [TestMethod]
        public void ManifestFindsNestedGameFolderAndGreatBattles()
        {
            string korea = CreateGame(Path.Combine("Flight Simulation", "Game"), "IL2Series.exe");
            string greatBattles = CreateGame("WWII Flight", "Il-2.exe");
            WriteManifest("1", "Flight Simulation");
            WriteManifest("2", "WWII Flight");
            WriteManifest("3", "Flight Simulation");

            CollectionAssert.AreEquivalent(new[] { korea, greatBattles },
                Il2SteamInstallDiscovery.FindManifestInstallRoots(_library).ToArray());
            AssertBothDiscoverKorea(korea);
        }

        [TestMethod]
        public void ManifestRejectsUnrelatedGamesMalformedEntriesAndEscapingPaths()
        {
            CreateGame("Other Game", "Other.exe");
            WriteManifest("1", "Other Game");
            File.WriteAllText(Path.Combine(_library, "steamapps", "appmanifest_2.acf"), "invalid manifest");
            string outside = Path.Combine(_library, "outside");
            Directory.CreateDirectory(Path.Combine(outside, "data"));
            Directory.CreateDirectory(Path.Combine(outside, "bin", "game"));
            File.WriteAllBytes(Path.Combine(outside, "bin", "game", "IL2Series.exe"), new byte[0]);
            WriteManifest("3", "../../outside");
            WriteManifest("4", outside);

            Assert.AreEqual(0, Il2SteamInstallDiscovery.FindManifestInstallRoots(_library).Count());
        }

        private void AssertBothDiscoverKorea(string root)
        {
            var installs = new Dictionary<string, global::Installer.Il2Install>(StringComparer.OrdinalIgnoreCase);
            global::Installer.Il2InstallDiscovery.AddSteamCandidates(installs, new[] { _library });
            string config = Path.Combine(root, "data", "startup.cfg");
            Assert.IsTrue(installs.ContainsKey(config), "Installer did not discover " + root);
            Assert.IsTrue(global::Installer.Il2InstallDiscovery.IsKorea(installs[config]));

            var candidates = new Dictionary<string, Il2InstallCandidate>(StringComparer.OrdinalIgnoreCase);
            TelemetryDiagnosticsService.AddSteamCandidates(candidates, new[] { _library });
            Assert.IsTrue(candidates.ContainsKey(config), "Client did not discover " + root);
            Assert.AreEqual("IL-2 Korea", candidates[config].DisplayName);
            Assert.IsFalse(File.Exists(config), "Discovery must not create startup.cfg");
        }

        private string CreateGame(string directory, string executable)
        {
            string root = Path.Combine(_library, "steamapps", "common", directory);
            Directory.CreateDirectory(Path.Combine(root, "data"));
            Directory.CreateDirectory(Path.Combine(root, "bin", "game"));
            File.WriteAllBytes(Path.Combine(root, "bin", "game", executable), new byte[0]);
            return root;
        }

        private void WriteManifest(string id, string directory)
        {
            File.WriteAllText(Path.Combine(_library, "steamapps", "appmanifest_" + id + ".acf"),
                "\"AppState\"\n{\n\t\"installdir\"\t\t\"" + directory + "\"\n}\n");
        }
    }
}
