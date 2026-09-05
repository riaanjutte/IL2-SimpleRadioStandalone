using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers
{
    public static class Il2SteamInstallDiscovery
    {
        private static readonly Regex InstallDirectorySetting = new Regex(
            "^[ \\t]*\"installdir\"[ \\t]+\"(?<directory>[^\"\\r\\n]+)\"[ \\t]*\\r?$",
            RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static bool HasKoreaExecutable(string root)
        {
            return File.Exists(Path.Combine(root, "bin", "game", "IL2Series.exe"));
        }

        public static bool HasGameExecutable(string root)
        {
            return HasKoreaExecutable(root)
                   || File.Exists(Path.Combine(root, "bin", "game", "Il-2.exe"));
        }

        public static IEnumerable<string> FindManifestInstallRoots(string libraryRoot)
        {
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string steamApps = Path.Combine(libraryRoot, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                return roots;
            }

            string[] manifests;
            try
            {
                manifests = Directory.GetFiles(steamApps, "appmanifest_*.acf");
            }
            catch (IOException) { return roots; }
            catch (UnauthorizedAccessException) { return roots; }

            string common = Path.GetFullPath(Path.Combine(steamApps, "common")) + Path.DirectorySeparatorChar;
            foreach (string manifest in manifests)
            {
                try
                {
                    Match setting = InstallDirectorySetting.Match(File.ReadAllText(manifest));
                    if (!setting.Success)
                    {
                        continue;
                    }

                    string directory = setting.Groups["directory"].Value;
                    if (Path.IsPathRooted(directory))
                    {
                        continue;
                    }

                    string install = Path.GetFullPath(Path.Combine(common, directory));
                    if (!install.StartsWith(common, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Executables identify the game even when Steam's folder or display name differs.
                    foreach (string root in new[] { install, Path.Combine(install, "Game") })
                    {
                        if (Directory.Exists(Path.Combine(root, "data")) && HasGameExecutable(root))
                        {
                            roots.Add(root);
                        }
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
                catch (NotSupportedException) { }
            }

            return roots;
        }
    }
}
