using System;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Audio.Managers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NAudio.Wave;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Audio
{
    [TestClass]
    public class EventDrivenResamplerTests
    {
        private static readonly WaveFormat Output = new WaveFormat(AudioManager.INPUT_SAMPLE_RATE, 16, 1);

        [TestMethod]
        public void FormatTheDmoRejects_FallsBackToInbuiltResamplerAndStillResamples()
        {
            // The DMO resampler does not take 64-bit float; under Wine it also rejects the microphone's float format.
            var input = WaveFormat.CreateCustomFormat(WaveFormatEncoding.IeeeFloat, 48000, 1, 48000 * 8, 8, 64);

            var resampler = new EventDrivenResampler(false, input, Output);

            Assert.IsTrue(resampler.UsesInbuiltResampler, "did not fall back to the inbuilt resampler");
            AssertResamplesSine(resampler, SineBytes(input, s => BitConverter.GetBytes((double) s)));
        }

        [TestMethod]
        public void MonoFloatMicrophone_InbuiltResampler_ProducesAudio()
        {
            var input = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

            var resampler = new EventDrivenResampler(true, input, Output);

            AssertResamplesSine(resampler, SineBytes(input, s => BitConverter.GetBytes(s)));
        }

        [TestMethod]
        public void FormatTheDmoAccepts_KeepsDmoResampler()
        {
            var resampler = new EventDrivenResampler(false, new WaveFormat(48000, 16, 1), Output);

            Assert.IsFalse(resampler.UsesInbuiltResampler, "fell back although the DMO accepts 16-bit PCM");
        }

        private static byte[] SineBytes(WaveFormat format, Func<float, byte[]> toBytes)
        {
            // 100 ms of a 440 Hz tone at half scale.
            var samples = format.SampleRate / 10;
            var bytesPerSample = format.BitsPerSample / 8;
            var bytes = new byte[samples * bytesPerSample];
            for (var i = 0; i < samples; i++)
            {
                var sample = (float) (0.5 * Math.Sin(2 * Math.PI * 440 * i / format.SampleRate));
                Buffer.BlockCopy(toBytes(sample), 0, bytes, i * bytesPerSample, bytesPerSample);
            }

            return bytes;
        }

        private static void AssertResamplesSine(EventDrivenResampler resampler, byte[] input)
        {
            var output = resampler.Resample(input, input.Length);

            Assert.IsTrue(output.Length > 0, "no samples came out of the resampler");
            short peak = 0;
            foreach (var s in output)
            {
                peak = Math.Max(peak, Math.Abs(s));
            }

            // Half scale in, so roughly 16384 out.
            Assert.IsTrue(peak > 12000 && peak < 20000, $"peak {peak} is not about half scale");
        }
    }
}
