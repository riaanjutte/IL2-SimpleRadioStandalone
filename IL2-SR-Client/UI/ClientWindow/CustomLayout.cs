using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

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
        public List<LayoutTab> Tabs { get; set; } = new List<LayoutTab>();
        public List<string> Hidden { get; set; } = new List<string>();

        // Row keys, see RowKey
        public HashSet<string> Collapsed { get; set; } = new HashSet<string>();

        public static string RowKey(IList<string> row)
        {
            return string.Join("+", row);
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
                    if (layout == null || layout.Tabs == null || layout.Tabs.Count == 0) layout = Default();
                }
                catch (Exception)
                {
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
            var seen = new HashSet<string>();

            foreach (var tab in Tabs)
            {
                if (string.IsNullOrWhiteSpace(tab.Name)) tab.Name = null;

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

        public void AddTab(string name)
        {
            Tabs.Add(new LayoutTab { Name = name });
            Normalise();
        }

        public bool RenameTab(int tab, string name)
        {
            if (!ValidTab(tab) || string.IsNullOrWhiteSpace(name)) return false;
            Tabs[tab].Name = name.Trim();
            return true;
        }

        public bool DeleteTab(int tab)
        {
            if (!ValidTab(tab) || Tabs.Count <= 1) return false;
            Hidden.AddRange(Tabs[tab].Rows.SelectMany(r => r));
            Tabs.RemoveAt(tab);
            Normalise();
            return true;
        }

        public void MoveTab(int tab, int delta)
        {
            var target = tab + delta;
            if (!ValidTab(tab) || !ValidTab(target)) return;
            var moved = Tabs[tab];
            Tabs.RemoveAt(tab);
            Tabs.Insert(target, moved);
        }

        public void MoveRow(int tab, int row, int delta)
        {
            var target = row + delta;
            if (!ValidRow(tab, row) || !ValidRow(tab, target)) return;
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

        public bool Pair(int tab, int row)
        {
            if (!ValidRow(tab, row) || !ValidRow(tab, row + 1)) return false;
            var rows = Tabs[tab].Rows;
            if (rows[row].Count != 1 || rows[row + 1].Count != 1) return false;
            Collapsed.Remove(RowKey(rows[row]));
            Collapsed.Remove(RowKey(rows[row + 1]));
            rows[row].Add(rows[row + 1][0]);
            rows.RemoveAt(row + 1);
            return true;
        }

        public void Unpair(int tab, int row)
        {
            if (!ValidRow(tab, row) || Tabs[tab].Rows[row].Count != 2) return;
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

        public void ShowPanel(string key, int tab)
        {
            if (!ValidTab(tab) || !Hidden.Remove(key)) return;
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
