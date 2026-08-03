using System;
using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.DSP;
using Ciribob.IL2.SimpleRadio.Standalone.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Audio
{
    [TestClass]
    public class RadioCollisionEffectProcessorTests
    {
        [TestMethod]
        public void CollisionProcessorChangesAudioWithoutClipping()
        {
            var samples = Enumerable.Repeat((short)12000, 4800).ToArray();
            var original = samples.ToArray();
            var processor = new RadioCollisionEffectProcessor(12345);

            processor.Apply(samples);

            Assert.IsFalse(original.SequenceEqual(samples));
            Assert.IsTrue(samples.Max(sample => Math.Abs((int)sample)) < short.MaxValue);
        }

        [TestMethod]
        public void CollisionProcessorIsDeterministicForSenderSeed()
        {
            var first = Enumerable.Repeat((short)9000, 2400).ToArray();
            var second = first.ToArray();

            new RadioCollisionEffectProcessor(42).Apply(first);
            new RadioCollisionEffectProcessor(42).Apply(second);

            CollectionAssert.AreEqual(first, second);
        }

        [TestMethod]
        public void LobbyMusicNeverUsesRadioCollisionEffect()
        {
            var lobbyMusic = new ClientAudio
            {
                IsLobbyMusic = true,
                IsRadioCollision = true,
                ReceivedRadio = 1
            };
            var normalRadio = new ClientAudio
            {
                IsRadioCollision = true,
                ReceivedRadio = 1
            };

            Assert.IsFalse(lobbyMusic.ShouldApplyRadioCollisionEffect);
            Assert.IsTrue(normalRadio.ShouldApplyRadioCollisionEffect);
        }
    }
}
