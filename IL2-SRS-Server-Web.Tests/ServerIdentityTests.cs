using System.Collections.Generic;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Admin;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Xunit;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Tests
{
    public class ServerIdentityTests
    {
        private static readonly Dictionary<string, string> NoOverrides = new Dictionary<string, string>();

        [Fact]
        public void NameFallsBackToTheHostnameAndVersionToTheServerVersion()
        {
            using var temp = new TempDirectory();
            var settings = new ServerSettingsStore(temp.CreateDatabase(), NoOverrides);

            var identity = new ServerIdentity(settings, "dev", "srs-1");
            Assert.Equal("srs-1", identity.Name);
            Assert.Equal(ReleaseMetadata.Version, identity.Version);
            Assert.Null(identity.AdminUrl);

            Assert.Null(settings.Set(ServerSettingsKeys.SERVER_NAME, " My SRS Server "));
            Assert.Null(settings.Set(ServerSettingsKeys.PUBLIC_ADMIN_URL, "https://srs.example.com"));
            identity = new ServerIdentity(settings, "1.0.5.0-preview", "srs-1");
            Assert.Equal("My SRS Server", identity.Name);
            Assert.Equal("1.0.5.0-preview", identity.Version);
            Assert.Equal("https://srs.example.com", identity.AdminUrl);
        }

        [Fact]
        public void ServerNameAndAdminUrlAreNotSentToClients()
        {
            using var temp = new TempDirectory();
            var settings = new ServerSettingsStore(temp.CreateDatabase(), NoOverrides);
            Assert.Null(settings.Set(ServerSettingsKeys.SERVER_NAME, "My SRS Server"));
            Assert.Null(settings.Set(ServerSettingsKeys.PUBLIC_ADMIN_URL, "https://srs.example.com"));

            var synced = settings.ToDictionary();
            Assert.False(synced.ContainsKey(nameof(ServerSettingsKeys.SERVER_NAME)));
            Assert.False(synced.ContainsKey(nameof(ServerSettingsKeys.PUBLIC_ADMIN_URL)));
        }

        [Theory]
        [InlineData("", true)]
        [InlineData("https://srs.example.com/admin", true)]
        [InlineData("http://10.0.0.5:8081", true)]
        [InlineData("srs.example.com", false)]
        [InlineData("ftp://srs.example.com", false)]
        public void PublicAdminUrlMustBeAnHttpAddress(string value, bool valid)
        {
            var error = SettingDefinition.For(ServerSettingsKeys.PUBLIC_ADMIN_URL).Validate(value);
            Assert.Equal(valid, error == null);
        }
    }
}
