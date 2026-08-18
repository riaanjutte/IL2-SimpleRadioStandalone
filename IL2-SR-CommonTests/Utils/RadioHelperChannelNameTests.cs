using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Utils
{
    [TestClass]
    public class RadioHelperChannelNameTests
    {
        [TestMethod]
        public void ConfiguredChannelOneNameIsPreservedWithoutFriendlyRci()
        {
            Assert.AreEqual(
                "Command",
                RadioHelper.ResolveChannelName(1, "Command", false, "CHN 1"));
        }

        [TestMethod]
        public void FriendlyRciOverridesConfiguredChannelOneName()
        {
            Assert.AreEqual(
                "RCI Control",
                RadioHelper.ResolveChannelName(1, "Command", true, "CHN 1"));
        }

        [TestMethod]
        public void FriendlyRciDoesNotOverrideOtherChannels()
        {
            Assert.AreEqual(
                "Tower/ATC",
                RadioHelper.ResolveChannelName(2, "Tower/ATC", true, "CHN 2"));
        }

        [TestMethod]
        public void DefaultChannelNameIsUsedWithoutConfigurationOrFriendlyRci()
        {
            Assert.AreEqual(
                "CHN 1",
                RadioHelper.ResolveChannelName(1, null, false, "CHN 1"));
        }
    }
}
