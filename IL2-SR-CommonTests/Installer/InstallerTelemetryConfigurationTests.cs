using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Installer
{
    [TestClass]
    public class InstallerTelemetryConfigurationTests
    {
        [TestMethod]
        public void DamagedConfigProducesWarningAndAllowsOtherInstallsToContinue()
        {
            var damaged = new global::Installer.Il2Install("Great Battles", @"D:\Great Battles");
            var healthy = new global::Installer.Il2Install("IL-2 Korea", @"D:\Korea");
            var configured = new List<global::Installer.Il2Install>();
            var log = new List<string>();

            global::Installer.TelemetryConfigurationResult result =
                global::Installer.InstallerTelemetryConfiguration.Configure(
                    new[] { damaged, healthy },
                    install =>
                    {
                        if (ReferenceEquals(install, damaged))
                        {
                            throw new InvalidDataException("startup.cfg is incomplete");
                        }

                        configured.Add(install);
                    },
                    log.Add);

            Assert.AreEqual(2, result.DetectedInstalls.Count);
            Assert.AreEqual(1, result.ConfiguredInstalls.Count);
            Assert.AreSame(healthy, result.ConfiguredInstalls[0]);
            Assert.AreEqual(1, configured.Count);
            Assert.AreEqual(1, result.Warnings.Count);
            Assert.AreEqual(damaged.StartupConfigPath, result.Warnings[0].StartupConfigPath);
            StringAssert.Contains(log[0], "SRS binary installation will continue");
        }

        [TestMethod]
        public void MissingConfigProducesWarningInsteadOfFailingInstallation()
        {
            var install = new global::Installer.Il2Install("Great Battles", @"D:\Great Battles");

            global::Installer.TelemetryConfigurationResult result =
                global::Installer.InstallerTelemetryConfiguration.Configure(
                    new[] { install },
                    ignored => { throw new FileNotFoundException("startup.cfg missing"); },
                    null);

            Assert.AreEqual(0, result.ConfiguredInstalls.Count);
            Assert.AreEqual(1, result.Warnings.Count);
        }

        [TestMethod]
        public void UnrelatedIoFailureRemainsFatal()
        {
            var install = new global::Installer.Il2Install("Great Battles", @"D:\Great Battles");

            try
            {
                global::Installer.InstallerTelemetryConfiguration.Configure(
                    new[] { install },
                    ignored => { throw new IOException("disk failure"); },
                    null);
                Assert.Fail("Expected unrelated installer I/O failure to remain fatal.");
            }
            catch (IOException ex)
            {
                Assert.AreEqual("disk failure", ex.Message);
            }
        }

        [TestMethod]
        public void RunningIl2FailureMakesCloseGameInstructionProminent()
        {
            var exception = new global::Installer.Il2RunningTelemetryConfigurationException(
                @"D:\Great Battles\data\startup.cfg");

            global::Installer.InstallerFailurePresentation presentation =
                global::Installer.InstallerFailurePresentation.FromException(exception);

            Assert.AreEqual("SRS UPDATE BLOCKED - CLOSE IL-2", presentation.Title);
            StringAssert.StartsWith(presentation.Message, "IL-2 IS STILL RUNNING");
            StringAssert.Contains(presentation.Message, "1. Close every running copy of IL-2 Great Battles and IL-2 Korea.");
            StringAssert.Contains(presentation.Message, exception.StartupConfigPath);
            Assert.AreEqual(System.Windows.MessageBoxImage.Warning, presentation.Image);
            Assert.IsFalse(presentation.OpenSupportResources);
        }

        [TestMethod]
        public void UnknownFailureRetainsSupportInstructions()
        {
            global::Installer.InstallerFailurePresentation presentation =
                global::Installer.InstallerFailurePresentation.FromException(
                    new IOException("disk failure"));

            Assert.AreEqual("Installation Error", presentation.Title);
            StringAssert.Contains(presentation.Message, "disk failure");
            StringAssert.Contains(presentation.Message, "installer-log.txt");
            Assert.AreEqual(System.Windows.MessageBoxImage.Error, presentation.Image);
            Assert.IsTrue(presentation.OpenSupportResources);
        }
    }
}
