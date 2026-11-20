using System;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Plugins;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using Microsoft.Extensions.Configuration;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Admin
{
    /// <summary>Which server this is: its name, host, version and admin address (for plugins).</summary>
    public sealed class ServerIdentity : IServerInfo
    {
        /// <summary>The image version, set by the Dockerfile from its VERSION build argument.</summary>
        public const string ImageVersionKey = "SRS_IMAGE_VERSION";

        private readonly ServerSettingsStore _settings;

        public ServerIdentity(ServerSettingsStore settings, IConfiguration configuration)
            : this(settings, configuration?[ImageVersionKey])
        {
        }

        internal ServerIdentity(ServerSettingsStore settings, string imageVersion, string hostname = null)
        {
            _settings = settings;
            Version = string.IsNullOrWhiteSpace(imageVersion) || imageVersion.Trim() == "dev"
                ? ReleaseMetadata.Version
                : imageVersion.Trim();
            Hostname = hostname ?? Environment.MachineName;
        }

        /// <summary>The machine name; in a container its hostname, the container ID unless the stack sets one.</summary>
        public string Hostname { get; }

        /// <summary>The image version, e.g. 1.0.5.0-preview, or the server version outside Docker.</summary>
        public string Version { get; }

        /// <summary>The Server name setting, or the hostname when it isn't set.</summary>
        public string Name => Text(ServerSettingsKeys.SERVER_NAME) ?? Hostname;

        /// <summary>The Public admin URL setting, or null.</summary>
        public string AdminUrl => Text(ServerSettingsKeys.PUBLIC_ADMIN_URL);

        private string Text(ServerSettingsKeys key)
        {
            var value = _settings?.Get(key).StringValue;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
