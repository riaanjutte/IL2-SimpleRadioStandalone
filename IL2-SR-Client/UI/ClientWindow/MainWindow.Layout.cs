using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI
{
    /// <summary>Outside Classic the tabs are built from the player's layout (see CustomLayout) out of the XAML panels.</summary>
    public partial class MainWindow
    {
        // Controls and Help scroll inside themselves, so on a tab that scrolls they need a height of their own.
        private const double SharedFillPanelMaxHeight = 400;

        private CustomLayout _layout;

        // Set while ApplyLayout swaps the tabs, so the TabControl's selection changes don't resize the window.
        private bool _rebuildingTabs;

        /// <summary>Called from the constructor after ApplyClassicThemeLayout.</summary>
        private void InitCustomLayout()
        {
            AutomationProperties.SetName(CustomiseLayoutButton, LocalizationManager.Get("Customise layout"));
            if (_classicLayout)
            {
                CustomiseLayoutButton.Visibility = Visibility.Collapsed;
                return;
            }

            CustomiseLayoutButton.Click += (sender, e) =>
                new LayoutEditorWindow(_layout, ApplyLayout)
                {
                    Owner = this,
                    PanelName = PanelHeader,
                    // The panel's own rule (RCI Controller without access keys); the layout never sets a panel's Visibility.
                    Available = key => PanelFor(key).Visibility == Visibility.Visible
                }.ShowDialog();
            ServerOverlaysCollapseToggle.Visibility = Visibility.Collapsed;
            TabControl.Loaded += (sender, e) => ReserveTabStripSpaceForCustomiseButton();
            ApplyLayout(CustomLayout.Load(
                _globalSettings.GetClientSetting(GlobalSettingsKeys.CustomLayout).RawValue,
                _globalSettings.GetClientSettingBool(GlobalSettingsKeys.ServerOverlaysCollapsed),
                _globalSettings.GetClientSettingBool(GlobalSettingsKeys.FavouritesCollapsed)));
        }

        private FrameworkElement PanelFor(string key)
        {
            switch (key)
            {
                case PanelKeys.Microphone: return MicrophonePanel;
                case PanelKeys.Speakers: return SpeakersPanel;
                case PanelKeys.Server: return ServerPanel;
                case PanelKeys.Overlays: return OverlaysColumn;
                case PanelKeys.Favourites: return FavouritesGroupBox;
                case PanelKeys.Controls: return ControlsGroupBox;
                case PanelKeys.GlobalSettings: return GlobalSettingsPanel;
                case PanelKeys.RciController: return RciControllerGroup;
                case PanelKeys.AudioOptions: return AudioOptionsPanel;
                case PanelKeys.Profile: return ProfilePanel;
                case PanelKeys.ProfileSettings: return ProfileSettingsPanel;
                case PanelKeys.Help: return HelpPanel;
                default: throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown panel");
            }
        }

        /// <summary>The ⚙ button sits over the right end of the tab strip, so the strip wraps before reaching it.</summary>
        private void ReserveTabStripSpaceForCustomiseButton()
        {
            var strip = FindDescendant<TabPanel>(TabControl);
            if (strip == null)
            {
                return;
            }

            double reserve = CustomiseLayoutButton.Width + CustomiseLayoutButton.Margin.Right + 4;
            var margin = strip.Margin;
            strip.Margin = new Thickness(margin.Left, margin.Top, Math.Max(margin.Right, reserve), margin.Bottom);
        }

        private static T FindDescendant<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var found = child as T ?? FindDescendant<T>(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>A panel's translated header, as its group box shows it.</summary>
        private string PanelHeader(string key)
        {
            var panel = PanelFor(key) as HeaderedContentControl ?? OverlaysColumn.Children.OfType<GroupBox>().First();
            return panel.Header as string;
        }

        /// <summary>Rebuilds the tabs from the layout, keeping the shown tab where it still exists, and saves the layout.</summary>
        private void ApplyLayout(CustomLayout layout)
        {
            layout.Normalise();
            if (layout.Tabs.Count == 0)
            {
                layout = CustomLayout.Default(); // with no tab the panels would have nowhere to go
            }

            int selected = CustomLayout.TabToKeep(_layout, TabControl.SelectedIndex, layout);
            _layout = layout;
            _rebuildingTabs = true;
            try
            {
                BuildTabs(layout, selected);
            }
            finally
            {
                _rebuildingTabs = false;
            }

            _globalSettings.SetClientSetting(GlobalSettingsKeys.CustomLayout, layout.Save());
            // Measured once the new tab is laid out.
            Dispatcher.BeginInvoke(new Action(ApplyPanelCollapse), DispatcherPriority.Loaded);
        }

        private void BuildTabs(CustomLayout layout, int selected)
        {
            foreach (var key in PanelKeys.All)
            {
                var panel = PanelFor(key);
                // Pinned: off every parent the panel would lose the window's DataContext for a moment, and its two-way
                // bound pickers (devices, server address) could write the cleared selection back.
                if (DataContext != null)
                {
                    panel.DataContext = DataContext;
                }

                if (panel.Parent is Panel parent)
                {
                    parent.Children.Remove(panel);
                }
                else if (panel.Parent is ContentControl owner)
                {
                    owner.Content = null;
                }
            }

            TabControl.Items.Clear();
            foreach (var tab in layout.Tabs)
            {
                object content;
                if (CustomLayout.FillsTab(tab))
                {
                    content = BuildRow(tab.Rows[0], true);
                }
                else
                {
                    var rows = new StackPanel { Margin = new Thickness(10, 0, 10, 0) };
                    foreach (var row in tab.Rows)
                    {
                        rows.Children.Add(BuildRow(row, false));
                    }

                    content = new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        // Settings always showed its scroll bar; keep it so the tab looks as it did.
                        VerticalScrollBarVisibility = tab.BuiltIn == "settings" ? ScrollBarVisibility.Visible : ScrollBarVisibility.Auto,
                        Content = rows
                    };
                }

                TabControl.Items.Add(new TabItem { Header = CustomLayout.Header(tab, LocalizationManager.Get), Content = content });
            }

            foreach (var key in layout.Hidden)
            {
                SetPanelCollapsed(key, false); // a collapsed row that was hidden comes back expanded
                HiddenPanelsHost.Children.Add(PanelFor(key));
            }

            TabControl.SelectedIndex = selected;
        }

        private Grid BuildRow(List<string> row, bool fillsTab)
        {
            var grid = new Grid();
            var panels = row.Select(PanelFor).ToList();
            // A panel hidden by its own rule (RCI Controller without access keys) leaves the whole row to the other.
            var shown = panels.Where(p => p.Visibility == Visibility.Visible).ToList();
            for (int i = 0; i < Math.Max(1, shown.Count); i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
            }

            foreach (var panel in panels)
            {
                Grid.SetColumn(panel, Math.Max(0, shown.IndexOf(panel)));
                if (!fillsTab && (panel == ControlsGroupBox || panel == HelpPanel))
                {
                    panel.MaxHeight = SharedFillPanelMaxHeight;
                }
                else
                {
                    panel.ClearValue(MaxHeightProperty);
                }

                grid.Children.Add(panel);
            }

            grid.Visibility = shown.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            bool collapsed = IsRowCollapsed(row);
            SetRowCollapsed(row, collapsed);
            // Over the right-hand panel's header bar, like the Classic Server/Overlays arrow.
            var toggle = new ToggleButton
            {
                Style = (Style) FindResource("PanelCollapseToggleStyle"),
                Margin = new Thickness(0, 16, 18, 0),
                IsChecked = collapsed
            };
            Grid.SetColumn(toggle, grid.ColumnDefinitions.Count - 1);
            SetCollapseToolTip(toggle);
            toggle.Click += (sender, e) =>
            {
                bool nowCollapsed = toggle.IsChecked == true;
                if (nowCollapsed)
                {
                    _layout.Collapsed.Add(CustomLayout.RowKey(row));
                }
                else
                {
                    _layout.Collapsed.Remove(CustomLayout.RowKey(row));
                }

                _globalSettings.SetClientSetting(GlobalSettingsKeys.CustomLayout, _layout.Save());
                SetRowCollapsed(row, nowCollapsed);
                SetCollapseToolTip(toggle);
                ApplyPanelCollapse();
            };
            grid.Children.Add(toggle);
            return grid;
        }

        private bool IsRowCollapsed(IList<string> row)
        {
            return _layout.Collapsed.Contains(CustomLayout.RowKey(row));
        }

        private bool IsPanelCollapsed(string key)
        {
            if (_layout == null)
            {
                // Classic, or the window is still being built: the XAML arrow.
                return (key == PanelKeys.Server || key == PanelKeys.Overlays) && ServerOverlaysCollapseToggle.IsChecked == true;
            }

            var row = _layout.Tabs.SelectMany(t => t.Rows).FirstOrDefault(r => r.Contains(key));
            return row != null && IsRowCollapsed(row);
        }

        private void SetRowCollapsed(IEnumerable<string> row, bool collapsed)
        {
            foreach (var key in row)
            {
                SetPanelCollapsed(key, collapsed);
            }
        }

        /// <summary>Collapses a panel to its header bar.</summary>
        private void SetPanelCollapsed(string key, bool collapsed)
        {
            var panel = PanelFor(key);
            // Top-aligned, so a panel that fills its tab shrinks to its header bar too.
            panel.VerticalAlignment = collapsed ? VerticalAlignment.Top : VerticalAlignment.Stretch;
            var visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
            if (key == PanelKeys.Overlays)
            {
                OverlaysLayout.Visibility = visibility;
                CombatBoxRciPanel.Visibility = _showCombatBoxRci && !collapsed ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (((GroupBox) panel).Content is UIElement content)
            {
                content.Visibility = visibility;
            }
        }

        /// <summary>
        /// Measures the empty space at the bottom of the shown tab (panels moved elsewhere, rows collapsed) and shrinks
        /// the window by it, so the window fits the tab; a tab that fills or overflows keeps the normal height.
        /// </summary>
        private void ApplyLayoutCollapse()
        {
            int index = TabControl.SelectedIndex;
            if (!(TabControl.SelectedItem is TabItem tabItem) || index >= _layout.Tabs.Count)
            {
                return; // between clearing and refilling the tabs
            }

            if (WindowState != WindowState.Normal || IsSnapped())
            {
                return; // the viewport isn't the normal height's; measured again when restored (see InitPanelCollapse)
            }

            var rows = _layout.Tabs[index].Rows;
            _collapsedPanelsHeight = 0;
            if (tabItem.Content is Grid rowGrid)
            {
                // Controls or Help filling its tab: expanded it fills the whole viewport, so only collapsing it frees
                // space. Collapsed it is top-aligned, so its height doesn't depend on the window's and can't swing.
                if (rows.Any(IsRowCollapsed))
                {
                    rowGrid.UpdateLayout();
                    _collapsedPanelsHeight = CollapsedPanelsHeight.EmptyHeight(
                        rowGrid.ActualHeight + _heightReduction, rowGrid.DesiredSize.Height);
                }
            }
            else if (tabItem.Content is ScrollViewer scroll)
            {
                // The content is measured at its natural height whatever the window's, so this can't swing either.
                var content = (FrameworkElement) scroll.Content;
                content.UpdateLayout();
                _collapsedPanelsHeight = CollapsedPanelsHeight.EmptyHeight(
                    scroll.ViewportHeight + _heightReduction, content.DesiredSize.Height);
            }

            tabItem.Tag = _collapsedPanelsHeight; // used straight away when this tab is shown again
            ResizeForCollapsedPanels();
        }

        private static void SetCollapseToolTip(ToggleButton toggle)
        {
            string text = LocalizationManager.Get(toggle.IsChecked == true ? "Expand" : "Collapse");
            toggle.ToolTip = text;
            AutomationProperties.SetName(toggle, text); // the arrow has no text for screen readers otherwise
        }
    }
}
