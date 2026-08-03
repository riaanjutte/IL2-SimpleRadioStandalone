using System;
using FragLabs.Audio.Codecs;
using NVorbis;

namespace Ciribob.IL2.SimpleRadio.Standalone.Server.Audio
{
    internal sealed class OggOpusFrameSource : IDisposable
    {
        internal const int OutputSampleRate = 16000;
        internal const int FrameDurationMilliseconds = 40;
        internal const int SamplesPerFrame = OutputSampleRate / 1000 * FrameDurationMilliseconds;

        private readonly VorbisReader _reader;
        private readonly OpusEncoder _encoder;
        private readonly float[] _sourceBuffer;
        private readonly int _channels;
        private readonly double _sourceStep;
        private float _volume;
        private int _sourceBufferCount;
        private int _sourceBufferPosition;
        private double _sourcePhase;
        private float _currentSample;
        private float _nextSample;
        private bool _initialized;
        private bool _sourceEnded;

        public OggOpusFrameSource(string path, float volume)
        {
            _reader = new VorbisReader(path);
            _channels = _reader.Channels;
            if (_channels <= 0 || _reader.SampleRate <= 0)
            {
                throw new InvalidOperationException("The Ogg file has an invalid audio format.");
            }

            _sourceStep = (double)_reader.SampleRate / OutputSampleRate;
            Volume = volume;
            _sourceBuffer = new float[4096 * _channels];
            _encoder = OpusEncoder.Create(OutputSampleRate, 1,
                FragLabs.Audio.Codecs.Opus.Application.Audio);
        }

        public float Volume
        {
            get { return _volume; }
            set { _volume = Math.Max(0f, Math.Min(1f, value)); }
        }

        public bool TryReadFrame(out byte[] encodedFrame)
        {
            encodedFrame = null;
            var pcm = new byte[SamplesPerFrame * sizeof(short)];
            var samplesWritten = 0;

            if (!EnsureInitialized())
            {
                return false;
            }

            while (samplesWritten < SamplesPerFrame)
            {
                var sample = _currentSample + (_nextSample - _currentSample) * (float)_sourcePhase;
                var pcmSample = (short)Math.Round(Math.Max(-1f, Math.Min(1f, sample * _volume)) * short.MaxValue);
                pcm[samplesWritten * 2] = (byte)(pcmSample & 0xff);
                pcm[samplesWritten * 2 + 1] = (byte)((pcmSample >> 8) & 0xff);
                samplesWritten++;

                _sourcePhase += _sourceStep;
                while (_sourcePhase >= 1d)
                {
                    _currentSample = _nextSample;
                    if (!TryReadMonoSample(out _nextSample))
                    {
                        _sourceEnded = true;
                        break;
                    }

                    _sourcePhase -= 1d;
                }

                if (_sourceEnded)
                {
                    break;
                }
            }

            if (samplesWritten == 0)
            {
                return false;
            }

            int encodedLength;
            var encodedBuffer = _encoder.Encode(pcm, pcm.Length, out encodedLength);
            if (encodedLength <= 0)
            {
                return false;
            }

            encodedFrame = new byte[encodedLength];
            Buffer.BlockCopy(encodedBuffer, 0, encodedFrame, 0, encodedLength);
            return true;
        }

        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return !_sourceEnded;
            }

            _initialized = true;
            if (!TryReadMonoSample(out _currentSample))
            {
                _sourceEnded = true;
                return false;
            }

            if (!TryReadMonoSample(out _nextSample))
            {
                _nextSample = _currentSample;
            }

            return true;
        }

        private bool TryReadMonoSample(out float sample)
        {
            sample = 0f;
            if (_sourceBufferPosition + _channels > _sourceBufferCount)
            {
                _sourceBufferCount = _reader.ReadSamples(_sourceBuffer, 0, _sourceBuffer.Length);
                _sourceBufferPosition = 0;
                if (_sourceBufferCount < _channels)
                {
                    return false;
                }
            }

            for (var channel = 0; channel < _channels; channel++)
            {
                sample += _sourceBuffer[_sourceBufferPosition++];
            }

            sample /= _channels;
            return true;
        }

        public void Dispose()
        {
            _encoder?.Dispose();
            _reader?.Dispose();
        }
    }
}
