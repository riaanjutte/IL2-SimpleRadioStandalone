using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Utils
{
    [TestClass]
    public class RadioHelperMuteTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ConfigureMutableRadios();
            ClearMutedRadios();
            ConfigureMutableRadios();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigureMutableRadios();
            ClearMutedRadios();
        }

        [TestMethod]
        public void ToggleSelectedRadioMuteCapsReceiveVolumeWithoutChangingRadioVolumeOrPtt()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.selected = 1;
            state.ptt = true;
            state.radios[1].volume = 0.8f;

            RadioHelper.ToggleSelectedRadioMute();

            Assert.IsTrue(RadioHelper.IsRadioMuted(1));
            Assert.AreEqual(0.8f, state.radios[1].volume);
            Assert.IsTrue(state.ptt);
            Assert.IsTrue(RadioHelper.GetEffectiveReceiveVolume(1, state.radios[1]) < state.radios[1].volume);
            Assert.IsTrue(RadioHelper.GetEffectiveReceiveVolume(1, state.radios[1]) <= 0.5f);
        }

        [TestMethod]
        public void MutingRadioDoesNotIncreaseAlreadyQuietReceiveVolume()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.selected = 1;
            state.radios[1].volume = 0.03f;

            RadioHelper.ToggleSelectedRadioMute();

            Assert.AreEqual(0.03f, RadioHelper.GetEffectiveReceiveVolume(1, state.radios[1]));
        }

        [TestMethod]
        public void ToggleSelectedRadioMuteUsesRadioOneWhenIntercomSelectedAndSecondRadioUnavailable()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.selected = 0;
            state.radios[2].modulation = RadioInformation.Modulation.DISABLED;

            RadioHelper.ToggleSelectedRadioMute();

            Assert.IsTrue(RadioHelper.IsRadioMuted(1));
            Assert.IsFalse(RadioHelper.IsRadioMuted(0));
            Assert.IsFalse(RadioHelper.IsRadioMuted(2));
        }

        [TestMethod]
        public void ToggleOtherRadioMuteMutesOnlyTheNonSelectedRadio()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.selected = 1;

            RadioHelper.ToggleOtherRadioMute();

            Assert.IsFalse(RadioHelper.IsRadioMuted(1));
            Assert.IsTrue(RadioHelper.IsRadioMuted(2));
        }

        [TestMethod]
        public void ToggleAllRadiosMuteMutesAvailableRadiosThenRestoresWhenAllAreMuted()
        {
            RadioHelper.ToggleAllRadiosMute();

            Assert.IsTrue(RadioHelper.IsRadioMuted(1));
            Assert.IsTrue(RadioHelper.IsRadioMuted(2));

            RadioHelper.ToggleAllRadiosMute();

            Assert.IsFalse(RadioHelper.IsRadioMuted(1));
            Assert.IsFalse(RadioHelper.IsRadioMuted(2));
        }

        [TestMethod]
        public void ToggleAllRadiosMuteIgnoresDisabledRadio()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.radios[2].modulation = RadioInformation.Modulation.DISABLED;

            RadioHelper.ToggleAllRadiosMute();

            Assert.IsTrue(RadioHelper.IsRadioMuted(1));
            Assert.IsFalse(RadioHelper.IsRadioMuted(2));
        }

        [TestMethod]
        public void MutedRadioRestoresToFullEffectiveReceiveVolumeWhenToggledAgain()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.selected = 1;
            state.radios[1].volume = 0.8f;

            RadioHelper.ToggleSelectedRadioMute();
            RadioHelper.ToggleSelectedRadioMute();

            Assert.IsFalse(RadioHelper.IsRadioMuted(1));
            Assert.AreEqual(0.8f, RadioHelper.GetEffectiveReceiveVolume(1, state.radios[1]));
        }

        private static void ConfigureMutableRadios()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.control = PlayerGameState.RadioSwitchControls.HOTAS;
            state.selected = 1;
            state.ptt = false;

            state.radios[0].modulation = RadioInformation.Modulation.INTERCOM;
            state.radios[0].volMode = RadioInformation.VolumeMode.OVERLAY;
            state.radios[0].volume = 1.0f;

            state.radios[1].modulation = RadioInformation.Modulation.AM;
            state.radios[1].volMode = RadioInformation.VolumeMode.OVERLAY;
            state.radios[1].volume = 0.8f;

            state.radios[2].modulation = RadioInformation.Modulation.AM;
            state.radios[2].volMode = RadioInformation.VolumeMode.OVERLAY;
            state.radios[2].volume = 0.9f;
        }

        private static void ClearMutedRadios()
        {
            for (var radioId = 1; radioId <= 2; radioId++)
            {
                if (!RadioHelper.IsRadioMuted(radioId))
                {
                    continue;
                }

                ClientStateSingleton.Instance.PlayerGameState.selected = (short)radioId;
                RadioHelper.ToggleSelectedRadioMute();
            }
        }
    }
}
