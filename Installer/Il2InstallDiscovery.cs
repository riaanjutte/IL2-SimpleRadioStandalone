using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Installer
{
    internal sealed class Il2Install
    {
        public Il2Install(string displayName, string installPath)
        {
            DisplayName = displayName;
            InstallPath = installPath;
        }

        public string DisplayName { get; private set; }
        public string InstallPath { get; private set; }
        public string StartupConfigPath
        {
            get { return Path.Combine(InstallPath, "data", "startup.cfg"); }
        }
    }

    internal static class Il2InstallDiscovery
    {
        private const string GreatBattlesDisplayName = "IL-2 Sturmovik Great Battles";
        private const string KoreaDisplayName = "IL-2 Korea";
        private const string RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\IL2-SRS";

        private static readonly string[] ProcessNames = { "Il-2", "IL2Series" };
        private static readonly string[] KnownFolderNames =
        {
            "IL-2 Sturmovik Battle of Stalingrad",
            "IL-2 Sturmovik Great Battles",
            "IL-2 Sturmovik Korea",
            "IL-2 Korea"
        };

        public static List<Il2Install> FindInstalledGames(params string[] explicitPaths)
        {
            Dictionary<string, Il2Install> installs =
                new Dictionary<string, Il2Install>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in explicitPaths ?? new string[0])
            {
                AddCandidate(installs, path, string.Empty);
                AddSiblingCandidates(installs, path);
            }

            AddSavedCandidate(installs, "IL2Path");
            AddSavedCandidate(installs, "IL2GreatBattlesPath");
            AddSavedCandidate(installs, "IL2KoreaPath");
            AddRunningProcessCandidates(installs);
            AddUninstallRegistryCandidates(installs);
            AddSteamCandidates(installs);
            AddCommonFolderCandidates(installs);

            return installs.Values
                .OrderBy(install => install.DisplayName.IndexOf("Korea", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0)
                .ThenBy(install => install.InstallPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool IsKorea(Il2Install install)
        {
            return install != null
                   && install.DisplayName.IndexOf("Korea", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddSavedCandidate(Dictionary<string, Il2Install> installs, string name)
        {
            string path = Convert.ToString(Registry.GetValue(RegistryPath, name, ""), CultureInfo.InvariantCulture);
            AddCandidate(installs, path, string.Empty);
            AddSiblingCandidates(installs, path);
        }

        private static void AddRunningProcessCandidates(Dictionary<string, Il2Install> installs)
        {
            foreach (string processName in ProcessNames)
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        string path = process.MainModule == null ? string.Empty : process.MainModule.FileName;
                        AddCandidate(installs, path, string.Empty);
                        AddSiblingCandidates(installs, path);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private static void AddUninstallRegistryCandidates(Dictionary<string, Il2Install> installs)
        {
            RegistryHive[] hives = { RegistryHive.CurrentUser, RegistryHive.LocalMachine };
            RegistryView[] views = { RegistryView.Registry64, RegistryView.Registry32 };

            foreach (RegistryHive hive in hives)
            {
                foreach (RegistryView view in views)
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                        using (RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (uninstall == null)
                            {
                                continue;
                            }

                            foreach (string subKeyName in uninstall.GetSubKeyNames())
                            {
                                using (RegistryKey appKey = uninstall.OpenSubKey(subKeyName))
                                {
                                    if (appKey == null)
                                    {
                                        continue;
                                    }

                                    string displayName = Convert.ToString(appKey.GetValue("DisplayName"), CultureInfo.InvariantCulture);
                                    if (!LooksLikeIl2(displayName))
                                    {
                                        continue;
                                    }

                                    AddCandidate(installs, Convert.ToString(appKey.GetValue("InstallLocation"), CultureInfo.InvariantCulture), displayName);
                                    AddCandidate(installs, ExtractExecutablePath(Convert.ToString(appKey.GetValue("DisplayIcon"), CultureInfo.InvariantCulture)), displayName);
                                    AddCandidate(installs, ExtractExecutablePath(Convert.ToString(appKey.GetValue("UninstallString"), CultureInfo.InvariantCulture)), displayName);
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void AddSteamCandidates(Dictionary<string, Il2Install> installs)
        {
            foreach (string steamRoot in GetSteamLibraryRoots())
            {
                string commonPath = Path.Combine(steamRoot, "steamapps", "common");
                if (!Directory.Exists(commonPath))
                {
                    continue;
                }

                AddKnownFolders(installs, commonPath);
                try
                {
                    foreach (string directory in Directory.GetDirectories(commonPath))
                    {
                        if (LooksLikeIl2(Path.GetFileName(directory)))
                        {
                            AddCandidate(installs, directory, Path.GetFileName(directory));
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddCommonFolderCandidates(Dictionary<string, Il2Install> installs)
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] roots =
            {
                programFiles,
                programFilesX86,
                Path.Combine(programFiles, "1C Game Studios"),
                Path.Combine(programFilesX86, "1C Game Studios")
            };

            foreach (string root in roots.Where(Directory.Exists))
            {
                AddKnownFolders(installs, root);
                try
                {
                    foreach (string directory in Directory.GetDirectories(root))
                    {
                        if (LooksLikeIl2(Path.GetFileName(directory)))
                        {
                            AddCandidate(installs, directory, Path.GetFileName(directory));
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddSiblingCandidates(Dictionary<string, Il2Install> installs, string path)
        {
            string root = FindValidRoot(path);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            AddKnownFolders(installs, root);
            DirectoryInfo parent = Directory.GetParent(root);
            if (parent != null)
            {
                AddKnownFolders(installs, parent.FullName);
                try
                {
                    foreach (string directory in Directory.GetDirectories(parent.FullName))
                    {
                        if (LooksLikeIl2(Path.GetFileName(directory)))
                        {
                            AddCandidate(installs, directory, Path.GetFileName(directory));
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddKnownFolders(Dictionary<string, Il2Install> installs, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return;
            }

            foreach (string folderName in KnownFolderNames)
            {
                AddCandidate(installs, Path.Combine(root, folderName), folderName);
            }
        }

        private static void AddCandidate(Dictionary<string, Il2Install> installs, string path, string displayHint)
        {
            string root = FindValidRoot(path);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            string configPath = Path.GetFullPath(Path.Combine(root, "data", "startup.cfg"));
            string displayName = InferDisplayName(root + " " + displayHint);
            installs[configPath] = new Il2Install(displayName, root);
        }

        private static string FindValidRoot(string path)
        {
            path = NormalizePath(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (File.Exists(path))
            {
                path = Path.GetDirectoryName(path);
            }

            List<string> candidates = new List<string>();
            AddPathCandidate(candidates, path);
            AddPathCandidate(candidates, Path.Combine(path, "Game"));

            string current = path;
            for (int i = 0; i < 6 && !string.IsNullOrWhiteSpace(current); i++)
            {
                AddPathCandidate(candidates, current);
                AddPathCandidate(candidates, Path.Combine(current, "Game"));
                try
                {
                    current = Directory.GetParent(current) == null ? string.Empty : Directory.GetParent(current).FullName;
                }
                catch
                {
                    break;
                }
            }

            foreach (string candidate in candidates)
            {
                try
                {
                    if (Directory.Exists(candidate)
                        && File.Exists(Path.Combine(candidate, "data", "startup.cfg")))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static void AddPathCandidate(ICollection<string> candidates, string path)
        {
            if (!string.IsNullOrWhiteSpace(path)
                && !candidates.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(path);
            }
        }

        private static IEnumerable<string> GetSteamLibraryRoots()
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddSteamRoot(roots, Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", ""), CultureInfo.InvariantCulture));
            AddSteamRoot(roots, Convert.ToString(Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "InstallPath", ""), CultureInfo.InvariantCulture));
            AddSteamRoot(roots, Convert.ToString(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", ""), CultureInfo.InvariantCulture));
            AddSteamRoot(roots, Convert.ToString(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", ""), CultureInfo.InvariantCulture));

            foreach (string root in roots.ToList())
            {
                string libraryFile = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFile))
                {
                    continue;
                }

                try
                {
                    string text = File.ReadAllText(libraryFile);
                    foreach (Match match in Regex.Matches(text, "\"(?:path|\\d+)\"\\s+\"(?<path>.*?)\"", RegexOptions.IgnoreCase))
                    {
                        AddSteamRoot(roots, match.Groups["path"].Value.Replace(@"\\", @"\"));
                    }
                }
                catch
                {
                }
            }

            return roots;
        }

        private static void AddSteamRoot(ISet<string> roots, string path)
        {
            path = NormalizePath(path);
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                roots.Add(Path.GetFullPath(path));
            }
        }

        private static string ExtractExecutablePath(string value)
        {
            value = NormalizePath(value);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            int exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe >= 0 ? value.Substring(0, exe + 4).Trim('"') : value;
        }

        private static string NormalizePath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        }

        private static bool LooksLikeIl2(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string lower = value.ToLowerInvariant();
            return (lower.Contains("il-2") || lower.Contains("il2") || lower.Contains("sturmovik"))
                   && (lower.Contains("korea")
                       || lower.Contains("great battles")
                       || lower.Contains("battle of stalingrad")
                       || lower.Contains("sturmovik"));
        }

        private static string InferDisplayName(string value)
        {
            return value.IndexOf("korea", StringComparison.OrdinalIgnoreCase) >= 0
                ? KoreaDisplayName
                : GreatBattlesDisplayName;
        }
    }
}
