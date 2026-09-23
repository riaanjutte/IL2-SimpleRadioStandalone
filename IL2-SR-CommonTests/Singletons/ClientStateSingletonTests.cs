using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Singletons
{
    [TestClass]
    public class ClientStateSingletonTests
    {
        [TestMethod]
        public void SelectGameNameKeyKeepsTheGamesSeparate()
        {
            Assert.AreEqual(GlobalSettingsKeys.LastSeenNameGreatBattles,
                ClientStateSingleton.SelectGameNameKey(true, false));
            Assert.AreEqual(GlobalSettingsKeys.LastSeenNameKorea,
                ClientStateSingleton.SelectGameNameKey(false, true));
            Assert.IsNull(ClientStateSingleton.SelectGameNameKey(false, false));
            Assert.IsNull(ClientStateSingleton.SelectGameNameKey(true, true));
        }

        [TestMethod]
        public void ResolvePilotNamePrefersTheActiveGamesRememberedName()
        {
            Assert.AreEqual("Haluter", ClientStateSingleton.ResolvePilotName("Haluter", "=TBAS=Haluter"));
            Assert.AreEqual("=TBAS=Haluter", ClientStateSingleton.ResolvePilotName("", "=TBAS=Haluter"));
            Assert.AreEqual("=TBAS=Haluter", ClientStateSingleton.ResolvePilotName(" ", "=TBAS=Haluter"));
        }
    }
}
