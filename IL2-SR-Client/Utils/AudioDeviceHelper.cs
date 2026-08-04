using System;
using NAudio.CoreAudioApi;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public static class AudioDeviceHelper
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

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
            catch (NotImplementedException ex)
            {
                Logger.Warn(ex, $"Unable to unmute audio device {device.FriendlyName} - continuing anyway");
            }
        }
    }
}
