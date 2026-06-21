using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Network
{
    [TestClass]
    public class CombatBoxCallsignRosterTests
    {
        [TestMethod]
        public void ParseExtractsAssignedCallsignsByPlayerNameAndCoalition()
        {
            const string roster = @"{
  ""players"": [
    {
      ""name"": ""=DW=_Biffas"",
      ""coalition"": ""Allies"",
      ""coalitionCode"": 1,
      ""callsign"": ""FIREBIRD-1"",
      ""callsignAssignedAtUtc"": ""2026-06-02T20:42:44.6643124Z""
    },
    {
      ""name"": ""=DW=_Biffas"",
      ""coalition"": ""Axis"",
      ""coalitionCode"": 2,
      ""callsign"": ""FALCON-2"",
      ""callsignAssignedAtUtc"": ""2026-06-02T20:49:57.8510347Z""
    }
  ]
}";

            var callsigns = CombatBoxCallsignRoster.Parse(roster);

            Assert.AreEqual(2, callsigns.Count);
            Assert.AreEqual("FIREBIRD-1", callsigns[new CallsignRosterKey("=DW=_Biffas", 1)]);
            Assert.AreEqual("FALCON-2", callsigns[new CallsignRosterKey("=DW=_Biffas", 2)]);
            Assert.IsFalse(callsigns.ContainsKey(new CallsignRosterKey("=DW=_Biffas", 0)));
        }

        [TestMethod]
        public void ParseIgnoresBlankCallsignsAndUnknownCoalitions()
        {
            const string roster = @"{
  ""players"": [
    { ""name"": ""NoCallsign"", ""coalitionCode"": 1, ""callsign"": """" },
    { ""name"": ""NoCoalition"", ""coalitionCode"": 0, ""callsign"": ""MAGIC"" },
    { ""name"": ""Valid"", ""coalitionCode"": 2, ""callsign"": ""COWBOY-1"" }
  ]
}";

            var callsigns = CombatBoxCallsignRoster.Parse(roster);

            Assert.AreEqual(1, callsigns.Count);
            Assert.AreEqual("COWBOY-1", callsigns[new CallsignRosterKey("Valid", 2)]);
        }

        [TestMethod]
        public void ParseReturnsEmptyDictionaryForEmptyOrMissingPlayersRoster()
        {
            Assert.AreEqual(0, CombatBoxCallsignRoster.Parse(null).Count);
            Assert.AreEqual(0, CombatBoxCallsignRoster.Parse("").Count);
            Assert.AreEqual(0, CombatBoxCallsignRoster.Parse("{}").Count);
            Assert.AreEqual(0, CombatBoxCallsignRoster.Parse(@"{ ""players"": null }").Count);
        }

        [TestMethod]
        public void ParseTrimsCallsignAndMatchesPlayerNameCaseInsensitively()
        {
            const string roster = @"{
  ""players"": [
    { ""name"": ""  CAG_SonoftheMorning  "", ""coalitionCode"": 1, ""callsign"": ""  STUD-3  "" }
  ]
}";

            var callsigns = CombatBoxCallsignRoster.Parse(roster);

            Assert.AreEqual(1, callsigns.Count);
            Assert.AreEqual("STUD-3", callsigns[new CallsignRosterKey("cag_sonofthemorning", 1)]);
            Assert.AreEqual("STUD-3", callsigns[new CallsignRosterKey(" CAG_SonoftheMorning ", 1)]);
        }

        [TestMethod]
        public void ParseUsesCoalitionCodeAsPartOfLookupKey()
        {
            const string roster = @"{
  ""players"": [
    { ""name"": ""Broadway"", ""coalitionCode"": 1, ""callsign"": ""CHECKMATE"" },
    { ""name"": ""Broadway"", ""coalitionCode"": 2, ""callsign"": ""DARKSTAR"" }
  ]
}";

            var callsigns = CombatBoxCallsignRoster.Parse(roster);

            Assert.AreEqual("CHECKMATE", callsigns[new CallsignRosterKey("Broadway", 1)]);
            Assert.AreEqual("DARKSTAR", callsigns[new CallsignRosterKey("Broadway", 2)]);
            Assert.IsFalse(callsigns.ContainsKey(new CallsignRosterKey("Broadway", 0)));
        }

        [TestMethod]
        public void ParseLastDuplicateEntryWins()
        {
            const string roster = @"{
  ""players"": [
    { ""name"": ""DEFCON"", ""coalitionCode"": 2, ""callsign"": ""OLD-CALLSIGN"" },
    { ""name"": ""defcon"", ""coalitionCode"": 2, ""callsign"": ""NEW-CALLSIGN"" }
  ]
}";

            var callsigns = CombatBoxCallsignRoster.Parse(roster);

            Assert.AreEqual(1, callsigns.Count);
            Assert.AreEqual("NEW-CALLSIGN", callsigns[new CallsignRosterKey("DEFCON", 2)]);
        }

        [TestMethod]
        public void ParseIgnoresNullPlayersMissingNamesAndWhitespaceNames()
        {
            const string roster = @"{
  ""players"": [
    null,
    { ""coalitionCode"": 1, ""callsign"": ""NO-NAME"" },
    { ""name"": ""   "", ""coalitionCode"": 1, ""callsign"": ""BLANK-NAME"" },
    { ""name"": ""Valid"", ""coalitionCode"": 1, ""callsign"": ""VALID-CALLSIGN"" }
  ]
}";

            var callsigns = CombatBoxCallsignRoster.Parse(roster);

            Assert.AreEqual(1, callsigns.Count);
            Assert.AreEqual("VALID-CALLSIGN", callsigns[new CallsignRosterKey("Valid", 1)]);
        }

        [TestMethod]
        public void ParseAssignmentsExtractsVehiclesWhenAvailable()
        {
            const string roster = @"{
  ""players"": [
    { ""name"": ""Assigned"", ""coalitionCode"": 1, ""callsign"": ""RAVEN-1"", ""vehicle"": ""  Spitfire Mk.IXe  "" },
    { ""name"": ""VehicleOnly"", ""coalitionCode"": 1, ""vehicle"": ""Bf 109 G-14"" },
    { ""name"": ""Empty"", ""coalitionCode"": 1, ""callsign"": """", ""vehicle"": """" }
  ]
}";

            var assignments = CombatBoxCallsignRoster.ParseAssignments(roster);

            Assert.AreEqual(2, assignments.Count);
            Assert.AreEqual("RAVEN-1", assignments[new CallsignRosterKey("Assigned", 1)].Callsign);
            Assert.AreEqual("Spitfire Mk.IXe", assignments[new CallsignRosterKey("Assigned", 1)].Vehicle);
            Assert.AreEqual("", assignments[new CallsignRosterKey("VehicleOnly", 1)].Callsign);
            Assert.AreEqual("Bf 109 G-14", assignments[new CallsignRosterKey("VehicleOnly", 1)].Vehicle);
        }
    }
}
