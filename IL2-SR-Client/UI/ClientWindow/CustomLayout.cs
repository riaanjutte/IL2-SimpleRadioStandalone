using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow
{
    internal static class PanelKeys
    {
        public const string Microphone = "microphone";
        public const string Speakers = "speakers";
        public const string Server = "server";
        public const string Overlays = "overlays";
        public const string Favourites = "favourites";
        public const string Controls = "controls";
        public const string GlobalSettings = "globalSettings";
        public const string RciController = "rciController";
        public const string AudioOptions = "audioOptions";
        public const string Profile = "profile";
        public const string ProfileSettings = "profileSettings";
        public const string Help = "help";

        public static readonly string[] All =
        {
            Microphone, Speakers, Server, Overlays, Favourites, Controls,
            GlobalSettings, RciController, AudioOptions, Profile, ProfileSettings, Help
        };
    }

    internal sealed class LayoutTab
    {
        // null for a custom tab; otherwise general, controls, settings or help
        public string BuiltIn { get; set; }

        // null until renamed
        public string Name { get; set; }

        public List<List<string>> Rows { get; set; } = new List<List<string>>();
    }

    internal sealed class CustomLayout
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public List<LayoutTab> Tabs { get; set; } = new List<LayoutTab>();
        public List<string> Hidden { get; set; } = new List<string>();

        // Row keys, see RowKey
        public HashSet<string> Collapsed { get; set; } = new HashSet<string>();

        public static string RowKey(IList<string> row)
        {
            return string.Join("+", row);
        }

        // A renamed or custom tab shows the player's name as typed; only built-in names are translated.
        public static string Header(LayoutTab tab, Func<string, string> translate)
        {
            return tab.Name ?? translate(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(tab.BuiltIn));
        }

        // Controls and Help fill their tab and scroll inside, so a tab holding only them is not wrapped in a scroll viewer.
        public static bool FillsTab(LayoutTab tab)
        {
            return tab.Rows.Count == 1 && tab.Rows[0].All(k => k == PanelKeys.Controls || k == PanelKeys.Help);
        }

        /// <summary>The tab to show after switching layouts: the same built-in tab, else a custom tab's position, else the first.</summary>
        public static int TabToKeep(CustomLayout old, int oldIndex, CustomLayout updated)
        {
            if (old == null || oldIndex < 0 || oldIndex >= old.Tabs.Count) return 0;
            var builtIn = old.Tabs[oldIndex].BuiltIn;
            if (builtIn != null) return Math.Max(0, updated.Tabs.FindIndex(t => t.BuiltIn == builtIn));
            return oldIndex < updated.Tabs.Count ? oldIndex : 0;
        }

        /// <summary>
        /// The indices of a tab's rows the player can see: a row whose panels are all unavailable in this build
        /// (RCI Controller without access keys) is left out.
        /// </summary>
        public static List<int> ShownRows(LayoutTab tab, Func<string, bool> available)
        {
            return Enumerable.Range(0, tab.Rows.Count).Where(r => tab.Rows[r].Any(available)).ToList();
        }

        public static CustomLayout Default()
        {
            return new CustomLayout
            {
                Tabs =
                {
                    Tab("general", PanelKeys.Microphone + "," + PanelKeys.Speakers, PanelKeys.Server + "," + PanelKeys.Overlays, PanelKeys.Favourites),
                    Tab("controls", PanelKeys.Controls),
                    Tab("settings", PanelKeys.GlobalSettings, PanelKeys.RciController, PanelKeys.AudioOptions, PanelKeys.Profile, PanelKeys.ProfileSettings),
                    Tab("help", PanelKeys.Help)
                }
            };
        }

        private static LayoutTab Tab(string builtIn, params string[] rows)
        {
            return new LayoutTab { BuiltIn = builtIn, Rows = rows.Select(r => r.Split(',').ToList()).ToList() };
        }

        public static CustomLayout Load(string stored, bool serverOverlaysCollapsed, bool favouritesCollapsed)
        {
            CustomLayout layout;
            if (string.IsNullOrEmpty(stored))
            {
                layout = Default();
                if (serverOverlaysCollapsed) layout.Collapsed.Add(PanelKeys.Server + "+" + PanelKeys.Overlays);
                if (favouritesCollapsed) layout.Collapsed.Add(PanelKeys.Favourites);
            }
            else
            {
                try
                {
                    layout = JsonConvert.DeserializeObject<CustomLayout>(Encoding.UTF8.GetString(Convert.FromBase64String(stored)));
                    layout?.Normalise();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Saved window layout is unreadable, using the default layout");
                    layout = null;
                }

                if (layout == null || layout.Tabs.Count == 0)
                {
                    if (layout != null) Logger.Warn("Saved window layout has no tabs, using the default layout");
                    layout = Default();
                }
            }

            layout.Normalise();
            return layout;
        }

        // Base64 because raw JSON does not survive the settings file.
        public string Save()
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)));
        }

        public CustomLayout Clone()
        {
            return JsonConvert.DeserializeObject<CustomLayout>(JsonConvert.SerializeObject(this));
        }

        public void Normalise()
        {
            Tabs = (Tabs ?? new List<LayoutTab>()).Where(t => t != null).ToList();
            foreach (var tab in Tabs.Where(t => string.IsNullOrWhiteSpace(t.Name))) tab.Name = null;
            // A custom tab with no name has nothing to show in the tab strip; its panels fall back to the first tab.
            Tabs = Tabs.Where(t => t.BuiltIn != null || t.Name != null).ToList();
            var seen = new HashSet<string>();

            foreach (var tab in Tabs)
            {
                var rows = new List<List<string>>();
                foreach (var row in tab.Rows ?? new List<List<string>>())
                {
                    var kept = (row ?? new List<string>()).Where(k => k != null && PanelKeys.All.Contains(k) && seen.Add(k)).Take(2).ToList();
                    if (kept.Count > 0) rows.Add(kept);
                }

                tab.Rows = rows;
            }

            Hidden = (Hidden ?? new List<string>()).Where(k => k != null && PanelKeys.All.Contains(k) && seen.Add(k)).ToList();

            if (Tabs.Count > 0)
            {
                foreach (var key in PanelKeys.All.Where(k => !seen.Contains(k)))
                    Tabs[0].Rows.Add(new List<string> { key });
            }

            var rowKeys = new HashSet<string>(Tabs.SelectMany(t => t.Rows).Select(RowKey));
            Collapsed = new HashSet<string>((Collapsed ?? new HashSet<string>()).Where(rowKeys.Contains));
        }

        public bool AddTab(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            Tabs.Add(new LayoutTab { Name = name.Trim() });
            Normalise();
            return true;
        }

        public bool RenameTab(int tab, string name)
        {
            if (!ValidTab(tab) || string.IsNullOrWhiteSpace(name)) return false;
            Tabs[tab].Name = name.Trim();
            return true;
        }

        public bool CanDeleteTab(int tab)
        {
            return ValidTab(tab) && Tabs.Count > 1;
        }

        public bool DeleteTab(int tab)
        {
            if (!CanDeleteTab(tab)) return false;
            Hidden.AddRange(Tabs[tab].Rows.SelectMany(r => r));
            Tabs.RemoveAt(tab);
            Normalise();
            return true;
        }

        public bool CanMoveTab(int tab, int delta)
        {
            return ValidTab(tab) && ValidTab(tab + delta);
        }

        public void MoveTab(int tab, int delta)
        {
            var target = tab + delta;
            if (!CanMoveTab(tab, delta)) return;
            var moved = Tabs[tab];
            Tabs.RemoveAt(tab);
            Tabs.Insert(target, moved);
        }

        public bool CanMoveRow(int tab, int row, int delta)
        {
            return ValidRow(tab, row) && ValidRow(tab, row + delta);
        }

        public void MoveRow(int tab, int row, int delta)
        {
            var target = row + delta;
            if (!CanMoveRow(tab, row, delta)) return;
            var rows = Tabs[tab].Rows;
            var moved = rows[row];
            Collapsed.Remove(RowKey(moved));
            rows.RemoveAt(row);
            rows.Insert(target, moved);
        }

        public void MoveRowToTab(int tab, int row, int targetTab)
        {
            if (!ValidRow(tab, row) || !ValidTab(targetTab)) return;
            var moved = Tabs[tab].Rows[row];
            Collapsed.Remove(RowKey(moved));
            Tabs[tab].Rows.RemoveAt(row);
            Tabs[targetTab].Rows.Add(moved);
        }

        public bool CanPair(int tab, int row)
        {
            return CanPair(tab, row, row + 1);
        }

        /// <param name="below">A later row of the same tab; rows in between (ones the player can't see) stay below the pair.</param>
        public bool CanPair(int tab, int row, int below)
        {
            return below > row && ValidRow(tab, row) && ValidRow(tab, below) && Tabs[tab].Rows[row].Count == 1 && Tabs[tab].Rows[below].Count == 1;
        }

        public bool Pair(int tab, int row, int below)
        {
            if (!CanPair(tab, row, below)) return false;
            MoveRow(tab, below, row + 1 - below);
            return Pair(tab, row);
        }

        public bool Pair(int tab, int row)
        {
            if (!CanPair(tab, row)) return false;
            var rows = Tabs[tab].Rows;
            Collapsed.Remove(RowKey(rows[row]));
            Collapsed.Remove(RowKey(rows[row + 1]));
            rows[row].Add(rows[row + 1][0]);
            rows.RemoveAt(row + 1);
            return true;
        }

        public bool CanUnpair(int tab, int row)
        {
            return ValidRow(tab, row) && Tabs[tab].Rows[row].Count == 2;
        }

        public void Unpair(int tab, int row)
        {
            if (!CanUnpair(tab, row)) return;
            var rows = Tabs[tab].Rows;
            var pair = rows[row];
            Collapsed.Remove(RowKey(pair));
            rows[row] = new List<string> { pair[0] };
            rows.Insert(row + 1, new List<string> { pair[1] });
        }

        public void HideRow(int tab, int row)
        {
            if (!ValidRow(tab, row)) return;
            Hidden.AddRange(Tabs[tab].Rows[row]);
            Tabs[tab].Rows.RemoveAt(row);
            Normalise();
        }

        /// <summary>The panels that could be added to a tab: hidden ones (tab -1) first, then other tabs' panels in order.</summary>
        public IEnumerable<KeyValuePair<string, int>> PanelsToAdd(int tab)
        {
            return Hidden.Select(k => new KeyValuePair<string, int>(k, -1))
                .Concat(Tabs.SelectMany((t, i) => i == tab ? Enumerable.Empty<KeyValuePair<string, int>>()
                    : t.Rows.SelectMany(r => r).Select(k => new KeyValuePair<string, int>(k, i))));
        }

        /// <summary>Adds a hidden panel, or one from another tab, as a new row at the end of the tab; a pair it leaves keeps its other panel.</summary>
        public void AddPanel(string key, int tab)
        {
            if (!ValidTab(tab) || Tabs[tab].Rows.Any(r => r.Contains(key))) return;
            if (!Hidden.Remove(key))
            {
                var from = Tabs.FirstOrDefault(t => t.Rows.Any(r => r.Contains(key)));
                if (from == null) return;
                var row = from.Rows.First(r => r.Contains(key));
                Collapsed.Remove(RowKey(row));
                row.Remove(key);
                if (row.Count == 0) from.Rows.Remove(row);
            }

            Tabs[tab].Rows.Add(new List<string> { key });
        }

        private bool ValidTab(int tab)
        {
            return tab >= 0 && tab < Tabs.Count;
        }

        private bool ValidRow(int tab, int row)
        {
            return ValidTab(tab) && row >= 0 && row < Tabs[tab].Rows.Count;
        }
    }
}
