# Panel Fit, Dividers and Three Panels per Row Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A row of the main window holds up to three panels; a panel can be fit to its content's width; neighbouring panels have a draggable divider whose split is saved.

**Architecture:** `CustomLayout` (pure, unit-tested) gains rows of three with Join/Split, a `Fit` set of panel keys and a `Splits` map of row key to shares. `MainWindow.BuildRow` turns those into `Auto`/star grid columns and `GridSplitter`s. `LayoutEditorWindow` gets Join and Split buttons and fit checkboxes. Height is untouched: every row stays as tall as its tallest panel.

**Tech Stack:** WPF on .NET Framework 4.8.1, C# 7.3, Newtonsoft.Json, MSTest (`IL2-SR-CommonTests`, sees internals of the client).

**Spec:** `docs/superpowers/specs/2026-11-19-panel-fit-and-divider-design.md`

## Global Constraints

- Work on branch `layout-add-from-other-tabs`. No merge to `master`, no push.
- At most **3** panels per row. Row key = the row's panel keys joined with `+` (`CustomLayout.RowKey`).
- `CustomLayout.MinShare = 0.15`: no panel's share of a row goes below it.
- A layout saved by 1.0.5.1 (no `Fit`, no `Splits`, rows of one or two) must load and look exactly as before.
- Classic theme: untouched (it never builds rows from the layout).
- Every new or renamed user-facing string goes in all six `IL2-SR-Client/Localization/*.resx`; `powershell -NoProfile -File scripts/Validate-ResxTranslations.ps1` must pass. The `.resx` files are UTF-8 with BOM and CRLF: keep both.
- Live checks run the Debug client with `-cfg="<a scratch copy of a settings folder>"`, never the player's real `%APPDATA%\IL2-SRS`. Close test clients by PID, never by image name: the player runs a real client.
- Build: `"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" IL2-SR-CommonTests/IL2-SR-CommonTests.csproj -p:Configuration=Debug -p:Platform=x64 -p:SolutionDir="$(pwd -W)/" -v:minimal -nologo`
- Tests: `"/c/Program Files/Microsoft Visual Studio/2022/Community/Common7/IDE/Extensions/TestPlatform/vstest.console.exe" IL2-SR-CommonTests/bin/x64/Debug/DCS-SR-CommonTests.dll /Platform:x64` (324 pass before this plan). One class: add `/TestCaseFilter:"FullyQualifiedName~CustomLayoutTests"`.

## Review Focus

- A layout saved by 1.0.5.1 loads with no fit panels and equal splits (test in Task 3: `ALayoutSavedWithoutFitOrSplitsLoadsAsBefore`).
- RCI Controller hidden by its own rule (no access keys) inside a row of three with a split: the other panels divide the row and no column is left empty (test in Task 2: `ColumnsLeaveOutPanelsThatAreNotShown`).
- A hand-edited `global.cfg` with shares such as `[0.95, 0.05]`, the wrong count, or `NaN`: the entry is dropped and the row is equal (test in Task 3: `NormaliseDropsSplitsThatAreStaleOrOutOfRange`).
- A divider dragged while the layout editor is open, then OK in the editor: the split is kept (live check in Task 3).
- Three panels, one of them fit, with the window at its 400 px minimum width: no exception, panels may clip (live check in Task 3).

---

### Task 1: Rows of three, Join and Split

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/CustomLayout.cs` (`Normalise`, the `CanPair`/`Pair`/`CanUnpair`/`Unpair` block)
- Modify: `IL2-SR-Client/UI/ClientWindow/LayoutEditorWindow.xaml` (the `PairButton` line), `LayoutEditorWindow.xaml.cs` (`UpdateButtons`, `Pair_Click`)
- Modify: `IL2-SR-Client/Localization/{en,de,es,fr,it,ru}.resx`
- Test: `IL2-SR-CommonTests/UI/CustomLayoutTests.cs`

**Interfaces:**
- Produces (replacing `CanPair`, `Pair`, `CanUnpair`, `Unpair`; no other callers exist):
  - `public const int MaxPanelsPerRow = 3;`
  - `public bool CanJoin(int tab, int row)` = `CanJoin(tab, row, row + 1)`
  - `public bool CanJoin(int tab, int row, int below)` — `below > row`, both rows valid, and their panel counts sum to `MaxPanelsPerRow` or fewer.
  - `public bool Join(int tab, int row, int below)` / `public bool Join(int tab, int row)` — the lower row's panels are appended on the right of `row`; rows between them stay below the joined row (as `Pair` does today via `MoveRow`). Removes both rows' keys from `Collapsed`.
  - `public bool CanSplit(int tab, int row)` — the row holds two or three panels.
  - `public void Split(int tab, int row)` — removes the row's key from `Collapsed`, moves its last panel to a new row at `row + 1`.

- [ ] **Step 1: Write the failing tests.** In `CustomLayoutTests.cs` rename the existing uses (`Pair`→`Join`, `CanPair`→`CanJoin`, `Unpair`→`Split`, `CanUnpair`→`CanSplit`) and change these tests:

```csharp
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
```

  In `EditorButtonRulesMatchWhatTheOperationsWouldDo` the first assertion becomes `Assert.IsFalse(layout.CanJoin(0, 0), "two pairs would make four");` and add `Assert.IsTrue(layout.CanJoin(0, 1), "a pair takes a third panel");`. In `PairFailsForAPairOrWhenThereIsNoRowBelow` (rename to `JoinFailsWhenThereIsNoRowBelowOrTheRowWouldPassThree`) `layout.Join(0, 1)` now succeeds: assert `Join(0, 0)` false, `Join(0, 2)` false, `Join(1, 0)` false.

- [ ] **Step 2: Build and run `CustomLayoutTests`.** Expected: the build fails with "does not contain a definition for 'CanJoin'".

- [ ] **Step 3: Implement the model** as the Interfaces block says; `Normalise` changes `.Take(2)` to `.Take(MaxPanelsPerRow)`.

- [ ] **Step 4: Editor.** In the XAML replace `PairButton` with two buttons in the same `WrapPanel`: `JoinButton` (`Content="Join with row below"`, `Click="Join_Click"`) and `SplitButton` (`Content="Split row"`, `Click="Split_Click"`), both `Style="{StaticResource EditorButton}"`. In `UpdateButtons`: `JoinButton.IsEnabled = rowBelow && _layout.CanJoin(tab, row, _shownRows[shown + 1]); SplitButton.IsEnabled = _layout.CanSplit(tab, row);`. `Join_Click` and `Split_Click` each do their one operation, then `ShowRows(shown); KeepFocus(sender, RowList);` as `Pair_Click` does. Delete `Pair_Click` and the `PairButton.Content` line.

- [ ] **Step 5: Text.** In all six `.resx` files rename the key `Pair with row below` to `Join with row below` and `Unpair` to `Split row`, with these values:

| | Join with row below | Split row |
| --- | --- | --- |
| en | Join with row below | Split row |
| de | Mit Zeile darunter verbinden | Zeile teilen |
| es | Unir con la fila de abajo | Dividir fila |
| fr | Joindre à la ligne du dessous | Scinder la ligne |
| it | Unisci alla riga sotto | Dividi riga |
| ru | Объединить со строкой ниже | Разделить строку |

- [ ] **Step 6: Build, run all tests, run the resx validation.** Expected: 0 errors, all tests pass (324 + 2 new), "RESX validation passed for 6 file(s)."

- [ ] **Step 7: Live check.** In the Debug client with a scratch settings folder: open ⚙, on General select "Server + Overlays", press **Join with row below**, press OK. Expected: Server, Overlays and Favourites side by side in three equal columns, one collapse arrow over Favourites. Press **Split row** twice and OK: three rows again.

- [ ] **Step 8: Commit** — `Layout editor: up to three panels per row, with Join and Split`.

---

### Task 2: Fit to content

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/CustomLayout.cs`, `MainWindow.Layout.cs` (`BuildRow`), `LayoutEditorWindow.xaml` and `.xaml.cs`
- Modify: the six `.resx` files
- Test: `IL2-SR-CommonTests/UI/CustomLayoutTests.cs`

**Interfaces:**
- Produces in `CustomLayout`:
  - `public HashSet<string> Fit { get; set; } = new HashSet<string>();` — panel keys; `Normalise` keeps only keys in `PanelKeys.All` (null becomes empty).
  - `public List<double?> Columns(IList<string> row, Func<string, bool> shown)` — one entry per panel of `row` for which `shown` is true, in order: `null` for a fit panel (an `Auto` column), otherwise its star weight. In this task every weight is `1`; Task 3 replaces it with the panel's share.

- [ ] **Step 1: Write the failing tests.**

```csharp
[TestMethod]
public void ColumnsAreAutoForFitPanelsAndStarForTheRest()
{
    var layout = CustomLayout.Default();
    layout.Fit.Add("server");

    var columns = layout.Columns(layout.Tabs[0].Rows[1], k => true); // [server,overlays]

    Assert.AreEqual(2, columns.Count);
    Assert.IsNull(columns[0]);
    Assert.AreEqual(1d, columns[1]);
}

[TestMethod]
public void ColumnsLeaveOutPanelsThatAreNotShown()
{
    var layout = CustomLayout.Default();
    layout.Join(2, 0);             // settings: [globalSettings,rciController] ...
    layout.Join(2, 0);             // [globalSettings,rciController,audioOptions]
    layout.Fit.Add("rciController");

    var columns = layout.Columns(layout.Tabs[2].Rows[0], k => k != PanelKeys.RciController);

    CollectionAssert.AreEqual(new double?[] { 1d, 1d }, columns);
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
```

- [ ] **Step 2: Build and run `CustomLayoutTests`.** Expected: build fails, "does not contain a definition for 'Fit'".

- [ ] **Step 3: Implement `Fit`, its `Normalise` line and `Columns`.**

- [ ] **Step 4: `BuildRow`.** Replace the loop that adds one default `ColumnDefinition` per shown panel: take `_layout.Columns(row, key => PanelFor(key).Visibility == Visibility.Visible)` and add a column per entry, `GridLength.Auto` for `null`, else `new GridLength(weight, GridUnitType.Star)`; when the list is empty keep the single default column that exists today. Nothing else in `BuildRow` changes: panels keep their column by shown index and the collapse arrow stays in the last column. A row of only `Auto` columns packs to the left by itself.

- [ ] **Step 5: Editor.** Under the `WrapPanel` of row buttons add `<WrapPanel x:Name="FitBoxes" />`. A new `FillFitBoxes()` clears it and, when a row is selected, adds a `TextBlock` "Fit to content:" (via `LocalizationManager.Get`) and one `CheckBox` per available panel of the selected row: `Content` = `PanelName(key)`, `IsChecked` = `_layout.Fit.Contains(key)`, `AutomationProperties.Name` = the label followed by the panel name, and a `Click` handler that adds or removes the key in `_layout.Fit` and then sets only the selected `RowList` item's `Content` to the row's new label (rebuilding the list would swallow the click, as the rename code notes). Call `FillFitBoxes()` at the end of `UpdateButtons` (it runs on every row selection). Row labels: a fit panel's name is `LocalizationManager.Format("{0} (fit)", PanelName(key))`; put the label logic in one `RowName(int row)` method used by `ShowRows` and the checkbox handler.

- [ ] **Step 6: Text**, in all six `.resx` files:

| | Fit to content: | {0} (fit) |
| --- | --- | --- |
| en | Fit to content: | {0} (fit) |
| de | An Inhalt anpassen: | {0} (angepasst) |
| es | Ajustar al contenido: | {0} (ajustado) |
| fr | Ajuster au contenu : | {0} (ajusté) |
| it | Adatta al contenuto: | {0} (adattato) |
| ru | По размеру содержимого: | {0} (по содержимому) |

- [ ] **Step 7: Build, run all tests, run the resx validation.** Expected: all pass (3 new).

- [ ] **Step 8: Live check.** Scratch settings, Debug client. Make a tab with the row "Server + Favourites", tick **Server** under Fit to content, OK. Expected: Server about 260 px wide on the left, Favourites filling the rest; the row label reads "Server (fit) + Favourites"; after a restart the same. Then fit each of the twelve panels once (alone in a row is enough) and note any whose content becomes unusable or wider than the window; report those, do not fix them here.

- [ ] **Step 9: Commit** — `Layout editor: Fit to content per panel`.

---

### Task 3: Dividers

**Files:**
- Modify: `IL2-SR-Client/UI/ClientWindow/CustomLayout.cs`, `MainWindow.Layout.cs`, `LayoutEditorWindow.xaml.cs`
- Modify: the six `.resx` files, `CHANGELOG.md`
- Test: `IL2-SR-CommonTests/UI/CustomLayoutTests.cs`

**Interfaces:**
- Consumes: `Columns` and `Fit` from Task 2; `Join`/`Split` from Task 1; `LayoutEditorWindow.SetRowCollapsed(string rowKey, bool collapsed)` as the pattern for editor sync.
- Produces in `CustomLayout`:
  - `public const double MinShare = 0.15;`
  - `public Dictionary<string, double[]> Splits { get; set; } = new Dictionary<string, double[]>();` — row key to one share per panel of the row, summing to 1.
  - `public double[] Shares(IList<string> row)` — a copy of the stored shares, else equal shares.
  - `public void SetSplit(IList<string> row, int left, int right, double leftWidth, double rightWidth)` — keeps the two panels' combined share, divides it in proportion to the two widths, clamps so both stay at `MinShare` or more, stores the row's shares. Does nothing when the widths do not sum to more than 0.
  - `public void ResetSplit(IList<string> row)` — removes the row's entry.
  - `Normalise` drops a `Splits` entry when no row has that key, the count differs from the row's, any share is not a number or is below `MinShare` (allow 1e-6), or the sum is not 1 within 1e-6. Null becomes empty.
  - `Columns` now uses `Shares(row)[i]` as the star weight of panel `i`.
- Produces in `LayoutEditorWindow`: `internal void SetSplit(string rowKey, double[] shares)` — stores a copy in the edited layout's `Splits`, or removes the entry when `shares` is null.

- [ ] **Step 1: Write the failing tests.** Shares are compared with a tolerance (`0.85` and `1 - 0.85` are not exact in binary):

```csharp
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
```

  Update Task 2's two `Columns` assertions: weights are now shares, so `1d` becomes `0.5` in `ColumnsAreAutoForFitPanelsAndStarForTheRest`, and `new double?[] { 1d, 1d }` becomes `new double?[] { 1d / 3, 1d / 3 }` in `ColumnsLeaveOutPanelsThatAreNotShown`.

- [ ] **Step 2: Build and run `CustomLayoutTests`.** Expected: build fails, "does not contain a definition for 'Shares'".

- [ ] **Step 3: Implement the model** as the Interfaces block says. Run the tests: all pass.

- [ ] **Step 4: Dividers in `BuildRow`.** After the panels are added, for each pair of neighbouring shown panels where neither key is in `_layout.Fit`, add a `GridSplitter` to the grid in the left panel's column: `HorizontalAlignment = Right`, `VerticalAlignment = Stretch`, `Width = 6`, `Background = Brushes.Transparent`, `ResizeDirection = Columns`, `ResizeBehavior = CurrentAndNext`, `Cursor = Cursors.SizeWE`, `ToolTip` and `AutomationProperties.Name` = the translated text of Step 6. Add it after the panels so it is on top, and before the collapse arrow so the arrow stays clickable. Three handlers share one local `Store()`:
  - `DragCompleted`, and `KeyUp` for Left/Right (the splitter moves by keyboard too, and raises no drag event): `_layout.SetSplit(row, leftIndexInRow, rightIndexInRow, grid.ColumnDefinitions[i].ActualWidth, grid.ColumnDefinitions[i + 1].ActualWidth)`, then `Store()`.
  - `MouseDoubleClick`: `_layout.ResetSplit(row)`, then `Store()`.
  - `Store()`: set every star column's `Width` again from `_layout.Columns(...)` (so a clamped drag snaps back to the limit), save with `_globalSettings.SetClientSetting(GlobalSettingsKeys.CustomLayout, _layout.Save())`, call `_layoutEditor?.SetSplit(CustomLayout.RowKey(row), _layout.Splits.TryGetValue(...) ? shares : null)`, and queue `ApplyPanelCollapse` at `DispatcherPriority.Loaded` because a new width can change the row's height.

- [ ] **Step 5: `LayoutEditorWindow.SetSplit`**, next to `SetRowCollapsed`.

- [ ] **Step 6: Text**, one string in all six `.resx` files, key `Drag to resize the panels. Double-click to make them equal.`:

| | value |
| --- | --- |
| en | Drag to resize the panels. Double-click to make them equal. |
| de | Ziehen, um die Bereiche zu skalieren. Doppelklick macht sie gleich breit. |
| es | Arrastra para cambiar el tamaño de los paneles. Doble clic para igualarlos. |
| fr | Faites glisser pour redimensionner les panneaux. Double-cliquez pour les égaliser. |
| it | Trascina per ridimensionare i pannelli. Doppio clic per renderli uguali. |
| ru | Перетащите, чтобы изменить размер панелей. Двойной щелчок делает их равными. |

  `CHANGELOG.md`, under Unreleased › Changed, after the two layout-editor lines already there: one line covering this whole plan — up to three panels per row with **Join with row below** and **Split row**, **Fit to content** per panel, and dragging the gap between two panels to resize them (double-click to make them equal).

- [ ] **Step 7: Build, run all tests, run the resx validation.** Expected: all pass (5 new).

- [ ] **Step 8: Live checks**, scratch settings, Debug client:
  1. Drag the gap between Microphone and Speakers to about one third. Restart: the split is the same.
  2. Drag it as far left as it goes: it snaps back so Microphone keeps 15% of the row.
  3. Double-click the gap: equal halves.
  4. Open ⚙, drag a divider in the main window, press OK in the editor: the split is kept.
  5. A row of three with the middle panel fit: no divider on either side of it; the outer two are equal.
  6. A row of three with one fit, window dragged to its 400 px minimum: no exception in the log; clipping is accepted.
  7. Narrow a panel until its text wraps: the window's height follows the taller row.

- [ ] **Step 9: Commit** — `Main window: draggable dividers between panels in a row`.
