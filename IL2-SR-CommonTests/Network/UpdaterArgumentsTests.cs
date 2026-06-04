using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class UpdaterArgumentsTests
    {
        [TestMethod]
        public void BuildWithBetaAndTagUsesSharedContract()
        {
            var arguments = UpdaterArguments.Build(true, "v1.0.4.5-beta.2");

            Assert.AreEqual("-beta -tag \"v1.0.4.5-beta.2\"", arguments);
        }

        [TestMethod]
        public void BuildWithoutBetaOrTagReturnsEmptyString()
        {
            var arguments = UpdaterArguments.Build(false, null);

            Assert.AreEqual(string.Empty, arguments);
        }

        [TestMethod]
        public void ParseReadsBetaAndSeparateTag()
        {
            var arguments = UpdaterArguments.Parse(new[]
            {
                "IL2-SRS-AutoUpdater.exe",
                "-beta",
                "-tag",
                "v1.0.4.5-beta.2"
            });

            Assert.IsTrue(arguments.Beta);
            Assert.AreEqual("v1.0.4.5-beta.2", arguments.ReleaseTag);
        }

        [TestMethod]
        public void ParseReadsTagEqualsForms()
        {
            Assert.AreEqual("v1.0.4.5-beta.2", UpdaterArguments.Parse(new[] {"-tag=v1.0.4.5-beta.2"}).ReleaseTag);
            Assert.AreEqual("v1.0.4.5-beta.2", UpdaterArguments.Parse(new[] {"--tag=v1.0.4.5-beta.2"}).ReleaseTag);
            Assert.AreEqual("v1.0.4.5-beta.2", UpdaterArguments.Parse(new[] {"/tag=v1.0.4.5-beta.2"}).ReleaseTag);
        }

        [TestMethod]
        public void ParseIgnoresMissingTagValue()
        {
            var arguments = UpdaterArguments.Parse(new[] {"-tag", "-beta"});

            Assert.IsTrue(arguments.Beta);
            Assert.IsNull(arguments.ReleaseTag);
        }
    }
}
