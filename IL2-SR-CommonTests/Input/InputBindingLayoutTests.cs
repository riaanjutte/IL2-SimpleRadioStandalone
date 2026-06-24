using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Input
{
    [TestClass]
    public class InputBindingLayoutTests
    {
        [TestMethod]
        public void RadioSpecificChannelBindingsAreInsideScannedInputRange()
        {
            Assert.IsTrue(InputBinding.Radio1ChannelUp >= InputBinding.Intercom);
            Assert.IsTrue(InputBinding.Radio1ChannelDown >= InputBinding.Intercom);
            Assert.IsTrue(InputBinding.Radio2ChannelUp >= InputBinding.Intercom);
            Assert.IsTrue(InputBinding.Radio2ChannelDown >= InputBinding.Intercom);

            Assert.IsTrue(InputBinding.Radio1ChannelUp <= InputBinding.Radio2ChannelDown);
            Assert.IsTrue(InputBinding.Radio1ChannelDown <= InputBinding.Radio2ChannelDown);
            Assert.IsTrue(InputBinding.Radio2ChannelUp <= InputBinding.Radio2ChannelDown);
        }

        [TestMethod]
        public void RadioSpecificChannelModifiersUseStandardOffset()
        {
            Assert.AreEqual((int)InputBinding.Radio1ChannelUp + 100, (int)InputBinding.ModifierRadio1ChannelUp);
            Assert.AreEqual((int)InputBinding.Radio1ChannelDown + 100, (int)InputBinding.ModifierRadio1ChannelDown);
            Assert.AreEqual((int)InputBinding.Radio2ChannelUp + 100, (int)InputBinding.ModifierRadio2ChannelUp);
            Assert.AreEqual((int)InputBinding.Radio2ChannelDown + 100, (int)InputBinding.ModifierRadio2ChannelDown);
        }
    }
}
