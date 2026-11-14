using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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

        /// <summary>Called from the constructor after ApplyClassicThemeLayout.</summary>
        private void InitCustomLayout()
        {
            AutomationProperties.SetName(CustomiseLayoutButton, LocalizationManager.Get("Customise layout"));
            if (_classicLayout)
            {
                CustomiseLayoutButton.Visibility = Visibility.Collapsed;
                return;
            }

            ServerOverlaysCollapseToggle.Visibility = Visibility.Collapsed;
            FavouritesCollapseToggle.Visibility = Visibility.Collapsed;
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

        /// <summary>Rebuilds the tabs from the layout, keeping the shown tab where it still exists, and saves the layout.</summary>
        private void ApplyLayout(CustomLayout layout)
        {
            _layout = layout;
            int selected = TabControl.SelectedIndex;

            foreach (var key in PanelKeys.All)
            {
                var panel = PanelFor(key);
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
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
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

            TabControl.SelectedIndex = Math.Max(0, Math.Min(selected, TabControl.Items.Count - 1));
            _globalSettings.SetClientSetting(GlobalSettingsKeys.CustomLayout, layout.Save());
            Dispatcher.BeginInvoke(new Action(ApplyPanelCollapse), DispatcherPriority.Loaded);
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
                // Classic, or the window is still being built: the XAML arrows.
                if (key == PanelKeys.Server || key == PanelKeys.Overlays)
                {
                    return ServerOverlaysCollapseToggle.IsChecked == true;
                }

                return key == PanelKeys.Favourites && FavouritesCollapseToggle.IsChecked == true;
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
        /// Measures the empty space the collapsed rows leave at the bottom of the shown tab and shrinks the window by it.
        /// </summary>
        private void ApplyLayoutCollapse()
        {
            int index = TabControl.SelectedIndex;
            if (!(TabControl.SelectedItem is TabItem tabItem) || index >= _layout.Tabs.Count)
            {
                return; // between clearing and refilling the tabs
            }

            var rows = _layout.Tabs[index].Rows;
            _collapsedPanelsHeight = 0;
            // A tab that Controls or Help fills (no scroll viewer) has no empty space to give back.
            if (tabItem.Content is ScrollViewer scroll && rows.Any(IsRowCollapsed))
            {
                var content = (FrameworkElement) scroll.Content;
                SetRowsCollapsed(rows, false);
                content.UpdateLayout();
                double expandedHeight = content.DesiredSize.Height;

                SetRowsCollapsed(rows, true);
                content.UpdateLayout();
                _collapsedPanelsHeight = CollapsedPanelsHeight.FreedHeight(
                    scroll.ViewportHeight + _heightReduction, expandedHeight, content.DesiredSize.Height);
            }

            tabItem.Tag = _collapsedPanelsHeight; // used straight away when this tab is shown again
            ResizeForCollapsedPanels();
        }

        /// <param name="saved">true: as saved in the layout; false: all expanded.</param>
        private void SetRowsCollapsed(IEnumerable<List<string>> rows, bool saved)
        {
            foreach (var row in rows)
            {
                SetRowCollapsed(row, saved && IsRowCollapsed(row));
            }
        }

        private static void SetCollapseToolTip(ToggleButton toggle)
        {
            string text = LocalizationManager.Get(toggle.IsChecked == true ? "Expand" : "Collapse");
            toggle.ToolTip = text;
            AutomationProperties.SetName(toggle, text); // the arrow has no text for screen readers otherwise
        }
    }
}
