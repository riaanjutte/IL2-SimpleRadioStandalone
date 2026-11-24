using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Utils
{
    [TestClass]
    public class RadioHelperVolumeTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            ConfigureRadios();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ConfigureRadios();
        }

        [TestMethod]
        public void VolumeUpAndDownMoveOneStep()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;

            RadioHelper.RadioVolumeUp(1);
            Assert.AreEqual(0.85f, state.radios[1].volume, 0.0001f);

            RadioHelper.RadioVolumeDown(1);
            RadioHelper.RadioVolumeDown(1);
            Assert.AreEqual(0.75f, state.radios[1].volume, 0.0001f);
        }

        [TestMethod]
        public void EachRadioChangesOnlyItsOwnVolume()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;

            RadioHelper.RadioVolumeDown(0);
            RadioHelper.RadioVolumeUp(2);

            Assert.AreEqual(0.95f, state.radios[0].volume, 0.0001f);
            Assert.AreEqual(0.8f, state.radios[1].volume, 0.0001f);
            Assert.AreEqual(0.95f, state.radios[2].volume, 0.0001f);
        }

        [TestMethod]
        public void VolumeStaysBetweenSilentAndFull()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;

            state.radios[1].volume = 1.0f;
            RadioHelper.RadioVolumeUp(1);
            Assert.AreEqual(1.0f, state.radios[1].volume, 0.0001f);

            state.radios[1].volume = 0.0f;
            RadioHelper.RadioVolumeDown(1);
            Assert.AreEqual(0.0f, state.radios[1].volume, 0.0001f);
        }

        [TestMethod]
        public void VolumeBetweenStepsMovesToTheNextWholeStep()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;

            state.radios[1].volume = 0.37f;
            RadioHelper.RadioVolumeUp(1);
            Assert.AreEqual(0.40f, state.radios[1].volume, 0.0001f);

            state.radios[1].volume = 0.37f;
            RadioHelper.RadioVolumeDown(1);
            Assert.AreEqual(0.30f, state.radios[1].volume, 0.0001f);
        }

        [TestMethod]
        public void VolumeSetByTheGameIsLeftAlone()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.radios[1].volMode = RadioInformation.VolumeMode.COCKPIT;

            RadioHelper.RadioVolumeUp(1);

            Assert.AreEqual(0.8f, state.radios[1].volume, 0.0001f);
        }

        [TestMethod]
        public void DisabledRadioIsLeftAlone()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;
            state.radios[2].modulation = RadioInformation.Modulation.DISABLED;

            RadioHelper.RadioVolumeDown(2);

            Assert.AreEqual(0.9f, state.radios[2].volume, 0.0001f);
        }

        private static void ConfigureRadios()
        {
            var state = ClientStateSingleton.Instance.PlayerGameState;

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
    }
}
