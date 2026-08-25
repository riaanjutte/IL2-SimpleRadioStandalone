using Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.Helpers
{
    [TestClass]
    public class AirfieldCodeLookupTests
    {
        [TestMethod]
        public void ResolvesCaseAccentAndPunctuationVariantsToCanonicalCode()
        {
            Assert.AreEqual("BIE", AirfieldCodeLookup.GetCode("Bierset"));
            Assert.AreEqual("BIE", AirfieldCodeLookup.GetCode("liège"));
            Assert.AreEqual("BIE", AirfieldCodeLookup.GetCode("LIEGE/BIERSET"));
        }

        [TestMethod]
        public void ResolvesMissionPrefixesAndOperationalSuffixes()
        {
            Assert.AreEqual("EIN", AirfieldCodeLookup.GetCode("B-78 Eindhoven BSP"));
            Assert.AreEqual("ASC", AirfieldCodeLookup.GetCode("Y-29 Asch FSP"));
            Assert.AreEqual("AAC", AirfieldCodeLookup.GetCode("ef_Aachen"));
            Assert.AreEqual("KOC", AirfieldCodeLookup.GetCode("Axis AF Kochegarovo FWD"));
        }

        [TestMethod]
        public void ResolvesOperationalAliasesNotPresentInBaseMapData()
        {
            Assert.AreEqual("DEU", AirfieldCodeLookup.GetCode("B-70 Deurne-Antwerpen BSP"));
            Assert.AreEqual("MTZ", AirfieldCodeLookup.GetCode("Y-70 Maitzborn"));
            Assert.AreEqual("MAY", AirfieldCodeLookup.GetCode("Mayen Recovery Airstrip"));
            Assert.AreEqual("GEL", AirfieldCodeLookup.GetCode("Gelenjik"));
        }

        [TestMethod]
        public void UsesStableFallbackForUnknownAirfields()
        {
            Assert.AreEqual("MYS", AirfieldCodeLookup.GetCode("Mystery Field"));
            Assert.AreEqual("ABX", AirfieldCodeLookup.GetCode("AB"));
            Assert.AreEqual(string.Empty, AirfieldCodeLookup.GetCode("  "));
        }

        [TestMethod]
        public void EmbeddedTableLoadsAndContainsAllGreatBattlesMapEntries()
        {
            Assert.IsTrue(AirfieldCodeLookup.KnownEntryCount >= 794);
            Assert.IsTrue(AirfieldCodeLookup.KnownAliasCount >= 840);
        }
    }
}
