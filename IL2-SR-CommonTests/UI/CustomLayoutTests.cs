using System.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Tests.UI
{
    [TestClass]
    public class CustomLayoutTests
    {
        private static string Rows(CustomLayout layout, int tab)
        {
            return string.Join(" ", layout.Tabs[tab].Rows.Select(r => "[" + string.Join(",", r) + "]"));
        }

        [TestMethod]
        public void DefaultHasTheFourBuiltInTabsInOrder()
        {
            var layout = CustomLayout.Default();

            CollectionAssert.AreEqual(new[] { "general", "controls", "settings", "help" }, layout.Tabs.Select(t => t.BuiltIn).ToArray());
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
            Assert.AreEqual("[controls]", Rows(layout, 1));
            Assert.AreEqual("[globalSettings] [rciController] [audioOptions] [profile] [profileSettings]", Rows(layout, 2));
            Assert.AreEqual("[help]", Rows(layout, 3));
            Assert.AreEqual(0, layout.Hidden.Count);
        }

        [TestMethod]
        public void NothingStoredSeedsCollapsedFromTheOldFlags()
        {
            var layout = CustomLayout.Load(null, true, false);

            CollectionAssert.AreEquivalent(new[] { "server+overlays" }, layout.Collapsed.ToArray());
            CollectionAssert.AreEquivalent(new[] { "favourites" }, CustomLayout.Load("", false, true).Collapsed.ToArray());
        }

        [TestMethod]
        public void UnreadableStoredLayoutFallsBackToDefaultWithoutSeeding()
        {
            var layout = CustomLayout.Load("not base64", true, true);

            Assert.AreEqual(Rows(CustomLayout.Default(), 0), Rows(layout, 0));
            Assert.AreEqual(0, layout.Collapsed.Count);
        }

        [TestMethod]
        public void ValidBase64OfNonJsonFallsBackToDefault()
        {
            var layout = CustomLayout.Load(System.Convert.ToBase64String(new byte[] { 1, 2, 3 }), true, true);

            Assert.AreEqual(4, layout.Tabs.Count);
            Assert.AreEqual(0, layout.Collapsed.Count);
        }

        [TestMethod]
        public void SaveAndLoadRoundTripKeepsNamesAndCollapsedRows()
        {
            const string name = "In \"flight\"; #1 Größe ü 🛩";
            var layout = CustomLayout.Default();
            layout.AddTab(name);
            layout.Collapsed.Add("favourites");

            var loaded = CustomLayout.Load(layout.Save(), false, false);

            Assert.AreEqual(5, loaded.Tabs.Count);
            Assert.AreEqual(name, loaded.Tabs[4].Name);
            CollectionAssert.AreEquivalent(new[] { "favourites" }, loaded.Collapsed.ToArray());
        }

        [TestMethod]
        public void NormaliseDropsUnknownKeysAndLaterDuplicates()
        {
            var layout = CustomLayout.Default();
            layout.Tabs[1].Rows.Add(new System.Collections.Generic.List<string> { "foo" });
            layout.Tabs[1].Rows.Add(new System.Collections.Generic.List<string> { "microphone" });
            layout.Hidden.Add("speakers");

            layout.Normalise();

            Assert.AreEqual("[controls]", Rows(layout, 1));
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
            Assert.AreEqual(0, layout.Hidden.Count);
        }

        [TestMethod]
        public void NormaliseAppendsAMissingPanelToTheFirstTab()
        {
            var layout = CustomLayout.Default();
            layout.Tabs.RemoveAt(3);

            layout.Normalise();

            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites] [help]", Rows(layout, 0));
        }

        [TestMethod]
        public void NormaliseKeepsOnlyTheFirstTwoOfARowAndTheRestGoBackToTheFirstTab()
        {
            var layout = CustomLayout.Default();
            layout.Tabs[1].Rows[0] = new System.Collections.Generic.List<string> { "controls", "help", "favourites" };
            layout.Tabs[0].Rows.RemoveAt(2);
            layout.Tabs.RemoveAt(3);

            layout.Normalise();

            Assert.AreEqual("[controls,help]", Rows(layout, 1));
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
        }

        [TestMethod]
        public void NormaliseDropsEmptyRowsBlankNamesAndStaleCollapsedKeys()
        {
            var layout = CustomLayout.Default();
            layout.Tabs[0].Name = "  ";
            layout.Tabs[0].Rows.Add(new System.Collections.Generic.List<string>());
            layout.Collapsed.Add("nonexistent");
            layout.Collapsed.Add("favourites");

            layout.Normalise();

            Assert.IsNull(layout.Tabs[0].Name);
            Assert.AreEqual(3, layout.Tabs[0].Rows.Count);
            CollectionAssert.AreEquivalent(new[] { "favourites" }, layout.Collapsed.ToArray());
        }

        [TestMethod]
        public void DeletingATabHidesItsPanels()
        {
            var layout = CustomLayout.Default();

            Assert.IsTrue(layout.DeleteTab(1));

            Assert.AreEqual(3, layout.Tabs.Count);
            CollectionAssert.AreEqual(new[] { "controls" }, layout.Hidden.ToArray());
        }

        [TestMethod]
        public void TheLastTabCannotBeDeleted()
        {
            var layout = CustomLayout.Default();
            layout.DeleteTab(3);
            layout.DeleteTab(2);
            layout.DeleteTab(1);

            Assert.IsFalse(layout.DeleteTab(0));
            Assert.AreEqual(1, layout.Tabs.Count);
        }

        [TestMethod]
        public void PairJoinsTwoSingleRowsAndUnpairSplitsThemBack()
        {
            var layout = CustomLayout.Default();

            Assert.IsTrue(layout.Pair(2, 0));
            Assert.AreEqual("[globalSettings,rciController] [audioOptions] [profile] [profileSettings]", Rows(layout, 2));

            layout.Unpair(2, 0);
            Assert.AreEqual("[globalSettings] [rciController] [audioOptions] [profile] [profileSettings]", Rows(layout, 2));
        }

        [TestMethod]
        public void PairFailsForAPairOrWhenThereIsNoRowBelow()
        {
            var layout = CustomLayout.Default();

            Assert.IsFalse(layout.Pair(0, 0));
            Assert.IsFalse(layout.Pair(0, 1));
            Assert.IsFalse(layout.Pair(0, 2));
            Assert.IsFalse(layout.Pair(1, 0));
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
        }

        [TestMethod]
        public void PairingMovingAndUnpairingClearTheCollapsedKey()
        {
            var layout = CustomLayout.Default();
            layout.Collapsed.Add("server+overlays");
            layout.Collapsed.Add("favourites");
            layout.Collapsed.Add("globalSettings");

            layout.Unpair(0, 1);
            layout.MoveRow(0, 3, -1);
            layout.Pair(2, 0);

            Assert.AreEqual(0, layout.Collapsed.Count);
        }

        [TestMethod]
        public void MoveRowToTabAppendsToTheTarget()
        {
            var layout = CustomLayout.Default();

            layout.MoveRowToTab(0, 2, 3);

            Assert.AreEqual("[microphone,speakers] [server,overlays]", Rows(layout, 0));
            Assert.AreEqual("[help] [favourites]", Rows(layout, 3));
        }

        [TestMethod]
        public void MoveTabAndMoveRowReorderAndIgnoreOutOfRange()
        {
            var layout = CustomLayout.Default();

            layout.MoveTab(0, 1);
            layout.MoveTab(0, -1);
            layout.MoveTab(3, 1);
            CollectionAssert.AreEqual(new[] { "controls", "general", "settings", "help" }, layout.Tabs.Select(t => t.BuiltIn).ToArray());

            layout.MoveRow(1, 0, 1);
            layout.MoveRow(1, 0, -1);
            Assert.AreEqual("[server,overlays] [microphone,speakers] [favourites]", Rows(layout, 1));
        }

        [TestMethod]
        public void HidingARowAndShowingItsPanelsRoundTrips()
        {
            var layout = CustomLayout.Default();
            layout.Collapsed.Add("favourites");

            layout.HideRow(0, 2);
            CollectionAssert.AreEqual(new[] { "favourites" }, layout.Hidden.ToArray());
            Assert.AreEqual(0, layout.Collapsed.Count);

            layout.ShowPanel("favourites", 1);
            Assert.AreEqual(0, layout.Hidden.Count);
            Assert.AreEqual("[controls] [favourites]", Rows(layout, 1));
        }

        [TestMethod]
        public void RenamingToBlankIsRefused()
        {
            var layout = CustomLayout.Default();

            Assert.IsFalse(layout.RenameTab(0, "  "));
            Assert.IsNull(layout.Tabs[0].Name);
            Assert.IsTrue(layout.RenameTab(0, " Flight "));
            Assert.AreEqual("Flight", layout.Tabs[0].Name);
        }

        [TestMethod]
        public void CloneIsIndependent()
        {
            var layout = CustomLayout.Default();
            var clone = layout.Clone();

            clone.HideRow(0, 0);

            Assert.AreEqual(0, layout.Hidden.Count);
            Assert.AreEqual(2, clone.Hidden.Count);
        }

        [TestMethod]
        public void StoredLayoutWithOnlyNullTabsFallsBackToDefault()
        {
            var stored = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{\"Tabs\":[null]}"));

            var layout = CustomLayout.Load(stored, false, false);

            Assert.AreEqual(4, layout.Tabs.Count);
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
        }

        [TestMethod]
        public void AddTabRefusesABlankNameAndTrimsOthers()
        {
            var layout = CustomLayout.Default();

            Assert.IsFalse(layout.AddTab(" "));
            Assert.AreEqual(4, layout.Tabs.Count);
            Assert.IsTrue(layout.AddTab(" Flight "));
            Assert.AreEqual("Flight", layout.Tabs[4].Name);
        }

        [TestMethod]
        public void NormaliseDropsACustomTabWithoutAName()
        {
            var layout = CustomLayout.Default();
            layout.Tabs.Add(new LayoutTab());

            layout.Normalise();

            Assert.AreEqual(4, layout.Tabs.Count);
        }

        [TestMethod]
        public void HeaderTranslatesOnlyUnrenamedBuiltInTabs()
        {
            var layout = CustomLayout.Default();
            layout.RenameTab(2, "General");
            layout.AddTab("Help");

            var headers = layout.Tabs.Select(t => CustomLayout.Header(t, s => "<" + s + ">")).ToArray();

            CollectionAssert.AreEqual(new[] { "<General>", "<Controls>", "General", "<Help>", "Help" }, headers);
        }

        [TestMethod]
        public void OnlyATabHoldingJustControlsOrHelpFillsTheTab()
        {
            CollectionAssert.AreEqual(new[] { false, true, false, true },
                CustomLayout.Default().Tabs.Select(CustomLayout.FillsTab).ToArray());

            var layout = CustomLayout.Default();
            layout.MoveRowToTab(3, 0, 1);
            layout.Pair(1, 0); // [controls,help]: still fills; Help's tab is now empty and does not
            layout.MoveRowToTab(0, 2, 1); // favourites joins: two rows, so the tab scrolls

            CollectionAssert.AreEqual(new[] { false, false, false, false }, layout.Tabs.Select(CustomLayout.FillsTab).ToArray());
            layout.HideRow(1, 1);
            Assert.IsTrue(CustomLayout.FillsTab(layout.Tabs[1]));
        }
    }
}
