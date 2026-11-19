using System;
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
        public void NormaliseKeepsOnlyTheFirstThreeOfARowAndTheRestGoBackToTheFirstTab()
        {
            var layout = CustomLayout.Default();
            layout.Tabs[1].Rows[0] = new System.Collections.Generic.List<string> { "controls", "help", "favourites", "server" };
            layout.Tabs[0].Rows.RemoveAt(2);
            layout.Tabs[0].Rows[1].Remove("server");
            layout.Tabs.RemoveAt(3);

            layout.Normalise();

            Assert.AreEqual("[controls,help,favourites]", Rows(layout, 1));
            Assert.AreEqual("[microphone,speakers] [overlays] [server]", Rows(layout, 0));
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
        public void JoinJoinsTwoSingleRowsAndSplitSplitsThemBack()
        {
            var layout = CustomLayout.Default();

            Assert.IsTrue(layout.Join(2, 0));
            Assert.AreEqual("[globalSettings,rciController] [audioOptions] [profile] [profileSettings]", Rows(layout, 2));

            layout.Split(2, 0);
            Assert.AreEqual("[globalSettings] [rciController] [audioOptions] [profile] [profileSettings]", Rows(layout, 2));
        }

        [TestMethod]
        public void JoinFailsWhenThereIsNoRowBelowOrTheRowWouldPassThree()
        {
            var layout = CustomLayout.Default();

            Assert.IsFalse(layout.Join(0, 0));
            Assert.IsFalse(layout.Join(0, 2));
            Assert.IsFalse(layout.Join(1, 0));
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
        }

        [TestMethod]
        public void JoinAddsTheLowerRowsPanelsOnTheRightUpToThree()
        {
            var layout = CustomLayout.Default(); // general: [microphone,speakers] [server,overlays] [favourites]

            Assert.IsFalse(layout.CanJoin(0, 0), "two pairs would make four");
            Assert.IsFalse(layout.Join(0, 0));
            Assert.IsTrue(layout.Join(0, 1));
            Assert.AreEqual("[microphone,speakers] [server,overlays,favourites]", Rows(layout, 0));
            Assert.IsFalse(layout.CanJoin(0, 0), "five panels");
            Assert.IsFalse(layout.CanJoin(0, 1), "nothing below the last row");
        }

        [TestMethod]
        public void SplitMovesTheRightHandPanelToARowBelow()
        {
            var layout = CustomLayout.Default();
            layout.Join(0, 1);

            layout.Split(0, 1);
            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
            layout.Split(0, 1);
            Assert.AreEqual("[microphone,speakers] [server] [overlays] [favourites]", Rows(layout, 0));
            Assert.IsFalse(layout.CanSplit(0, 1));
        }

        [TestMethod]
        public void JoiningMovingAndSplittingClearTheCollapsedKey()
        {
            var layout = CustomLayout.Default();
            layout.Collapsed.Add("server+overlays");
            layout.Collapsed.Add("favourites");
            layout.Collapsed.Add("globalSettings");

            layout.Split(0, 1);
            layout.MoveRow(0, 3, -1);
            layout.Join(2, 0);

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

            layout.AddPanel("favourites", 1);
            Assert.AreEqual(0, layout.Hidden.Count);
            Assert.AreEqual("[controls] [favourites]", Rows(layout, 1));
        }

        [TestMethod]
        public void AddingAPanelFromAnotherTabTakesItOutOfItsPair()
        {
            var layout = CustomLayout.Default();
            layout.Collapsed.Add("server+overlays");

            layout.AddPanel("overlays", 1);

            Assert.AreEqual("[microphone,speakers] [server] [favourites]", Rows(layout, 0));
            Assert.AreEqual("[controls] [overlays]", Rows(layout, 1));
            Assert.AreEqual(0, layout.Collapsed.Count);
        }

        [TestMethod]
        public void AddingAPanelFromAnotherTabRemovesItsEmptyRow()
        {
            var layout = CustomLayout.Default();

            layout.AddPanel("favourites", 3);
            layout.AddPanel("controls", 3);

            Assert.AreEqual("[microphone,speakers] [server,overlays]", Rows(layout, 0));
            Assert.AreEqual("", Rows(layout, 1));
            Assert.AreEqual("[help] [favourites] [controls]", Rows(layout, 3));
        }

        [TestMethod]
        public void AddingAPanelAlreadyOnTheTabChangesNothing()
        {
            var layout = CustomLayout.Default();

            layout.AddPanel("server", 0);

            Assert.AreEqual("[microphone,speakers] [server,overlays] [favourites]", Rows(layout, 0));
        }

        [TestMethod]
        public void PanelsToAddListsHiddenPanelsThenOtherTabsPanelsInOrder()
        {
            var layout = CustomLayout.Default();
            layout.HideRow(1, 0);

            var others = layout.PanelsToAdd(2).Select(p => p.Key + "@" + p.Value).ToArray();

            CollectionAssert.AreEqual(new[]
            {
                "controls@-1", "microphone@0", "speakers@0", "server@0", "overlays@0", "favourites@0", "help@3"
            }, others);
        }

        [TestMethod]
        public void ColumnsAreAutoForFitPanelsAndStarForTheRest()
        {
            var layout = CustomLayout.Default();
            layout.Fit.Add("server");

            var columns = layout.Columns(layout.Tabs[0].Rows[1], k => true); // [server,overlays]

            Assert.AreEqual(2, columns.Count);
            Assert.IsNull(columns[0]);
            Assert.AreEqual(0.5, columns[1]);
        }

        [TestMethod]
        public void ColumnsLeaveOutPanelsThatAreNotShown()
        {
            var layout = CustomLayout.Default();
            layout.Join(2, 0); // settings: [globalSettings,rciController] ...
            layout.Join(2, 0); // [globalSettings,rciController,audioOptions]
            layout.Fit.Add("rciController");

            var columns = layout.Columns(layout.Tabs[2].Rows[0], k => k != PanelKeys.RciController);

            CollectionAssert.AreEqual(new double?[] { 1d / 3, 1d / 3 }, columns);
        }

        [TestMethod]
        public void FitSurvivesSaveAndMovesButNormaliseDropsUnknownKeys()
        {
            var layout = CustomLayout.Default();
            layout.Fit.Add("server");
            layout.Fit.Add("nonsense");
            layout.Split(0, 1);
            layout.MoveRowToTab(0, 1, 1);

            var loaded = CustomLayout.Load(layout.Save(), false, false);

            CollectionAssert.AreEqual(new[] { "server" }, loaded.Fit.ToArray());
        }

        private static void AssertShares(double[] expected, double[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++) Assert.AreEqual(expected[i], actual[i], 1e-9, "share " + i);
        }

        [TestMethod]
        public void SharesAreEqualUntilADividerIsDragged()
        {
            var layout = CustomLayout.Default();
            layout.Join(0, 1); // [server,overlays,favourites]
            var row = layout.Tabs[0].Rows[1];

            AssertShares(new[] { 1d / 3, 1d / 3, 1d / 3 }, layout.Shares(row));

            layout.SetSplit(row, 0, 1, 300, 100);
            AssertShares(new[] { 0.5, 1d / 6, 1d / 3 }, layout.Shares(row));
            AssertShares(new[] { 0.5, 1d / 6, 1d / 3 }, layout.Columns(row, k => true).Select(c => c.Value).ToArray());
        }

        [TestMethod]
        public void ADividerCannotTakeAPanelBelowTheMinimumShare()
        {
            var layout = CustomLayout.Default();
            var row = layout.Tabs[0].Rows[1]; // [server,overlays]

            layout.SetSplit(row, 0, 1, 990, 10);
            AssertShares(new[] { 0.85, 0.15 }, layout.Shares(row));

            layout.SetSplit(row, 0, 1, 0, 0);
            AssertShares(new[] { 0.85, 0.15 }, layout.Shares(row));

            layout.ResetSplit(row);
            AssertShares(new[] { 0.5, 0.5 }, layout.Shares(row));
        }

        [TestMethod]
        public void NormaliseDropsSplitsThatAreStaleOrOutOfRange()
        {
            var layout = CustomLayout.Default();
            layout.Splits["microphone+speakers"] = new[] { 0.95, 0.05 };
            layout.Splits["server+overlays"] = new[] { 0.4, 0.3, 0.3 };
            layout.Splits["favourites"] = new[] { double.NaN };
            layout.Splits["help+controls"] = new[] { 0.5, 0.5 };

            layout.Normalise();

            Assert.AreEqual(0, layout.Splits.Count);
        }

        [TestMethod]
        public void ASplitSurvivesSaveAndIsDroppedWhenItsRowChanges()
        {
            var layout = CustomLayout.Default();
            layout.SetSplit(layout.Tabs[0].Rows[1], 0, 1, 100, 300);

            var loaded = CustomLayout.Load(layout.Save(), false, false);
            AssertShares(new[] { 0.25, 0.75 }, loaded.Shares(loaded.Tabs[0].Rows[1]));

            loaded.Split(0, 1);
            loaded.Normalise();
            Assert.AreEqual(0, loaded.Splits.Count);
        }

        [TestMethod]
        public void JoinSplitAndAddPanelDropTheSplitOfARowTheyChange()
        {
            // Without waiting for Normalise: the editor works on one copy across several Applies.
            var layout = CustomLayout.Default(); // general: [microphone,speakers] [server,overlays] [favourites]
            layout.SetSplit(layout.Tabs[0].Rows[1], 0, 1, 100, 300);

            layout.Split(0, 1);
            Assert.AreEqual(0, layout.Splits.Count, "split");
            layout.Join(0, 1);
            AssertShares(new[] { 0.5, 0.5 }, layout.Shares(layout.Tabs[0].Rows[1]));

            layout.SetSplit(layout.Tabs[0].Rows[1], 0, 1, 100, 300);
            layout.Join(0, 1); // [server,overlays,favourites]
            Assert.AreEqual(0, layout.Splits.Count, "join");

            layout.SetSplit(layout.Tabs[0].Rows[1], 0, 1, 100, 300);
            layout.AddPanel("overlays", 1);
            Assert.AreEqual(0, layout.Splits.Count, "add to another tab");
        }

        [TestMethod]
        public void ALayoutSavedWithoutFitOrSplitsLoadsAsBefore()
        {
            // What 1.0.5.1 writes: no Fit, no Splits.
            const string json = "{\"Tabs\":[{\"BuiltIn\":\"general\",\"Name\":null,\"Rows\":[[\"microphone\",\"speakers\"],[\"server\",\"overlays\"]]}],\"Hidden\":[],\"Collapsed\":[]}";
            var stored = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));

            var layout = CustomLayout.Load(stored, false, false);

            Assert.AreEqual(0, layout.Fit.Count);
            Assert.AreEqual(0, layout.Splits.Count);
            CollectionAssert.AreEqual(new double?[] { 0.5, 0.5 }, layout.Columns(layout.Tabs[0].Rows[0], k => true));
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
            layout.Join(1, 0); // [controls,help]: still fills; Help's tab is now empty and does not
            layout.MoveRowToTab(0, 2, 1); // favourites joins: two rows, so the tab scrolls

            CollectionAssert.AreEqual(new[] { false, false, false, false }, layout.Tabs.Select(CustomLayout.FillsTab).ToArray());
            layout.HideRow(1, 1);
            Assert.IsTrue(CustomLayout.FillsTab(layout.Tabs[1]));
        }

        [TestMethod]
        public void EditorButtonRulesMatchWhatTheOperationsWouldDo()
        {
            var layout = CustomLayout.Default(); // general: [microphone,speakers] [server,overlays] [favourites]

            Assert.IsFalse(layout.CanJoin(0, 0), "two pairs would make four");
            Assert.IsTrue(layout.CanJoin(0, 1), "a pair takes a third panel");
            Assert.IsFalse(layout.CanJoin(0, 2), "nothing below the last row");
            Assert.IsTrue(layout.CanSplit(0, 1));
            Assert.IsFalse(layout.CanSplit(0, 2));
            Assert.IsTrue(layout.CanJoin(2, 0));
            Assert.IsFalse(layout.CanJoin(-1, 0));

            Assert.IsFalse(layout.CanMoveRow(0, 0, -1));
            Assert.IsTrue(layout.CanMoveRow(0, 0, 1));
            Assert.IsFalse(layout.CanMoveRow(0, 2, 1));
            Assert.IsFalse(layout.CanMoveRow(0, -1, 1), "no row selected");
            Assert.IsFalse(layout.CanMoveTab(0, -1));
            Assert.IsTrue(layout.CanMoveTab(3, -1));
            Assert.IsFalse(layout.CanMoveTab(3, 1));

            Assert.IsTrue(layout.CanDeleteTab(3));
            Assert.IsFalse(layout.CanDeleteTab(4));
            while (layout.Tabs.Count > 1) layout.DeleteTab(0);
            Assert.IsFalse(layout.CanDeleteTab(0), "the last tab stays");
            Assert.IsFalse(layout.DeleteTab(0));
        }

        [TestMethod]
        public void ShownRowsLeaveOutUnavailablePanelsAndJoinActsOnTheNextShownRow()
        {
            var layout = CustomLayout.Default(); // settings: [globalSettings] [rciController] [audioOptions] [profile] [profileSettings]
            Func<string, bool> available = k => k != PanelKeys.RciController;
            var shown = CustomLayout.ShownRows(layout.Tabs[2], available);

            CollectionAssert.AreEqual(new[] { 0, 2, 3, 4 }, shown);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, CustomLayout.ShownRows(layout.Tabs[0], available));

            Assert.IsFalse(layout.CanJoin(2, 0, 0), "a row can't join itself");
            Assert.IsFalse(layout.CanJoin(2, 2, 0), "only with a later row");
            Assert.IsTrue(layout.Join(2, 0, shown[1]));
            Assert.AreEqual("[globalSettings,audioOptions] [rciController] [profile] [profileSettings]", Rows(layout, 2));

            // Moving a shown row by the gap to the next shown one steps over the unavailable row.
            layout.MoveRow(2, 2, 0 - 2);
            Assert.AreEqual("[profile] [globalSettings,audioOptions] [rciController] [profileSettings]", Rows(layout, 2));
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, CustomLayout.ShownRows(layout.Tabs[2], available));

            layout.MoveRowToTab(2, 2, 1); // a row of only unavailable panels is not shown on its new tab either
            CollectionAssert.AreEqual(new[] { 0 }, CustomLayout.ShownRows(layout.Tabs[1], available));
        }

        [TestMethod]
        public void TabToKeepFollowsABuiltInTabElseThePositionElseTheFirst()
        {
            var old = CustomLayout.Default();
            old.AddTab("Mine");
            var updated = old.Clone();
            updated.MoveTab(2, -2); // settings first
            updated.DeleteTab(1);   // general gone

            Assert.AreEqual(0, CustomLayout.TabToKeep(old, 2, updated), "settings, wherever it went");
            Assert.AreEqual(1, CustomLayout.TabToKeep(old, 1, updated), "controls");
            Assert.AreEqual(0, CustomLayout.TabToKeep(old, 0, updated), "general was deleted");
            Assert.AreEqual(2, CustomLayout.TabToKeep(old, 3, updated), "help");
            Assert.AreEqual(0, CustomLayout.TabToKeep(old, 4, updated), "a custom tab: no index 4 any more");
            Assert.AreEqual(4, CustomLayout.TabToKeep(old, 4, old.Clone()), "a custom tab keeps its position");
            Assert.AreEqual(0, CustomLayout.TabToKeep(null, 2, updated), "the first layout");
            Assert.AreEqual(0, CustomLayout.TabToKeep(old, -1, updated));
        }
    }
}
