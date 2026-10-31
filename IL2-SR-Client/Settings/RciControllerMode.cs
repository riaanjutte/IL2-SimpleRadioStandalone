using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Easy.MessageHub;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    /// <summary>
    /// RCI Controller mode: lets a Combat Box RCI officer use SRS without IL-2. While active, SRS sends the
    /// codename from the officer's access code as CODENAME___RCI, uses the code's coalition instead of IL-2
    /// telemetry, and stays on the Combat Box server.
    ///
    /// The mode is active when it is switched on in Settings and the stored access code was valid when last
    /// checked (at startup, when settings are saved, and before each connection). A code that expires while
    /// connected does not end the session; the next connection needs a new code.
    /// </summary>
    public static class RciControllerMode
    {
        public const string ServerHost = "srs.combatbox.net";
        public const int ServerPort = 6002;

        private static volatile RciAccessCode _activeCode;

        /// <summary>The server a controller uses: Combat Box.</summary>
        public static string ServerAddress
        {
            get
            {
#if DEBUG
                // Debug builds only: IL2_SRS_RCI_DEV_SERVER points a controller at a test server instead.
                var devServer = Environment.GetEnvironmentVariable("IL2_SRS_RCI_DEV_SERVER");
                if (!string.IsNullOrWhiteSpace(devServer))
                {
                    return devServer.Trim();
                }
#endif
                return ServerHost + ":" + ServerPort;
            }
        }

        /// <summary>True when the address is the controller's server (any port).</summary>
        public static bool IsControllerServer(string address)
        {
            return string.Equals(HostOf(address), HostOf(ServerAddress), StringComparison.OrdinalIgnoreCase);
        }

        private static string HostOf(string address)
        {
            var host = (address ?? string.Empty).Trim();
            var portSeparator = host.LastIndexOf(':');
            return portSeparator > 0 ? host.Substring(0, portSeparator) : host;
        }

        public static bool IsActive => _activeCode != null;

        public static RciAccessCode ActiveCode => _activeCode;

        /// <summary>CODENAME___RCI while active, otherwise null.</summary>
        public static string RadioName => _activeCode?.RadioName;

        /// <summary>1 Allies or 2 Axis while active, otherwise 0.</summary>
        public static short Coalition => _activeCode?.Coalition ?? 0;

        /// <summary>
        /// False until this build trusts an RCI signing key. Until then no code can be valid, so the mode stays
        /// hidden and off, even if it was switched on before.
        /// </summary>
        public static bool IsAvailable => RciAccessCode.TrustedKeys.Count > 0;

        public static bool IsEnabledInSettings =>
            IsAvailable && GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RciControllerEnabled);

        /// <summary>Raised on the calling thread when the mode turns on or off, or its code changes.</summary>
        public static event Action ActiveChanged;

        /// <summary>
        /// Re-checks the saved settings. Returns the code check, or null when the mode is switched off.
        /// </summary>
        public static RciAccessCodeResult Refresh(DateTime utcNow)
        {
            var settings = GlobalSettingsStore.Instance;
            if (!IsEnabledInSettings)
            {
                SetActiveCode(null);
                return null;
            }

            var result = RciAccessCode.Validate(settings.GetClientSetting(GlobalSettingsKeys.RciAccessCode).RawValue, utcNow);
            SetActiveCode(result.IsValid ? result.Code : null);
            return result;
        }

        /// <summary>Turns the mode off for this session only (the saved setting is unchanged).</summary>
        public static void DeactivateForSession()
        {
            SetActiveCode(null);
        }

        internal static void SetActiveCode(RciAccessCode code)
        {
            var previous = _activeCode;
            _activeCode = code;
            if (ReferenceEquals(previous, code))
            {
                return;
            }

            // The controller's coalition replaces IL-2's; when the mode ends, IL-2 telemetry sets it again.
            var gameState = ClientStateSingleton.Instance.PlayerGameState;
            if (gameState != null)
            {
                gameState.coalition = code?.Coalition ?? 0;
            }

            ActiveChanged?.Invoke();
            MessageHub.Instance.Publish(new PlayerStateUpdate());
        }
    }
}
