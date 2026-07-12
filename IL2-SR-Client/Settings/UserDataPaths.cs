using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    public static class UserDataPaths
    {
        public const string DataFolderName = "IL2-SRS";
        private const string MigrationMarkerFileName = ".legacy-migration-complete";
        private static readonly string[] MigratedNamedFiles =
        {
            "FavouriteServers.csv",
            "whitelist.txt",
            "blacklist.txt"
        };

        private static readonly Lazy<string> ConfigDirectoryValue =
            new Lazy<string>(ResolveConfigDirectory);

        public static string ConfigDirectory
        {
            get { return ConfigDirectoryValue.Value; }
        }

        public static string GetPath(string fileName)
        {
            return Path.Combine(ConfigDirectory, fileName);
        }

        public static void EnsureDirectory()
        {
            Directory.CreateDirectory(ConfigDirectory);
        }

        public static void MigrateLegacyUserData(Action<string> log)
        {
            MigrateLegacyUserDataTo(
                ConfigDirectory,
                GetLegacySourceDirectories(),
                log);
        }

        internal static void MigrateLegacyUserDataTo(
            string configDirectory,
            IEnumerable<string> legacySources,
            Action<string> log)
        {
            Directory.CreateDirectory(configDirectory);

            string markerPath = Path.Combine(configDirectory, MigrationMarkerFileName);
            if (File.Exists(markerPath))
            {
                return;
            }

            List<string> sources = (legacySources ?? Enumerable.Empty<string>())
                .Where(source => !PathsEqual(source, configDirectory))
                .Where(Directory.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (sources.Count == 0)
            {
                File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                return;
            }

            string backupRoot = null;
            foreach (string source in sources)
            {
                foreach (string sourceFile in EnumerateUserDataFiles(source))
                {
                    string destination = Path.Combine(configDirectory, Path.GetFileName(sourceFile));
                    try
                    {
                        if (!File.Exists(destination))
                        {
                            File.Copy(sourceFile, destination, false);
                            Log(log, "Migrated user configuration " + sourceFile + " to " + destination);
                            continue;
                        }

                        if (FilesEqual(sourceFile, destination))
                        {
                            continue;
                        }

                        if (backupRoot == null)
                        {
                            backupRoot = Path.Combine(
                                configDirectory,
                                "MigrationBackups",
                                DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
                        }

                        string sourceBackup = Path.Combine(backupRoot, SafeFolderName(source));
                        Directory.CreateDirectory(sourceBackup);
                        string backupPath = UniquePath(Path.Combine(sourceBackup, Path.GetFileName(sourceFile)));
                        File.Copy(sourceFile, backupPath, false);
                        Log(log, "Preserved conflicting legacy configuration at " + backupPath);
                    }
                    catch (Exception ex)
                    {
                        Log(log, "Unable to migrate " + sourceFile + ": " + ex.Message);
                    }
                }
            }

            File.WriteAllText(markerPath, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        }
        public static IEnumerable<string> GetLegacySourceDirectories()
        {
            List<string> sources = new List<string>();
            AddDirectory(sources, AppDomain.CurrentDomain.BaseDirectory);
            AddDirectory(sources, Environment.CurrentDirectory);

            try
            {
                AddDirectory(sources, Convert.ToString(
                    Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\IL2-SRS", "SRSPath", ""),
                    CultureInfo.InvariantCulture));
            }
            catch
            {
            }

            AddDirectory(sources, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "IL2-SimpleRadio-Standalone"));
            AddDirectory(sources, Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "IL2-SimpleRadio-Standalone"));

            return sources;
        }

        private static string ResolveConfigDirectory()
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (!argument.Trim().StartsWith("-cfg=", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string configured = argument.Trim().Substring(5).Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured));
                }
            }

            string environmentOverride = Environment.GetEnvironmentVariable("IL2_SRS_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(environmentOverride))
            {
                return Path.GetFullPath(Environment.ExpandEnvironmentVariables(environmentOverride.Trim().Trim((char)34)));
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                DataFolderName);
        }

        private static IEnumerable<string> EnumerateUserDataFiles(string source)
        {
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (string file in Directory.GetFiles(source, "*.cfg", SearchOption.TopDirectoryOnly))
                {
                    files.Add(file);
                }
            }
            catch
            {
            }

            try
            {
                foreach (string file in Directory.GetFiles(source, "*.txt", SearchOption.TopDirectoryOnly))
                {
                    string name = Path.GetFileName(file);
                    if (!IsLogOrDocumentationFile(name))
                    {
                        files.Add(file);
                    }
                }
            }
            catch
            {
            }

            foreach (string fileName in MigratedNamedFiles)
            {
                string file = Path.Combine(source, fileName);
                if (File.Exists(file))
                {
                    files.Add(file);
                }
            }

            return files;
        }

        private static bool IsLogOrDocumentationFile(string fileName)
        {
            string lower = (fileName ?? string.Empty).ToLowerInvariant();
            return lower.Contains("log")
                   || lower == "readme.txt"
                   || lower == "license.txt"
                   || lower == "translating.txt";
        }

        private static void AddDirectory(ICollection<string> directories, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                string fullPath = Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (!directories.Contains(fullPath, StringComparer.OrdinalIgnoreCase))
                {
                    directories.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool FilesEqual(string left, string right)
        {
            FileInfo leftInfo = new FileInfo(left);
            FileInfo rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }

            const int bufferSize = 81920;
            byte[] leftBuffer = new byte[bufferSize];
            byte[] rightBuffer = new byte[bufferSize];

            using (FileStream leftStream = File.OpenRead(left))
            using (FileStream rightStream = File.OpenRead(right))
            {
                int read;
                while ((read = leftStream.Read(leftBuffer, 0, leftBuffer.Length)) > 0)
                {
                    int rightRead = rightStream.Read(rightBuffer, 0, rightBuffer.Length);
                    if (rightRead != read)
                    {
                        return false;
                    }

                    for (int i = 0; i < read; i++)
                    {
                        if (leftBuffer[i] != rightBuffer[i])
                        {
                            return false;
                        }
                    }
                }

                return rightStream.ReadByte() == -1;
            }
        }

        private static string SafeFolderName(string path)
        {
            string name = path.Replace(':', '_')
                .Replace(Path.DirectorySeparatorChar, '_')
                .Replace(Path.AltDirectorySeparatorChar, '_');

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(name) ? "legacy-install" : name;
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

        private static void Log(Action<string> log, string message)
        {
            if (log != null)
            {
                log(message);
            }
        }
    }
}
