using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public static class AudioDeviceHelper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static bool _unmuteNotImplementedLogged;

        /// <summary>
        /// Attempts to unmute the given audio device.
        ///
        /// This is best-effort: accessing MMDevice.AudioEndpointVolume eagerly queries
        /// per-channel volume and hardware support COM APIs which are not implemented
        /// under WINE and throw NotImplementedException.
        /// See https://github.com/ciribob/DCS-SimpleRadioStandalone/issues/621
        /// </summary>
        public static void TryUnmute(MMDevice device)
        {
            try
            {
                device.AudioEndpointVolume.Mute = false;
            }
            catch (NotImplementedException)
            {
                // Always the case under Wine, on every capture start: once is enough, and the stack trace says nothing.
                if (!_unmuteNotImplementedLogged)
                {
                    _unmuteNotImplementedLogged = true;
                    Logger.Warn($"Unable to unmute audio device {device.FriendlyName} (not implemented, as under Wine) - continuing anyway");
                }
            }
            catch (COMException ex)
            {
                Logger.Warn(ex, "Unable to unmute audio device - continuing anyway");
            }
        }
    }
}
