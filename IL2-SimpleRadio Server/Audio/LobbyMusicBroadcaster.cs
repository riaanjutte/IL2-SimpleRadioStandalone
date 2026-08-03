using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Setting;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Network.Models;
using Ciribob.IL2.SimpleRadio.Standalone.Server.Settings;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Audio
{
    internal sealed class LobbyMusicBroadcaster
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan TrackDelay = TimeSpan.FromSeconds(1);

        private readonly ConcurrentDictionary<string, SRClient> _clients;
        private readonly BlockingCollection<OutgoingUDPPackets> _outgoing;
        private readonly Func<double[]> _getLobbyFrequencies;
        private readonly ServerSettingsStore _settings;
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        private readonly byte[] _guidBytes = Encoding.ASCII.GetBytes(UDPVoicePacket.LobbyMusicGuid);
        private Thread _thread;
        private ulong _packetNumber = 1;
        private string _lastConfigurationWarning;

        public LobbyMusicBroadcaster(ConcurrentDictionary<string, SRClient> clients,
            BlockingCollection<OutgoingUDPPackets> outgoing, Func<double[]> getLobbyFrequencies,
            ServerSettingsStore settings)
        {
            _clients = clients;
            _outgoing = outgoing;
            _getLobbyFrequencies = getLobbyFrequencies;
            _settings = settings;
        }

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _thread = new Thread(Run)
            {
                IsBackground = true,
                Name = "SRS Lobby Music"
            };
            _thread.Start();
        }

        public void Stop()
        {
            _stop.Cancel();
            _thread?.Join(TimeSpan.FromSeconds(2));
            _thread = null;
        }

        private void Run()
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    if (!IsEnabled())
                    {
                        Wait(IdleDelay);
                        continue;
                    }

                    var musicDirectory = ResolveMusicDirectory();
                    var musicFiles = GetMusicFiles(musicDirectory);
                    if (musicFiles.Count == 0)
                    {
                        LogConfigurationWarning("Lobby music is enabled, but no .ogg files were found in " +
                                                musicDirectory);
                        Wait(IdleDelay);
                        continue;
                    }

                    _lastConfigurationWarning = null;
                    foreach (var musicFile in musicFiles)
                    {
                        if (_stop.IsCancellationRequested || !IsEnabled())
                        {
                            break;
                        }

                        PlayTrack(musicFile);
                        Wait(TrackDelay);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Unable to read the neutral lobby music configuration or playlist.");
                    Wait(IdleDelay);
                }
            }
        }

        private void PlayTrack(string path)
        {
            try
            {
                Logger.Info("Playing neutral lobby music: " + Path.GetFileName(path));
                using (var source = new OggOpusFrameSource(path, GetVolume()))
                {
                    var clock = Stopwatch.StartNew();
                    var nextFrameAt = TimeSpan.Zero;
                    byte[] encodedFrame;
                    while (!_stop.IsCancellationRequested && IsEnabled())
                    {
                        source.Volume = GetVolume();
                        if (!source.TryReadFrame(out encodedFrame))
                        {
                            break;
                        }

                        QueueFrame(encodedFrame);
                        nextFrameAt += TimeSpan.FromMilliseconds(OggOpusFrameSource.FrameDurationMilliseconds);
                        var delay = nextFrameAt - clock.Elapsed;
                        if (delay > TimeSpan.Zero)
                        {
                            Wait(delay);
                        }
                        else if (delay < TimeSpan.FromMilliseconds(-200))
                        {
                            clock.Restart();
                            nextFrameAt = TimeSpan.Zero;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Unable to play neutral lobby music file: " + path);
            }
        }

        private void QueueFrame(byte[] encodedFrame)
        {
            var endpoints = GetNeutralLobbyEndpoints(_clients.Values);
            if (endpoints.Count == 0)
            {
                return;
            }

            var frequencies = _getLobbyFrequencies() ?? new double[0];
            if (frequencies.Length == 0)
            {
                LogConfigurationWarning("Lobby music is enabled, but no valid global lobby frequency is configured.");
                return;
            }

            var packet = new UDPVoicePacket
            {
                GuidBytes = _guidBytes,
                OriginalClientGuidBytes = _guidBytes,
                AudioPart1Bytes = encodedFrame,
                AudioPart1Length = (ushort)encodedFrame.Length,
                Frequencies = frequencies,
                Modulations = Enumerable.Repeat((byte)RadioInformation.Modulation.AM, frequencies.Length).ToArray(),
                UnitId = 0,
                PacketNumber = _packetNumber++
            };

            _outgoing.TryAdd(new OutgoingUDPPackets
            {
                OutgoingEndPoints = endpoints,
                ReceivedPacket = packet.EncodePacket()
            });
        }

        internal static List<IPEndPoint> GetNeutralLobbyEndpoints(IEnumerable<SRClient> clients)
        {
            return (clients ?? Enumerable.Empty<SRClient>())
                .Where(client => client != null && client.Coalition == 0 && client.VoipPort != null)
                .Select(client => client.VoipPort)
                .Distinct()
                .ToList();
        }

        internal static List<string> GetMusicFiles(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return new List<string>();
            }

            return Directory.GetFiles(directory, "*.ogg", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private string ResolveMusicDirectory()
        {
            var configured = Environment.ExpandEnvironmentVariables(_settings
                .GetGeneralSetting(ServerSettingsKeys.LOBBY_MUSIC_DIRECTORY)
                .StringValue ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = "LobbyMusic";
            }

            return Path.GetFullPath(Path.IsPathRooted(configured)
                ? configured
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configured));
        }

        private float GetVolume()
        {
            float volume;
            var configured = _settings.GetGeneralSetting(ServerSettingsKeys.LOBBY_MUSIC_VOLUME).StringValue;
            if (!float.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out volume))
            {
                volume = 0.25f;
            }

            return Math.Max(0f, Math.Min(1f, volume));
        }

        private bool IsEnabled()
        {
            return _settings.GetGeneralSetting(ServerSettingsKeys.LOBBY_MUSIC_ENABLED).BoolValue;
        }

        private void LogConfigurationWarning(string warning)
        {
            if (string.Equals(_lastConfigurationWarning, warning, StringComparison.Ordinal))
            {
                return;
            }

            _lastConfigurationWarning = warning;
            Logger.Warn(warning);
        }

        private void Wait(TimeSpan duration)
        {
            if (duration > TimeSpan.Zero)
            {
                _stop.Token.WaitHandle.WaitOne(duration);
            }
        }
    }
}
