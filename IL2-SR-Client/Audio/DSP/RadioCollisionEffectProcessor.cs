using System;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.DSP
{
    public sealed class RadioCollisionEffectProcessor
    {
        private const double TwoPi = Math.PI * 2.0;
        private const double SampleRate = 48000.0;

        private double _flutterPhase;
        private double _buzzPhase;
        private uint _noiseState;
        private int _samplesUntilDropout;
        private int _dropoutSamplesRemaining;

        public RadioCollisionEffectProcessor(uint seed)
        {
            _noiseState = seed == 0 ? 0x6d2b79f5u : seed;
            _samplesUntilDropout = 1200 + (int)(NextRandom() % 1800);
        }

        public void Apply(short[] samples)
        {
            if (samples == null || samples.Length == 0)
            {
                return;
            }

            var flutterStep = TwoPi * 31.0 / SampleRate;
            var buzzStep = TwoPi * 117.0 / SampleRate;

            for (var i = 0; i < samples.Length; i++)
            {
                if (_dropoutSamplesRemaining > 0)
                {
                    _dropoutSamplesRemaining--;
                }
                else if (--_samplesUntilDropout <= 0)
                {
                    _dropoutSamplesRemaining = 120 + (int)(NextRandom() % 280);
                    _samplesUntilDropout = 1200 + (int)(NextRandom() % 3000);
                }

                var input = samples[i] / 32768.0;
                var flutter = 0.56 +
                              0.22 * Math.Sin(_flutterPhase) +
                              0.10 * Math.Sin(_buzzPhase);
                if (_dropoutSamplesRemaining > 0)
                {
                    flutter *= 0.18;
                }

                var noise = NextNoise() * (0.035 + 0.025 * Math.Abs(input));
                var distorted = input * flutter + noise;
                distorted = Math.Max(-0.58, Math.Min(0.58, distorted));
                distorted = distorted / 0.58 * 0.86;

                samples[i] = (short)(distorted * short.MaxValue);

                _flutterPhase = WrapPhase(_flutterPhase + flutterStep);
                _buzzPhase = WrapPhase(_buzzPhase + buzzStep);
            }
        }

        public static uint CreateSeed(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                if (!string.IsNullOrEmpty(value))
                {
                    foreach (var character in value)
                    {
                        hash ^= character;
                        hash *= 16777619u;
                    }
                }

                return hash;
            }
        }

        private double NextNoise()
        {
            return (NextRandom() / (double)uint.MaxValue) * 2.0 - 1.0;
        }

        private uint NextRandom()
        {
            var value = _noiseState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _noiseState = value;
            return value;
        }

        private static double WrapPhase(double phase)
        {
            return phase >= TwoPi ? phase - TwoPi : phase;
        }
    }
}
