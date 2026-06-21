using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class MemoryLeakGuardTests
    {
        [TestMethod]
        public void PilotRosterBuildDoesNotRetainSourceClientsOrGameStates()
        {
            var weakReferences = BuildRosterAndReturnSourceObjectReferences(out var roster);

            ForceFullCollection();

            foreach (var weakReference in weakReferences)
            {
                Assert.IsFalse(
                    weakReference.IsAlive,
                    "Pilot roster refreshes must not retain source SRClient or PlayerGameState objects after projecting display rows.");
            }

            GC.KeepAlive(roster);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static IList<WeakReference> BuildRosterAndReturnSourceObjectReferences(out IList<PilotRosterEntry> roster)
        {
            var localState = CreateState(2, 1, 2);
            var sourceClient = new SRClient
            {
                ClientGuid = Guid.NewGuid().ToString("N"),
                Name = "Leak Test Pilot",
                AssignedCallsign = "TEST-1",
                Coalition = 2,
                GameState = CreateState(2, 3, 4)
            };

            var weakReferences = new List<WeakReference>
            {
                new WeakReference(sourceClient),
                new WeakReference(sourceClient.GameState)
            };

            roster = PilotRosterBuilder.Build(localState, new[] { sourceClient });
            Assert.AreEqual(1, roster.Count, "The test setup must produce a roster row before checking object retention.");

            return weakReferences;
        }

        private static PlayerGameState CreateState(int coalition, int radio1Channel, int radio2Channel)
        {
            var state = new PlayerGameState { coalition = (short)coalition };
            SetRadioChannel(state.radios[1], radio1Channel);
            SetRadioChannel(state.radios[2], radio2Channel);
            return state;
        }

        private static void SetRadioChannel(RadioInformation radio, int channel)
        {
            radio.modulation = RadioInformation.Modulation.AM;
            radio.channel = channel;
            radio.freq = PlayerGameState.START_FREQ + PlayerGameState.CHANNEL_OFFSET * channel;
        }

        private static void ForceFullCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
