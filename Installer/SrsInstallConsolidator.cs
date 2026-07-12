using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IWshRuntimeLibrary;
using Microsoft.Win32;
using File = System.IO.File;

namespace Installer
{
    internal sealed class SrsConsolidationPlan
    {
        public SrsConsolidationPlan(string destination, IEnumerable<string> installations)
        {
            Destination = Path.GetFullPath(destination);
            Installations = installations
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public string Destination { get; private set; }
        public List<string> Installations { get; private set; }

        public List<string> DuplicateInstallations
        {
            get
            {
                return Installations
                    .Where(path => !SrsInstallConsolidator.PathsEqual(path, Destination))
                    .ToList();
            }
        }
    }

    internal sealed class SrsConsolidationResult
    {
        public string UserDataPath { get; set; }
        public string BackupPath { get; set; }
        public int MigratedInstallationCount { get; set; }
        public int RetiredInstallationCount { get; set; }
    }

    internal static class SrsInstallConsolidator
    {
        private const string ClientExecutable = "IL2-SR-ClientRadio.exe";
        private const string RegistryPath = @"HKEY_CURRENT_USER\SOFTWARE\IL2-SRS";
        private static readonly string[] KnownProgramFiles =
        {
            "IL2-SR-ClientRadio.exe",
            "IL2-SR-Server.exe",
            "IL2-SRS-External-Audio.exe",
            "IL2-SRS-AutoUpdater.exe",
            "SRS-AutoUpdater.exe",
            "opus.dll",
            "speexdsp.dll"
        };

        public static string UserDataPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "IL2-SRS");
            }
        }

        public static SrsConsolidationPlan CreatePlan(string destination, string registeredPath)
        {
            HashSet<string> installations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddInstall(installations, registeredPath);
            AddInstall(installations, destination);
            AddInstall(installations, ReadRegistryPath(@"HKEY_CURRENT_USER\SOFTWARE\IL2-SR-Standalone", "SRPathStandalone"));

            string canonical = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "IL2-SimpleRadio-Standalone");
            AddInstall(installations, canonical);
            AddInstall(installations, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "IL2-SimpleRadio-Standalone"));

            AddStartMenuShortcutInstall(installations);
            AddProgramFilesCandidates(installations);
            AddGameFolderCandidates(installations);

            return new SrsConsolidationPlan(destination, installations);
        }

        public static SrsConsolidationResult MigrateUserData(
            SrsConsolidationPlan plan,
            string registeredPath,
            Action<string> log)
        {
            return MigrateUserDataTo(plan, registeredPath, UserDataPath, log);
        }

        internal static SrsConsolidationResult MigrateUserDataTo(
            SrsConsolidationPlan plan,
            string registeredPath,
            string userDataPath,
            Action<string> log)
        {
            Directory.CreateDirectory(userDataPath);

            string migrationMarker = Path.Combine(userDataPath, ".legacy-migration-complete");
            bool migrationAlreadyCompleted = File.Exists(migrationMarker);
            List<string> sources = plan.Installations
                .Where(ContainsUserData)
                .Where(source => !migrationAlreadyCompleted
                                 || plan.DuplicateInstallations.Any(duplicate => PathsEqual(duplicate, source)))
                .ToList();

            if (migrationAlreadyCompleted && sources.Count == 0)
            {
                return new SrsConsolidationResult
                {
                    UserDataPath = userDataPath,
                    MigratedInstallationCount = 0
                };
            }

            string backupRoot = null;
            bool existingUserData = ContainsUserData(userDataPath);
            if (sources.Count > 0)
            {
                backupRoot = Path.Combine(
                    userDataPath,
                    "MigrationBackups",
                    DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
                Directory.CreateDirectory(backupRoot);

                if (existingUserData)
                {
                    BackupUserData(userDataPath, backupRoot, log);
                }

                foreach (string source in sources)
                {
                    BackupUserData(source, backupRoot, log);
                }
            }

            string primary = SelectPrimarySource(sources, registeredPath);
            if (!File.Exists(Path.Combine(userDataPath, "global.cfg"))
                && !string.IsNullOrWhiteSpace(primary))
            {
                string primaryGlobal = Path.Combine(primary, "global.cfg");
                if (File.Exists(primaryGlobal))
                {
                    File.Copy(primaryGlobal, Path.Combine(userDataPath, "global.cfg"), false);
                    Log(log, "Migrated primary global configuration from " + primary);
                }
            }

            HashSet<string> targetProfiles = ReadProfileNames(Path.Combine(userDataPath, "global.cfg"));
            if (targetProfiles.Count == 0)
            {
                targetProfiles.Add("default");
            }

            foreach (string source in OrderSources(sources, primary))
            {
                ImportProfiles(source, targetProfiles, userDataPath, log);
                ImportFavouriteServers(source, userDataPath, log);
                ImportTextConfiguration(source, userDataPath, log);
            }

            EnsureGlobalProfileList(targetProfiles, userDataPath);
            File.WriteAllText(
                migrationMarker,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            return new SrsConsolidationResult
            {
                UserDataPath = userDataPath,
                BackupPath = backupRoot,
                MigratedInstallationCount = sources.Count
            };
        }

        public static int RetireDuplicateInstallations(SrsConsolidationPlan plan, Action<string> log)
        {
            int retired = 0;
            foreach (string source in plan.DuplicateInstallations)
            {
                string clientPath = Path.Combine(source, ClientExecutable);
                if (!File.Exists(clientPath))
                {
                    continue;
                }

                foreach (string fileName in KnownProgramFiles)
                {
                    DeleteFile(Path.Combine(source, fileName), log);
                }

                DeleteKnownDirectory(Path.Combine(source, "AudioEffects"), log);
                DeleteKnownDirectory(Path.Combine(source, "Localization"), log);
                if (File.Exists(clientPath))
                {
                    throw new IOException(
                        "SRS was installed, but the duplicate copy at " + source +
                        " could not be removed. Close any SRS processes and run the installer as administrator.");
                }

                retired++;
                Log(log, "Retired duplicate SRS program files from " + source);
            }

            return retired;
        }

        public static bool IsInsideGameFolder(string destination)
        {
            string fullDestination = NormalizeDirectory(destination);
            if (string.IsNullOrWhiteSpace(fullDestination))
            {
                return false;
            }

            foreach (Il2Install install in Il2InstallDiscovery.FindInstalledGames())
            {
                string gameRoot = NormalizeDirectory(install.InstallPath);
                if (IsSameOrChildPath(fullDestination, gameRoot))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool PathsEqual(string left, string right)
        {
            string normalizedLeft = NormalizeDirectory(left);
            string normalizedRight = NormalizeDirectory(right);
            return !string.IsNullOrWhiteSpace(normalizedLeft)
                   && string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static void AddStartMenuShortcutInstall(ISet<string> installations)
        {
            string[] shortcutRoots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
                Environment.GetFolderPath(Environment.SpecialFolder.Programs)
            };

            foreach (string root in shortcutRoots)
            {
                string shortcutPath = Path.Combine(root, "IL2-SRS Client.lnk");
                if (!File.Exists(shortcutPath))
                {
                    continue;
                }

                try
                {
                    WshShell shell = new WshShell();
                    IWshShortcut shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                    AddInstall(installations, Path.GetDirectoryName(shortcut.TargetPath));
                }
                catch
                {
                }
            }
        }

        private static void AddProgramFilesCandidates(ISet<string> installations)
        {
            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                try
                {
                    foreach (string directory in Directory.GetDirectories(root))
                    {
                        string name = Path.GetFileName(directory);
                        if (name.IndexOf("SimpleRadio", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("IL2-SRS", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            AddInstall(installations, directory);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddGameFolderCandidates(ISet<string> installations)
        {
            foreach (Il2Install game in Il2InstallDiscovery.FindInstalledGames())
            {
                AddInstall(installations, game.InstallPath);

                DirectoryInfo parent = Directory.GetParent(game.InstallPath);
                if (parent != null)
                {
                    AddInstall(installations, parent.FullName);
                }

                AddChildInstallCandidates(installations, game.InstallPath);
            }
        }

        private static void AddChildInstallCandidates(ISet<string> installations, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return;
            }

            try
            {
                foreach (string directory in Directory.GetDirectories(root))
                {
                    AddInstall(installations, directory);
                }
            }
            catch
            {
            }
        }

        private static void AddInstall(ISet<string> installations, string path)
        {
            string normalized = NormalizeDirectory(path);
            if (!string.IsNullOrWhiteSpace(normalized)
                && File.Exists(Path.Combine(normalized, ClientExecutable)))
            {
                installations.Add(normalized);
            }
        }

        private static string ReadRegistryPath(string keyPath, string valueName)
        {
            try
            {
                return Convert.ToString(Registry.GetValue(keyPath, valueName, ""), CultureInfo.InvariantCulture);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SelectPrimarySource(IEnumerable<string> sources, string registeredPath)
        {
            string registered = sources.FirstOrDefault(source => PathsEqual(source, registeredPath)
                                                                 && File.Exists(Path.Combine(source, "global.cfg")));
            if (!string.IsNullOrWhiteSpace(registered))
            {
                return registered;
            }

            return sources
                .Where(source => File.Exists(Path.Combine(source, "global.cfg")))
                .OrderByDescending(source => File.GetLastWriteTimeUtc(Path.Combine(source, "global.cfg")))
                .FirstOrDefault();
        }

        private static IEnumerable<string> OrderSources(IEnumerable<string> sources, string primary)
        {
            if (!string.IsNullOrWhiteSpace(primary))
            {
                yield return primary;
            }

            foreach (string source in sources.Where(source => !PathsEqual(source, primary)))
            {
                yield return source;
            }
        }

        private static bool ContainsUserData(string source)
        {
            return EnumerateUserDataFiles(source).Any();
        }

        private static IEnumerable<string> EnumerateUserDataFiles(string source)
        {
            if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source))
            {
                return Enumerable.Empty<string>();
            }

            List<string> files = new List<string>();
            try
            {
                files.AddRange(Directory.GetFiles(source, "*.cfg", SearchOption.TopDirectoryOnly));
                files.AddRange(Directory.GetFiles(source, "*.txt", SearchOption.TopDirectoryOnly)
                    .Where(file => !IsLogOrDocumentationFile(Path.GetFileName(file))));
            }
            catch
            {
            }

            string favourites = Path.Combine(source, "FavouriteServers.csv");
            if (File.Exists(favourites))
            {
                files.Add(favourites);
            }

            return files.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static void BackupUserData(string source, string backupRoot, Action<string> log)
        {
            string destination = Path.Combine(backupRoot, SafeFolderName(source));
            Directory.CreateDirectory(destination);

            foreach (string sourceFile in EnumerateUserDataFiles(source))
            {
                string target = UniquePath(Path.Combine(destination, Path.GetFileName(sourceFile)));
                File.Copy(sourceFile, target, false);
            }

            Log(log, "Backed up SRS user data from " + source + " to " + destination);
        }

        private static void ImportProfiles(string source, ISet<string> targetProfiles, string userDataPath, Action<string> log)
        {
            HashSet<string> sourceProfiles = ReadProfileNames(Path.Combine(source, "global.cfg"));
            if (sourceProfiles.Count == 0 && File.Exists(Path.Combine(source, "default.cfg")))
            {
                sourceProfiles.Add("default");
            }

            foreach (string profile in sourceProfiles)
            {
                string sourceFile = Path.Combine(source, ProfileFileName(profile));
                if (!File.Exists(sourceFile))
                {
                    continue;
                }

                string targetName = ProfileFileName(profile);
                string targetFile = Path.Combine(userDataPath, targetName);
                string importedProfileName = Path.GetFileNameWithoutExtension(targetName);

                if (File.Exists(targetFile))
                {
                    if (FilesEqual(sourceFile, targetFile))
                    {
                        targetProfiles.Add(importedProfileName);
                        continue;
                    }

                    importedProfileName = UniqueProfileName(
                        importedProfileName + "-from-" + SafeShortName(source),
                        targetProfiles,
                        userDataPath);
                    targetFile = Path.Combine(userDataPath, importedProfileName + ".cfg");
                }

                File.Copy(sourceFile, targetFile, false);
                targetProfiles.Add(importedProfileName);
                Log(log, "Imported SRS profile " + importedProfileName + " from " + source);
            }
        }

        private static void ImportFavouriteServers(string source, string userDataPath, Action<string> log)
        {
            string sourceFile = Path.Combine(source, "FavouriteServers.csv");
            if (!File.Exists(sourceFile))
            {
                return;
            }

            string targetFile = Path.Combine(userDataPath, "FavouriteServers.csv");
            MergeUniqueLines(sourceFile, targetFile);
            Log(log, "Merged favourite servers from " + source);
        }

        private static void ImportTextConfiguration(string source, string userDataPath, Action<string> log)
        {
            foreach (string sourceFile in Directory.GetFiles(source, "*.txt", SearchOption.TopDirectoryOnly)
                         .Where(file => !IsLogOrDocumentationFile(Path.GetFileName(file))))
            {
                string fileName = Path.GetFileName(sourceFile);
                string targetFile = Path.Combine(userDataPath, fileName);

                if (string.Equals(fileName, "whitelist.txt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, "blacklist.txt", StringComparison.OrdinalIgnoreCase))
                {
                    MergeUniqueLines(sourceFile, targetFile);
                    continue;
                }

                if (!File.Exists(targetFile))
                {
                    File.Copy(sourceFile, targetFile, false);
                }
                else if (!FilesEqual(sourceFile, targetFile))
                {
                    string renamed = UniquePath(Path.Combine(
                        userDataPath,
                        Path.GetFileNameWithoutExtension(fileName) + "-from-" + SafeShortName(source) + Path.GetExtension(fileName)));
                    File.Copy(sourceFile, renamed, false);
                }
            }
        }

        private static void MergeUniqueLines(string sourceFile, string targetFile)
        {
            List<string> lines = new List<string>();
            if (File.Exists(targetFile))
            {
                lines.AddRange(File.ReadAllLines(targetFile));
            }

            HashSet<string> existing = new HashSet<string>(lines, StringComparer.OrdinalIgnoreCase);
            foreach (string line in File.ReadAllLines(sourceFile))
            {
                if (existing.Add(line))
                {
                    lines.Add(line);
                }
            }

            File.WriteAllLines(targetFile, lines, Encoding.UTF8);
        }

        private static HashSet<string> ReadProfileNames(string globalConfigPath)
        {
            HashSet<string> profiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(globalConfigPath))
            {
                return profiles;
            }

            try
            {
                foreach (string line in File.ReadAllLines(globalConfigPath))
                {
                    Match match = Regex.Match(line, @"^\s*SettingsProfiles\s*=\s*\{(?<profiles>.*?)\}\s*$", RegexOptions.IgnoreCase);
                    if (!match.Success)
                    {
                        continue;
                    }

                    foreach (string value in match.Groups["profiles"].Value.Split(','))
                    {
                        string profile = Path.GetFileNameWithoutExtension(value.Trim());
                        if (!string.IsNullOrWhiteSpace(profile))
                        {
                            profiles.Add(profile);
                        }
                    }
                }
            }
            catch
            {
            }

            return profiles;
        }

        private static void EnsureGlobalProfileList(ISet<string> profiles, string userDataPath)
        {
            if (profiles.Count == 0)
            {
                profiles.Add("default");
            }

            string globalPath = Path.Combine(userDataPath, "global.cfg");
            List<string> lines = File.Exists(globalPath)
                ? File.ReadAllLines(globalPath).ToList()
                : new List<string> { "[Client Settings]" };

            string value = "SettingsProfiles={" + string.Join(",", profiles.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)) + "}";
            int settingIndex = lines.FindIndex(line => Regex.IsMatch(line, @"^\s*SettingsProfiles\s*=", RegexOptions.IgnoreCase));
            if (settingIndex >= 0)
            {
                lines[settingIndex] = value;
            }
            else
            {
                int clientSection = lines.FindIndex(line => string.Equals(line.Trim(), "[Client Settings]", StringComparison.OrdinalIgnoreCase));
                if (clientSection < 0)
                {
                    lines.Add(string.Empty);
                    lines.Add("[Client Settings]");
                    clientSection = lines.Count - 1;
                }

                lines.Insert(clientSection + 1, value);
            }

            File.WriteAllLines(globalPath, lines, Encoding.UTF8);
        }

        private static string ProfileFileName(string profile)
        {
            return profile.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase)
                ? profile
                : profile + ".cfg";
        }

        private static string UniqueProfileName(string desired, ISet<string> existing, string userDataPath)
        {
            string candidate = desired;
            int suffix = 2;
            while (existing.Contains(candidate) || File.Exists(Path.Combine(userDataPath, candidate + ".cfg")))
            {
                candidate = desired + "-" + suffix;
                suffix++;
            }

            return candidate;
        }

        private static void DeleteFile(string path, Action<string> log)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Log(log, "Unable to remove duplicate file " + path + ": " + ex.Message);
            }
        }

        private static void DeleteKnownDirectory(string path, Action<string> log)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (Exception ex)
            {
                Log(log, "Unable to remove duplicate directory " + path + ": " + ex.Message);
            }
        }

        private static bool IsLogOrDocumentationFile(string fileName)
        {
            string lower = (fileName ?? string.Empty).ToLowerInvariant();
            return lower.Contains("log")
                   || lower == "readme.txt"
                   || lower == "license.txt"
                   || lower == "translating.txt";
        }

        private static bool FilesEqual(string left, string right)
        {
            FileInfo leftInfo = new FileInfo(left);
            FileInfo rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }

            byte[] leftBytes = File.ReadAllBytes(left);
            byte[] rightBytes = File.ReadAllBytes(right);
            return leftBytes.SequenceEqual(rightBytes);
        }

        private static string SafeShortName(string path)
        {
            string name = Path.GetFileName(NormalizeDirectory(path));
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "legacy";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return name;
        }

        private static string SafeFolderName(string path)
        {
            string value = NormalizeDirectory(path)
                .Replace(':', '_')
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_');

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(value) ? "legacy-install" : value;
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path))
            {
                return path;
            }

            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            int suffix = 2;
            while (true)
            {
                string candidate = Path.Combine(directory, fileName + "-" + suffix + extension);
                if (!File.Exists(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private static string NormalizeDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsSameOrChildPath(string candidate, string parent)
        {
            if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(parent))
            {
                return false;
            }

            return string.Equals(candidate, parent, StringComparison.OrdinalIgnoreCase)
                   || candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private static void Log(Action<string> log, string message)
        {
            if (log != null)
            {
                log(message);
            }
        }
    }
}
