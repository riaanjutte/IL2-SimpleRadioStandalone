using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using MahApps.Metro.Controls;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    /// <summary>Edits a copy of the main window's layout; OK and Apply hand a copy of the edit to the main window.</summary>
    public partial class LayoutEditorWindow : MetroWindow
    {
        private readonly Action<CustomLayout> _apply;
        private CustomLayout _layout;
        private bool _filling;
        private TextBox _renameBox;

        // Model indices of what the lists show: panels unavailable in this build are left out (see Available).
        private List<int> _shownRows = new List<int>();
        // Panel key and the tab it is on now, -1 when hidden.
        private List<KeyValuePair<string, int>> _panelsToAdd = new List<KeyValuePair<string, int>>();

        internal LayoutEditorWindow(CustomLayout layout, Action<CustomLayout> apply)
        {
            InitializeComponent();
            // Before the lists are filled: tab names are the player's own and must not be looked up as translations.
            LocalizationManager.LocalizeElement(this);
            AutomationProperties.SetName(TabList, LocalizationManager.Get("Tabs"));
            AutomationProperties.SetName(RowList, LocalizationManager.Get("Panels on this tab"));
            AutomationProperties.SetName(HiddenList, LocalizationManager.Get("Other panels"));
            AutomationProperties.SetName(TargetTab, LocalizationManager.Get("Target tab"));

            _layout = layout.Clone();
            _apply = apply;
            Loaded += (sender, e) =>
            {
                Refresh(0, 0);
                FocusSelected(TabList);
            };
        }

        /// <summary>A row collapsed or expanded in the main window while the editor is open; a row the edit removed is dropped on apply.</summary>
        internal void SetRowCollapsed(string rowKey, bool collapsed)
        {
            if (collapsed)
            {
                _layout.Collapsed.Add(rowKey);
            }
            else
            {
                _layout.Collapsed.Remove(rowKey);
            }
        }

        /// <summary>A panel's translated header, from the main window; set before the window is shown.</summary>
        internal Func<string, string> PanelName { get; set; } = key => key;

        /// <summary>
        /// Whether a panel can appear at all in this build (RCI Controller needs access keys), from the main window;
        /// set before the window is shown. Unavailable panels stay in the layout but are left out of the lists.
        /// </summary>
        internal Func<string, bool> Available { get; set; } = key => true;

        private int Tab => TabList.SelectedIndex;

        // The selected row as the list shows it, and as the layout holds it.
        private int Shown => RowList.SelectedIndex;
        private int Row => Shown < 0 ? -1 : _shownRows[Shown];

        private string TabHeader(int tab)
        {
            return CustomLayout.Header(_layout.Tabs[tab], LocalizationManager.Get);
        }

        private void Refresh(int tab, int row)
        {
            _filling = true;
            Fill(TabList, Enumerable.Range(0, _layout.Tabs.Count).Select(TabHeader), tab);
            _filling = false;
            ShowRows(row);
        }

        /// <summary>Fills the selected tab's rows, the tabs its rows can move to, and the panels that can be added to it.</summary>
        private void ShowRows(int shown)
        {
            _filling = true;
            _shownRows = Tab < 0 ? new List<int>() : CustomLayout.ShownRows(_layout.Tabs[Tab], Available);
            Fill(RowList, _shownRows.Select(r => string.Join(" + ", _layout.Tabs[Tab].Rows[r].Where(Available).Select(PanelName))), shown);
            FillTargetTabs();
            _panelsToAdd = _layout.PanelsToAdd(Tab).Where(p => Available(p.Key)).ToList();
            Fill(HiddenList, _panelsToAdd.Select(PanelToAddName), HiddenList.SelectedIndex);
            _filling = false;
            UpdateButtons();
        }

        private string PanelToAddName(KeyValuePair<string, int> panel)
        {
            return panel.Value < 0
                ? LocalizationManager.Format("{0} (hidden)", PanelName(panel.Key))
                : LocalizationManager.Format("{0} (on {1})", PanelName(panel.Key), TabHeader(panel.Value));
        }

        private void FillTargetTabs()
        {
            // Keeps the chosen tab, so several panels can be moved to it one after another.
            var previous = (TargetTab.SelectedItem as ComboBoxItem)?.Tag;
            TargetTab.Items.Clear();
            for (int i = 0; i < _layout.Tabs.Count; i++)
            {
                if (i != Tab)
                {
                    TargetTab.Items.Add(new ComboBoxItem { Content = TabHeader(i), Tag = i });
                }
            }

            var items = TargetTab.Items.Cast<ComboBoxItem>().ToList();
            TargetTab.SelectedItem = items.FirstOrDefault(item => Equals(item.Tag, previous)) ?? items.FirstOrDefault();
        }

        // ListBoxItems rather than strings: two equal names would otherwise select as one.
        private static void Fill(ListBox list, IEnumerable<string> names, int selected)
        {
            list.Items.Clear();
            foreach (var name in names)
            {
                list.Items.Add(new ListBoxItem { Content = name });
            }

            list.SelectedIndex = Math.Min(selected, list.Items.Count - 1);
        }

        private void UpdateButtons(object sender = null, SelectionChangedEventArgs e = null)
        {
            if (_filling)
            {
                return;
            }

            int tab = Tab, shown = Shown, row = Row;
            bool rowBelow = shown >= 0 && shown + 1 < _shownRows.Count;
            RenameTabButton.IsEnabled = tab >= 0;
            DeleteTabButton.IsEnabled = _layout.CanDeleteTab(tab);
            TabUpButton.IsEnabled = _layout.CanMoveTab(tab, -1);
            TabDownButton.IsEnabled = _layout.CanMoveTab(tab, 1);
            RowUpButton.IsEnabled = shown > 0;
            RowDownButton.IsEnabled = rowBelow;
            JoinButton.IsEnabled = rowBelow && _layout.CanJoin(tab, row, _shownRows[shown + 1]);
            SplitButton.IsEnabled = _layout.CanSplit(tab, row);
            HideButton.IsEnabled = row >= 0;
            MoveToTabButton.IsEnabled = row >= 0 && TargetTab.SelectedItem != null;
            TargetTab.IsEnabled = TargetTab.Items.Count > 0;
            ShowPanelButton.IsEnabled = tab >= 0 && HiddenList.SelectedIndex >= 0;
        }

        /// <summary>A keyboard user whose button just greyed out carries on from the list it acted on.</summary>
        private void KeepFocus(object button, ListBox list)
        {
            if (!((UIElement) button).IsEnabled)
            {
                FocusSelected(list);
            }
        }

        /// <summary>Focuses the list's selected item once it is laid out, else the list.</summary>
        private void FocusSelected(ListBox list)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!((list.SelectedItem as UIElement)?.Focus() ?? false))
                {
                    list.Focus();
                }
            }), DispatcherPriority.Loaded);
        }

        private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_filling)
            {
                ShowRows(0);
            }
        }

        private void AddTab_Click(object sender, RoutedEventArgs e)
        {
            _layout.AddTab(LocalizationManager.Get("New tab"));
            Refresh(_layout.Tabs.Count - 1, 0);
            BeginRename();
        }

        private void RenameTab_Click(object sender, RoutedEventArgs e)
        {
            BeginRename();
        }

        /// <summary>Swaps the selected tab's name for a text box: Enter or leaving it renames, Escape keeps the old name.</summary>
        private void BeginRename()
        {
            int tab = Tab;
            if (tab < 0)
            {
                return;
            }

            var item = (ListBoxItem) TabList.Items[tab];
            if (!(item.Content is string oldName))
            {
                return; // already being renamed
            }

            var box = new TextBox { Text = oldName };
            AutomationProperties.SetName(box, LocalizationManager.Get("Tab name"));
            // Preview, and handled: otherwise Escape also reaches the Cancel button and closes the window.
            box.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Enter || e.Key == Key.Escape)
                {
                    EndRename(box, tab, oldName, e.Key == Key.Enter);
                    item.Focus();
                    e.Handled = true;
                }
            };
            box.LostKeyboardFocus += (sender, e) => EndRename(box, tab, oldName, true);
            item.Content = box;
            _renameBox = box;
            TabList.ScrollIntoView(item);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                box.Focus();
                box.SelectAll();
            }), DispatcherPriority.Loaded);
        }

        private void EndRename(TextBox box, int tab, string oldName, bool commit)
        {
            if (_renameBox != box)
            {
                return; // already ended: removing the box takes its focus away too
            }

            _renameBox = null;
            // Unchanged text leaves a built-in tab's name translated; empty text is refused and keeps the old name.
            if (commit && box.Text != oldName)
            {
                _layout.RenameTab(tab, box.Text);
            }

            // Only this item and the combo change: rebuilding the lists here would swallow the click that took the focus.
            ((ListBoxItem) TabList.Items[tab]).Content = TabHeader(tab);
            for (int i = 0; i < _panelsToAdd.Count; i++)
            {
                ((ListBoxItem) HiddenList.Items[i]).Content = PanelToAddName(_panelsToAdd[i]);
            }

            _filling = true;
            FillTargetTabs();
            _filling = false;
            UpdateButtons();
        }

        private void DeleteTab_Click(object sender, RoutedEventArgs e)
        {
            int tab = Tab;
            var answer = MessageBox.Show(this,
                LocalizationManager.Format("Delete the tab \"{0}\"? Its panels will be hidden. You can add them back from Other panels.", TabHeader(tab)),
                LocalizationManager.Get("Delete tab"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes && _layout.DeleteTab(tab))
            {
                Refresh(Math.Max(0, tab - 1), 0);
                KeepFocus(sender, TabList);
            }
        }

        private void TabUp_Click(object sender, RoutedEventArgs e)
        {
            MoveTab(sender, -1);
        }

        private void TabDown_Click(object sender, RoutedEventArgs e)
        {
            MoveTab(sender, 1);
        }

        private void MoveTab(object sender, int delta)
        {
            int tab = Tab;
            _layout.MoveTab(tab, delta);
            Refresh(tab + delta, Row);
            KeepFocus(sender, TabList);
        }

        private void RowUp_Click(object sender, RoutedEventArgs e)
        {
            MoveRow(sender, -1);
        }

        private void RowDown_Click(object sender, RoutedEventArgs e)
        {
            MoveRow(sender, 1);
        }

        private void MoveRow(object sender, int delta)
        {
            // Past any rows the player can't see, to where the neighbouring shown row is.
            int shown = Shown, row = Row;
            _layout.MoveRow(Tab, row, _shownRows[shown + delta] - row);
            ShowRows(shown + delta);
            KeepFocus(sender, RowList);
        }

        private void MoveToTab_Click(object sender, RoutedEventArgs e)
        {
            _layout.MoveRowToTab(Tab, Row, (int) ((ComboBoxItem) TargetTab.SelectedItem).Tag);
            ShowRows(Shown);
            KeepFocus(sender, RowList);
        }

        private void Join_Click(object sender, RoutedEventArgs e)
        {
            int shown = Shown;
            _layout.Join(Tab, Row, _shownRows[shown + 1]); // the next row the player can see
            ShowRows(shown);
            KeepFocus(sender, RowList);
        }

        private void Split_Click(object sender, RoutedEventArgs e)
        {
            int shown = Shown;
            _layout.Split(Tab, Row);
            ShowRows(shown);
            KeepFocus(sender, RowList);
        }

        private void Hide_Click(object sender, RoutedEventArgs e)
        {
            int shown = Shown;
            _layout.HideRow(Tab, Row);
            Refresh(Tab, shown);
            KeepFocus(sender, RowList);
        }

        private void ShowPanel_Click(object sender, RoutedEventArgs e)
        {
            int tab = Tab;
            _layout.AddPanel(_panelsToAdd[HiddenList.SelectedIndex].Key, tab);
            Refresh(tab, int.MaxValue); // the panel's new row, at the end
            KeepFocus(sender, HiddenList);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(this,
                LocalizationManager.Get("Reset the window layout to the default? Your tabs, tab names and hidden panels go back to the default, and every row is expanded, when you press OK or Apply."),
                LocalizationManager.Get("Reset layout"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                _layout = CustomLayout.Default(); // like any other edit: OK or Apply applies it, Cancel discards it
                Refresh(0, 0);
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            _apply(_layout.Clone()); // a copy: further edits here must not reach the main window before Apply
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _apply(_layout.Clone());
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
