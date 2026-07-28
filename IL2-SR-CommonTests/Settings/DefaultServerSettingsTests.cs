using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Settings
{
    [TestClass]
    public class DefaultServerSettingsTests
    {
        [TestMethod]
        public void AssignedCallsignRosterPathIsNotConfiguredByDefault()
        {
            string settingName = ServerSettingsKeys.ASSIGNED_CALLSIGNS_JSON_FILE.ToString();

            Assert.IsTrue(DefaultServerSettings.Defaults.ContainsKey(settingName));
            Assert.AreEqual(string.Empty, DefaultServerSettings.Defaults[settingName]);
        }
    }
}
