using System.Collections.Generic;
using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.PilotRoster;
using Ciribob.IL2.SimpleRadio.Standalone.Common;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class PilotRosterBuilderTests
    {
        private const int FriendlyCoalition = 2;
        private const int EnemyCoalition = 1;

        [TestMethod]
        public void BuildIncludesOnlyPilotsOnLocalCoalition()
        {
            var localState = CreateState(FriendlyCoalition, 1, 2);
            var roster = PilotRosterBuilder.Build(localState, new[]
            {
                CreateClient("friendly-1", "Broadway", "STUD 3", FriendlyCoalition, 3, 4),
                CreateClient("enemy-1", "Enemy", "BANDIT", EnemyCoalition, 5, 6),
                CreateClient("neutral-1", "Lobby", "LOBBY", 0, 7, 8)
            });

            Assert.AreEqual(1, roster.Count);
            Assert.AreEqual("STUD 3", roster[0].Callsign);
            Assert.AreEqual("BROADWAY", roster[0].PilotName);
            Assert.AreEqual("CHN 3", roster[0].Radio1Channel);
            Assert.AreEqual("CHN 4", roster[0].Radio2Channel);
        }

        [TestMethod]
        public void BuildReturnsEmptyWhenLocalCoalitionIsNeutral()
        {
            var roster = PilotRosterBuilder.Build(CreateState(0, 1, 2), new[]
            {
                CreateClient("friendly-1", "Broadway", "STUD 3", FriendlyCoalition, 3, 4)
            });

            Assert.AreEqual(0, roster.Count);
        }

        [TestMethod]
        public void BuildShowsEmptyCallsignAndUnavailableChannelsAsDashes()
        {
            var disabledState = CreateState(FriendlyCoalition, 1, 2);
            disabledState.radios[1].modulation = RadioInformation.Modulation.DISABLED;
            disabledState.radios[2].modulation = RadioInformation.Modulation.DISABLED;

            var roster = PilotRosterBuilder.Build(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                new SRClient
                {
                    ClientGuid = "friendly-1",
                    Name = "Unassigned",
                    Coalition = FriendlyCoalition,
                    GameState = disabledState
                }
            });

            Assert.AreEqual(1, roster.Count);
            Assert.AreEqual("--", roster[0].Callsign);
            Assert.AreEqual("--", roster[0].Radio1Channel);
            Assert.AreEqual("--", roster[0].Radio2Channel);
        }

        [TestMethod]
        public void BuildSortsCallsignsBeforeUnassignedPilots()
        {
            var roster = PilotRosterBuilder.Build(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "Zulu", "", FriendlyCoalition, 3, 4),
                CreateClient("friendly-2", "Alpha", "VIPER", FriendlyCoalition, 5, 6)
            }).ToList();

            Assert.AreEqual("VIPER", roster[0].Callsign);
            Assert.AreEqual("ZULU", roster[1].PilotName);
        }

        [TestMethod]
        public void BuildExcludesCombatBoxInfrastructureClients()
        {
            var roster = PilotRosterBuilder.Build(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "Axis Airfield", "", FriendlyCoalition, 2, 0),
                CreateClient("friendly-2", "Axis Command", "", FriendlyCoalition, 1, 0),
                CreateClient("friendly-3", "-TBAS-Haluter", "", FriendlyCoalition, 1, 2),
                CreateClient("friendly-4", "KRAKEN__RCI", "RAVEN-3", FriendlyCoalition, 4, 2)
            }).ToList();

            Assert.AreEqual(2, roster.Count);
            Assert.IsFalse(roster.Any(entry => entry.PilotName == "AXIS AIRFIELD"));
            Assert.IsFalse(roster.Any(entry => entry.PilotName == "AXIS COMMAND"));
            Assert.IsTrue(roster.Any(entry => entry.PilotName == "-TBAS-HALUTER"));
            Assert.IsTrue(roster.Any(entry => entry.PilotName == "KRAKEN__RCI"));
        }

        [TestMethod]
        public void BuildIncludesFriendlyPlayersWithAndWithoutCallsigns()
        {
            var roster = PilotRosterBuilder.Build(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "Assigned", "COWBOY-6", FriendlyCoalition, 3, 2),
                CreateClient("friendly-2", "NoCallsignOne", "", FriendlyCoalition, 1, 2),
                CreateClient("friendly-3", "NoCallsignTwo", null, FriendlyCoalition, 4, 5)
            }).ToList();

            Assert.AreEqual(3, roster.Count);
            Assert.IsTrue(roster.Any(entry => entry.PilotName == "ASSIGNED" && entry.Callsign == "COWBOY-6"));
            Assert.IsTrue(roster.Any(entry => entry.PilotName == "NOCALLSIGNONE" && entry.Callsign == "--"));
            Assert.IsTrue(roster.Any(entry => entry.PilotName == "NOCALLSIGNTWO" && entry.Callsign == "--"));
        }


        [TestMethod]
        public void BuildIncludesAssignedVehicleWhenAvailable()
        {
            var roster = PilotRosterBuilder.Build(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "Assigned", "COWBOY-6", FriendlyCoalition, 3, 2, "  P-51D-15  ")
            }).ToList();

            Assert.AreEqual(1, roster.Count);
            Assert.AreEqual("P-51D-15", roster[0].Vehicle);
            Assert.IsTrue(roster[0].HasVehicle);
        }

        [TestMethod]
        public void BuildActiveSquadOpsSummaryGroupsFriendlySquadsByOperationalChannel()
        {
            var summary = PilotRosterBuilder.BuildActiveSquadOpsSummary(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "=TBAS=Mayhem", "", FriendlyCoalition, 3, 2),
                CreateClient("friendly-2", "=TBAS=Haluter", "", FriendlyCoalition, 3, 4),
                CreateClient("friendly-3", "-JG4-PilotOne", "", FriendlyCoalition, 5, 2),
                CreateClient("friendly-4", "-JG4-PilotTwo", "", FriendlyCoalition, 5, 6),
                CreateClient("friendly-5", "[EMS]PilotOne", "", FriendlyCoalition, 9, 2),
                CreateClient("friendly-6", "[EMS]PilotTwo", "", FriendlyCoalition, 9, 2),
                CreateClient("enemy-1", "=TBAS=Enemy", "", EnemyCoalition, 3, 2)
            });

            Assert.AreEqual("ACTIVE SQUAD OPS: CH3 TBAS(2) | CH5 JG4(2) | CH9 EMS(2)", summary);
        }

        [TestMethod]
        public void BuildActiveSquadOpsSummaryRequiresAtLeastTwoPilotsPerSquadChannel()
        {
            var summary = PilotRosterBuilder.BuildActiveSquadOpsSummary(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "=TBAS=Solo", "", FriendlyCoalition, 3, 2),
                CreateClient("friendly-2", "=DW=One", "", FriendlyCoalition, 4, 2),
                CreateClient("friendly-3", "=DW=Two", "", FriendlyCoalition, 5, 2)
            });

            Assert.AreEqual(string.Empty, summary);
        }

        [TestMethod]
        public void BuildActiveSquadOpsSummaryIgnoresLobbyChannelsAndUnrecognisedNames()
        {
            var summary = PilotRosterBuilder.BuildActiveSquadOpsSummary(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "=TBAS=One", "", FriendlyCoalition, 1, 2),
                CreateClient("friendly-2", "=TBAS=Two", "", FriendlyCoalition, 1, 2),
                CreateClient("friendly-3", "Broadway", "", FriendlyCoalition, 7, 2),
                CreateClient("friendly-4", "Ginger", "", FriendlyCoalition, 7, 2)
            });

            Assert.AreEqual(string.Empty, summary);
        }

        [TestMethod]
        public void BuildActiveSquadOpsSummaryDetectsCommonSquadTagFormats()
        {
            var summary = PilotRosterBuilder.BuildActiveSquadOpsSummary(CreateState(FriendlyCoalition, 1, 2), new[]
            {
                CreateClient("friendly-1", "JG27_PilotOne", "", FriendlyCoalition, 6, 2),
                CreateClient("friendly-2", "JG27-PilotTwo", "", FriendlyCoalition, 6, 2),
                CreateClient("friendly-3", "332FG.Gordon", "", FriendlyCoalition, 8, 2),
                CreateClient("friendly-4", "332FG-Wingman", "", FriendlyCoalition, 8, 2)
            });

            Assert.AreEqual("ACTIVE SQUAD OPS: CH6 JG27(2) | CH8 332FG(2)", summary);
        }

        private static SRClient CreateClient(
            string guid,
            string name,
            string callsign,
            int coalition,
            int radio1Channel,
            int radio2Channel,
            string vehicle = null)
        {
            return new SRClient
            {
                ClientGuid = guid,
                Name = name,
                AssignedCallsign = callsign,
                AssignedVehicle = vehicle,
                Coalition = coalition,
                GameState = CreateState(coalition, radio1Channel, radio2Channel)
            };
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
    }
}
