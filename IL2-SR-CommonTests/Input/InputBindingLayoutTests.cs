using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
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

            Assert.IsTrue(InputBinding.Radio1ChannelUp <= InputBinding.RestartSrs);
            Assert.IsTrue(InputBinding.Radio1ChannelDown <= InputBinding.RestartSrs);
            Assert.IsTrue(InputBinding.Radio2ChannelUp <= InputBinding.RestartSrs);
            Assert.IsTrue(InputBinding.Radio2ChannelDown <= InputBinding.RestartSrs);
        }

        [TestMethod]
        public void RadioSpecificChannelModifiersUseStandardOffset()
        {
            Assert.AreEqual((int)InputBinding.Radio1ChannelUp + 100, (int)InputBinding.ModifierRadio1ChannelUp);
            Assert.AreEqual((int)InputBinding.Radio1ChannelDown + 100, (int)InputBinding.ModifierRadio1ChannelDown);
            Assert.AreEqual((int)InputBinding.Radio2ChannelUp + 100, (int)InputBinding.ModifierRadio2ChannelUp);
            Assert.AreEqual((int)InputBinding.Radio2ChannelDown + 100, (int)InputBinding.ModifierRadio2ChannelDown);
        }

        [TestMethod]
        public void FixedRadioMuteBindingsAreInsideScannedInputRangeAndHaveModifiers()
        {
            Assert.IsTrue(InputBinding.ToggleRadio1Mute >= InputBinding.Intercom);
            Assert.IsTrue(InputBinding.ToggleRadio1Mute <= InputBinding.ToggleRadio2Mute);
            Assert.IsTrue(InputBinding.ToggleRadio2Mute > InputBinding.TogglePilotRoster);
            Assert.AreEqual((int)InputBinding.ToggleRadio1Mute + 100, (int)InputBinding.ModifierToggleRadio1Mute);
            Assert.AreEqual((int)InputBinding.ToggleRadio2Mute + 100, (int)InputBinding.ModifierToggleRadio2Mute);
        }

        [TestMethod]
        public void FixedRadioMuteBindingsReachRadioControlDispatcher()
        {
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.ToggleRadio1Mute));
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.ToggleRadio2Mute));
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.ToggleSelectedRadioMute));
            Assert.IsFalse(InputDeviceManager.IsRadioControlBinding(InputBinding.TogglePilotRoster));
            Assert.IsFalse(InputDeviceManager.IsRadioControlBinding(InputBinding.Ptt2));
        }

        [TestMethod]
        public void VolumeButtonBindingsAreInsideScannedInputRangeAndHaveModifiers()
        {
            Assert.IsTrue(InputBinding.Radio1VolumeUp > InputBinding.IntercomVolumeAxis);
            Assert.IsTrue(InputBinding.IntercomVolumeDown < InputBinding.ModifierIntercom);
            Assert.AreEqual((int)InputBinding.Radio1VolumeUp + 100, (int)InputBinding.ModifierRadio1VolumeUp);
            Assert.AreEqual((int)InputBinding.Radio1VolumeDown + 100, (int)InputBinding.ModifierRadio1VolumeDown);
            Assert.AreEqual((int)InputBinding.Radio2VolumeUp + 100, (int)InputBinding.ModifierRadio2VolumeUp);
            Assert.AreEqual((int)InputBinding.Radio2VolumeDown + 100, (int)InputBinding.ModifierRadio2VolumeDown);
            Assert.AreEqual((int)InputBinding.IntercomVolumeUp + 100, (int)InputBinding.ModifierIntercomVolumeUp);
            Assert.AreEqual((int)InputBinding.IntercomVolumeDown + 100, (int)InputBinding.ModifierIntercomVolumeDown);
        }

        [TestMethod]
        public void VolumeButtonBindingsReachRadioControlDispatcher()
        {
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.Radio1VolumeUp));
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.Radio2VolumeDown));
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.IntercomVolumeUp));
            Assert.IsTrue(InputDeviceManager.IsRadioControlBinding(InputBinding.IntercomVolumeDown));
            Assert.IsFalse(InputDeviceManager.IsRadioControlBinding(InputBinding.IntercomVolumeAxis));
        }

        [TestMethod]
        public void RestartSrsBindingIsInsideScannedInputRange()
        {
            Assert.IsTrue(InputBinding.RestartSrs >= InputBinding.Intercom);
            Assert.IsTrue(InputBinding.RestartSrs >= InputBinding.Radio2ChannelDown);
        }

        [TestMethod]
        public void RestartSrsModifierUsesStandardOffset()
        {
            Assert.AreEqual((int)InputBinding.RestartSrs + 100, (int)InputBinding.ModifierRestartSrs);
        }

        [TestMethod]
        public void AdditionalPttBindingsAreInsideScannedInputRange()
        {
            Assert.IsTrue(InputBinding.Ptt2 >= InputBinding.Intercom);
            Assert.IsTrue(InputBinding.Ptt2 <= InputBinding.Ptt3);
            Assert.IsTrue(InputBinding.Ptt3 >= InputBinding.RestartSrs);
        }

        [TestMethod]
        public void AdditionalPttModifiersUseStandardOffset()
        {
            Assert.AreEqual((int)InputBinding.Ptt2 + 100, (int)InputBinding.ModifierPtt2);
            Assert.AreEqual((int)InputBinding.Ptt3 + 100, (int)InputBinding.ModifierPtt3);
        }

        [TestMethod]
        public void AllCommonPttSlotsAreRecognizedAsTransmitBindings()
        {
            Assert.IsTrue(UdpVoiceHandler.IsCommonPttBinding(InputBinding.Ptt));
            Assert.IsTrue(UdpVoiceHandler.IsCommonPttBinding(InputBinding.Ptt2));
            Assert.IsTrue(UdpVoiceHandler.IsCommonPttBinding(InputBinding.Ptt3));
            Assert.IsFalse(UdpVoiceHandler.IsCommonPttBinding(InputBinding.Intercom));
        }
    }
}
