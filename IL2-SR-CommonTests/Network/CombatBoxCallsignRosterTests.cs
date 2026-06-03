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
    }
}
