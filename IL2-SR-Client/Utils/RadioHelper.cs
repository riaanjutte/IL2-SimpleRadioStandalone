using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.ServiceModel.Channels;
using System.Text;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PresetChannels;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Easy.MessageHub;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    public static class RadioHelper
    {
        private const float MinimumMutedVolume = 0.05f;
        private const float MaximumMutedVolume = 0.50f;
        private const float DefaultMutedVolume = 0.25f;
        private static readonly HashSet<int> MutedRadios = new HashSet<int>();
        private static readonly object MutedRadiosLock = new object();
        private static bool _microphoneMuted;
     
        public static bool SelectRadio(int radioId, bool tts = true)
        {
            var radio = GetRadio(radioId);

            if (radio != null)
            {
                if (radio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    var current = ClientStateSingleton.Instance.PlayerGameState.selected;

                    ClientStateSingleton.Instance.PlayerGameState.selected = (short) radioId;

                    //only send audio if we switched
                    if (tts && current != ClientStateSingleton.Instance.PlayerGameState.selected)
                    {
                        if (radioId == 0)
                        {
                            MessageHub.Instance.Publish(new TextToSpeechMessage()
                            {
                                Message = "Selected Intercom"
                            });
                        }
                        else
                        {
                            MessageHub.Instance.Publish(new TextToSpeechMessage()
                            {
                                Message = "Selected Radio "+radioId
                            });
                        }
                    }
                    return true;
                }
            }

            return false;
        }

        public static bool IsSecondRadioAvailable()
        {
            return ClientStateSingleton.Instance.PlayerGameState.radios[2].modulation !=
                   RadioInformation.Modulation.DISABLED;
        }

        public static RadioInformation GetRadio(int radio)
        {
            var IL2PlayerRadioInfo = ClientStateSingleton.Instance.PlayerGameState;

            if ((IL2PlayerRadioInfo != null)  &&
                radio < IL2PlayerRadioInfo.radios.Length && (radio >= 0))
            {
                return IL2PlayerRadioInfo.radios[radio];
            }

            return null;
        }

        public static void SelectRadioChannel(int channel, int radioId)
        {
            var currentRadio = GetRadio(radioId);

            if (currentRadio == null || currentRadio.modulation == RadioInformation.Modulation.INTERCOM)
            {
                return;
            }
            var freq = PlayerGameState.START_FREQ +( PlayerGameState.CHANNEL_OFFSET * channel);

            currentRadio.freq = freq;

            currentRadio.channel = channel;

            if (IsSecondRadioAvailable())
            {
                MessageHub.Instance.Publish(new TextToSpeechMessage()
                {
                    Message = "Channel " + channel+" Radio "+radioId
                });
            }
            else
            {
                MessageHub.Instance.Publish(new TextToSpeechMessage()
                {
                    Message = "Channel " + channel
                });
            }
            
            MessageHub.Instance.Publish(new PlayerStateUpdate());
        }

        public static void RadioChannelUp(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null && currentRadio.modulation != RadioInformation.Modulation.INTERCOM)
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    var wrap = GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.WrapNextRadio);
                    
                    var chan = currentRadio.channel+1;

                    var limit = SyncedServerSettings.Instance.GetSettingInt(ServerSettingsKeys.CHANNEL_LIMIT);
                    if (chan > limit)
                    {
                        if (wrap)
                        {
                            chan = 1;
                        }
                        else
                        {
                            chan = limit;
                        }
                    }

                    var freq = PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * chan);

                    currentRadio.freq = freq;

                    currentRadio.channel = chan;

                    MessageHub.Instance.Publish(new PlayerStateUpdate());
                    if (IsSecondRadioAvailable())
                    {
                        MessageHub.Instance.Publish(new TextToSpeechMessage()
                        {
                            Message = "Channel " + chan + " Radio " + radioId
                        });
                    }
                    else
                    {
                        MessageHub.Instance.Publish(new TextToSpeechMessage()
                        {
                            Message = "Channel " + chan
                        });
                    }
                }
            }
        }

        public static void RadioChannelDown(int radioId)
        {
            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null && currentRadio.modulation != RadioInformation.Modulation.INTERCOM)
            {
                if (currentRadio.modulation != RadioInformation.Modulation.DISABLED
                    && ClientStateSingleton.Instance.PlayerGameState.control ==
                    PlayerGameState.RadioSwitchControls.HOTAS)
                {
                    var wrap = GlobalSettingsStore.Instance.ProfileSettingsStore.GetClientSettingBool(ProfileSettingsKeys.WrapNextRadio);

                    var chan = currentRadio.channel - 1;

                    var limit = SyncedServerSettings.Instance.GetSettingInt(ServerSettingsKeys.CHANNEL_LIMIT);
                    
                    if (chan < 1)
                    {
                        if (wrap)
                        {
                            chan = limit;
                        }
                        else
                        {
                            chan = 1;
                        }
                    }

                    var freq = PlayerGameState.START_FREQ + (PlayerGameState.CHANNEL_OFFSET * chan);

                    currentRadio.freq = freq;

                    currentRadio.channel = chan;

                    if (IsSecondRadioAvailable())
                    {
                        MessageHub.Instance.Publish(new TextToSpeechMessage()
                        {
                            Message = "Channel " + chan + " Radio " + radioId
                        });
                    }
                    else
                    {
                        MessageHub.Instance.Publish(new TextToSpeechMessage()
                        {
                            Message = "Channel " + chan
                        });
                    }

                    MessageHub.Instance.Publish(new PlayerStateUpdate());
                    
                }
            }
        }

        public static void SetRadioVolume(float volume, int radioId)
        {
            if (volume > 1.0)
            {
                volume = 1.0f;
            }else if (volume < 0)
            {
                volume = 0;
            }

            var currentRadio = RadioHelper.GetRadio(radioId);

            if (currentRadio != null
                && currentRadio.modulation != RadioInformation.Modulation.DISABLED
                && currentRadio.volMode == RadioInformation.VolumeMode.OVERLAY)
            {
                currentRadio.volume = volume;
            }
        }

        public static float GetEffectiveReceiveVolume(int radioId, RadioInformation radio)
        {
            if (radio == null)
            {
                return 0;
            }

            if (IsRadioMuted(radioId))
            {
                return Math.Min(radio.volume, GetSelectedRadioMutedVolume());
            }

            return radio.volume;
        }

        public static bool IsRadioMuted(int radioId)
        {
            lock (MutedRadiosLock)
            {
                return MutedRadios.Contains(radioId);
            }
        }

        public static bool IsMicrophoneMuted()
        {
            lock (MutedRadiosLock)
            {
                return _microphoneMuted;
            }
        }

        public static void SetMicrophoneMuted(bool muted)
        {
            lock (MutedRadiosLock)
            {
                _microphoneMuted = muted;
            }
        }

        public static void ToggleMicrophoneMute()
        {
            lock (MutedRadiosLock)
            {
                _microphoneMuted = !_microphoneMuted;
            }
        }

        public static void ToggleSelectedRadioMute()
        {
            var IL2PlayerRadioInfo = ClientStateSingleton.Instance.PlayerGameState;
            if (IL2PlayerRadioInfo == null)
            {
                return;
            }

            var selectedRadioId = IL2PlayerRadioInfo.selected;
            if (!IsSecondRadioAvailable() && selectedRadioId == 0)
            {
                selectedRadioId = 1;
            }

            ToggleRadioMute(selectedRadioId, GetSelectedRadioMutedVolume());
        }

        public static void ToggleOtherRadioMute()
        {
            var playerGameState = ClientStateSingleton.Instance.PlayerGameState;
            if (playerGameState == null)
            {
                return;
            }

            var mutedVolume = GetSelectedRadioMutedVolume();
            var selectedRadioId = playerGameState.selected;
            var otherRadioId = selectedRadioId == 1 ? 2 : 1;

            if (IsMutableRadio(otherRadioId))
            {
                ToggleRadioMute(otherRadioId, mutedVolume);
            }
        }

        public static void ToggleAllRadiosMute()
        {
            if (ClientStateSingleton.Instance.PlayerGameState == null)
            {
                return;
            }

            var mutedVolume = GetSelectedRadioMutedVolume();
            var radioIds = new List<int>();

            for (var radioId = 1; radioId <= 2; radioId++)
            {
                if (IsMutableRadio(radioId))
                {
                    radioIds.Add(radioId);
                }
            }

            if (radioIds.Count == 0)
            {
                return;
            }

            var restore = radioIds.TrueForAll(IsRadioMuted);
            foreach (var radioId in radioIds)
            {
                if (restore)
                {
                    RestoreRadioVolume(radioId, mutedVolume);
                }
                else
                {
                    MuteRadio(radioId, mutedVolume);
                }
            }
        }

        private static void ToggleRadioMute(int radioId, float mutedVolume)
        {
            var currentRadio = GetRadio(radioId);
            if (!IsMutableRadio(currentRadio))
            {
                return;
            }

            if (IsRadioMuted(radioId))
            {
                RestoreRadioVolume(radioId, mutedVolume);
            }
            else
            {
                MuteRadio(radioId, mutedVolume);
            }
        }

        private static void MuteRadio(int radioId, float mutedVolume)
        {
            var currentRadio = GetRadio(radioId);
            if (currentRadio == null)
            {
                return;
            }

            lock (MutedRadiosLock)
            {
                if (!MutedRadios.Add(radioId))
                {
                    return;
                }
            }

            PlaySelectedRadioMuteCue(radioId, false);
        }

        private static void RestoreRadioVolume(int radioId, float mutedVolume)
        {
            var currentRadio = GetRadio(radioId);
            if (currentRadio == null)
            {
                return;
            }

            lock (MutedRadiosLock)
            {
                if (!MutedRadios.Remove(radioId))
                {
                    return;
                }
            }

            PlaySelectedRadioMuteCue(radioId, true);
        }

        private static bool IsMutableRadio(int radioId)
        {
            return IsMutableRadio(GetRadio(radioId));
        }

        private static bool IsMutableRadio(RadioInformation radio)
        {
            return radio != null
                   && radio.modulation != RadioInformation.Modulation.DISABLED
                   && radio.modulation != RadioInformation.Modulation.INTERCOM
                   && radio.volMode == RadioInformation.VolumeMode.OVERLAY;
        }

        private static void PlaySelectedRadioMuteCue(int radioId, bool unmuted)
        {
            MessageHub.Instance.Publish(new SelectedRadioMuteCueMessage()
            {
                RadioId = radioId,
                Unmuted = unmuted
            });
        }

        private static float GetSelectedRadioMutedVolume()
        {
            var rawValue = GlobalSettingsStore.Instance.ProfileSettingsStore
                .GetClientSetting(ProfileSettingsKeys.SelectedRadioMutedVolume).RawValue;

            float mutedVolume;
            if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out mutedVolume))
            {
                mutedVolume = DefaultMutedVolume;
            }

            if (mutedVolume < MinimumMutedVolume)
            {
                return MinimumMutedVolume;
            }

            if (mutedVolume > MaximumMutedVolume)
            {
                return MaximumMutedVolume;
            }

            return mutedVolume;
        }

        public static void PreviousRadio()
        {
            var selected = ClientStateSingleton.Instance.PlayerGameState.selected;

            if (selected - 1 < 0)
            {
                //radio 2 if its not disabled - else one
                for (int i = ClientStateSingleton.Instance.PlayerGameState.radios.Length - 1; i >= 0; i--)
                {
                    if (SelectRadio(i))
                    {
                        return;
                    }

                }
            }
            else
            {
                for (int i = selected-1; i >= 0; i--)
                {
                    if (SelectRadio(i))
                    {
                        return;
                    }

                }
            }
            //looped
            SelectRadio(0);
        }

        public static void NextRadio()
        {
            var selected = ClientStateSingleton.Instance.PlayerGameState.selected;

            if (selected + 1 > ClientStateSingleton.Instance.PlayerGameState.radios.Length)
            {
                SelectRadio(0);
            }
            else
            {
                //find next radios
                for (int i = selected + 1; i < ClientStateSingleton.Instance.PlayerGameState.radios.Length; i++)
                {
                    if (SelectRadio(i))
                    {
                        return;
                    }
                }
            }
            //looped
            SelectRadio(0);
        }

        private static string BuildRadioStatus(int radio)
        {
            var selected = ClientStateSingleton.Instance.PlayerGameState.selected;

            var builder = new StringBuilder();

            var radio1 = GetRadio(radio);

            int radio1Count = ConnectedClientsSingleton.Instance.ClientsOnFreq(radio1.freq, radio1.modulation);

            if (radio == 0)
            {
                builder.Append("Intercom ");
            }
            else
            {
                builder.Append($"Radio {radio} ");
                builder.Append($" - Channel {radio1.Channel}.");
            }

            builder.Append($", {radio1Count} Connected.");

            builder.Append(".\n");

            return builder.ToString();
        }

        public static void ReadStatus()
        {
            var selected = ClientStateSingleton.Instance.PlayerGameState.selected;

            if (selected >= 0)
            {
                MessageHub.Instance.Publish(new TextToSpeechMessage()
                {
                    Message = BuildRadioStatus(selected)
                });
            }



        }
    }
}
