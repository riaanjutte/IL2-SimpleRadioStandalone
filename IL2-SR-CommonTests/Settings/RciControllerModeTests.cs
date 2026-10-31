using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.IL2;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Settings
{
    [TestClass]
    public class RciControllerModeTests
    {
        [TestCleanup]
        public void TurnModeOff()
        {
            RciControllerMode.SetActiveCode(null);
        }

        [TestMethod]
        public void ActiveControllerIsKnownByItsCodenameAndCoalition()
        {
            var state = ClientStateSingleton.Instance;
            state.LastSeenName = "Pilot Name";

            RciControllerMode.SetActiveCode(RciTestCodes.Create("SPYGLASS", 2));

            Assert.IsTrue(RciControllerMode.IsActive);
            Assert.AreEqual("SPYGLASS___RCI", state.LastSeenName);
            Assert.AreEqual((short)2, state.PlayerGameState.coalition);

            RciControllerMode.SetActiveCode(null);

            Assert.IsFalse(RciControllerMode.IsActive);
            Assert.AreEqual("Pilot Name", state.LastSeenName, "The pilot name is kept for when the mode is off.");
            Assert.AreEqual((short)0, state.PlayerGameState.coalition);
        }

        [TestMethod]
        public void TurningTheModeOnOrOffIsAnnounced()
        {
            var changes = 0;
            Action onChange = () => changes++;
            RciControllerMode.ActiveChanged += onChange;
            try
            {
                var code = RciTestCodes.Create("MAGIC", 1);
                RciControllerMode.SetActiveCode(code);
                RciControllerMode.SetActiveCode(code);
                RciControllerMode.SetActiveCode(null);
                Assert.AreEqual(2, changes, "Setting the same code again is not a change.");
            }
            finally
            {
                RciControllerMode.ActiveChanged -= onChange;
            }
        }

        [TestMethod]
        public void ControllerCoalitionReplacesTheOneIl2Reports()
        {
            var state = new PlayerGameState();

            var changed = IL2RadioSyncHandler.ApplyControlData(state, 1234, 1, controllerCoalition: 2);
            Assert.IsTrue(changed);
            Assert.AreEqual((short)2, state.coalition);
            Assert.AreEqual(1234, state.vehicleId);

            Assert.IsFalse(IL2RadioSyncHandler.ApplyControlData(state, 1234, 3, controllerCoalition: 2),
                "IL-2 switching side does not move the controller.");
            Assert.AreEqual((short)2, state.coalition);
        }

        [TestMethod]
        public void RadioStateIsOnlyResetWithoutIl2WhenNotAController()
        {
            Assert.IsTrue(IL2RadioSyncManager.ShouldResetRadioState(telemetryCurrent: false, rciControllerActive: false));
            Assert.IsFalse(IL2RadioSyncManager.ShouldResetRadioState(telemetryCurrent: false, rciControllerActive: true));
            Assert.IsFalse(IL2RadioSyncManager.ShouldResetRadioState(telemetryCurrent: true, rciControllerActive: false));
        }

        [TestMethod]
        public void ControllerServerIsCombatBoxOnAnyPort()
        {
            Assert.AreEqual("srs.combatbox.net:6002", RciControllerMode.ServerAddress);
            Assert.IsTrue(RciControllerMode.IsControllerServer("srs.combatbox.net:6002"));
            Assert.IsTrue(RciControllerMode.IsControllerServer(" SRS.CombatBox.net:7002 "));
            Assert.IsTrue(RciControllerMode.IsControllerServer("srs.combatbox.net"));
            Assert.IsFalse(RciControllerMode.IsControllerServer("tacticalairwar.com:6002"));
            Assert.IsFalse(RciControllerMode.IsControllerServer(""));
        }

        [TestMethod]
        public void ModeIsOffWhenSwitchedOffInSettingsAndNeedsAValidCodeWhenOn()
        {
            var settings = GlobalSettingsStore.Instance;
            try
            {
                settings.SetClientSetting(GlobalSettingsKeys.RciControllerEnabled, false);
                Assert.IsNull(RciControllerMode.Refresh(DateTime.UtcNow));
                Assert.IsFalse(RciControllerMode.IsActive);

                settings.SetClientSetting(GlobalSettingsKeys.RciControllerEnabled, true);
                settings.SetClientSetting(GlobalSettingsKeys.RciAccessCode, "not a code");
                var result = RciControllerMode.Refresh(DateTime.UtcNow);
                if (RciControllerMode.IsAvailable)
                {
                    Assert.IsFalse(result.IsValid);
                }
                else
                {
                    // Release builds before rci-web issues codes: a mode switched on earlier must not block connecting.
                    Assert.IsNull(result, "Without a trusted key the mode stays off, even when switched on.");
                    Assert.IsFalse(RciControllerMode.IsEnabledInSettings);
                }

                Assert.IsFalse(RciControllerMode.IsActive);
            }
            finally
            {
                settings.SetClientSetting(GlobalSettingsKeys.RciControllerEnabled, false);
                settings.SetClientSetting(GlobalSettingsKeys.RciAccessCode, "");
            }
        }
    }

    /// <summary>Valid access codes signed with a throwaway key, for tests of the mode.</summary>
    internal static class RciTestCodes
    {
        public static RciAccessCode Create(string codename, int coalition)
        {
            using (var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256))
            {
                var publicKey = signer.ExportParameters(false);
                var keys = new Dictionary<string, RciAccessKey> { ["test-1"] = new RciAccessKey(publicKey.Q.X, publicKey.Q.Y) };
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var json = "{\"kid\":\"test-1\",\"sub\":\"1\",\"cs\":\"" + codename + "\",\"co\":" + coalition +
                           ",\"iat\":" + now + ",\"exp\":" + (now + 3600) + "}";
                var payload = RciAccessCode.EncodeBase64Url(Encoding.UTF8.GetBytes(json));
                var signature = signer.SignData(Encoding.ASCII.GetBytes(RciAccessCode.Prefix + payload), HashAlgorithmName.SHA256);
                var code = RciAccessCode.Prefix + payload + "." + RciAccessCode.EncodeBase64Url(signature);
                return RciAccessCode.Validate(code, DateTime.UtcNow, keys).Code;
            }
        }
    }
}
