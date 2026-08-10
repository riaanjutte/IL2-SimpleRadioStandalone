using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using Microsoft.Win32;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics
{
    internal sealed class TelemetryDiagnosticsService
    {
        private const string SrsAddress = "127.0.0.1";
        private const int DefaultSrsPort = 4322;
        private const string GreatBattlesDisplayName = "IL-2 Sturmovik Great Battles";
        private const string KoreaDisplayName = "IL-2 Korea";
        private static readonly string[] IL2ProcessNames = { "Il-2", "IL2Series" };
        private static readonly string[] KnownSteamFolderNames =
        {
            "IL-2 Sturmovik Battle of Stalingrad",
            "IL-2 Sturmovik Great Battles",
            "IL-2 Sturmovik Korea",
            "IL-2 Korea"
        };

        private readonly IList<ITelemetryDiagnosticProvider> _providers;

        private TelemetryDiagnosticsService(IEnumerable<ITelemetryDiagnosticProvider> providers)
        {
            _providers = providers.ToList();
        }

        public static TelemetryDiagnosticsService CreateDefault()
        {
            return new TelemetryDiagnosticsService(new ITelemetryDiagnosticProvider[]
            {
                new IL2WinWingTelemetryDiagnosticProvider()
            });
        }

        public string BuildReportText()
        {
            List<TelemetryDiagnosticContext> contexts = BuildContexts();
            Dictionary<string, TelemetryRepairResult> repairResults = RepairStartupConfigs(contexts);
            contexts = BuildContexts();
            if (contexts.Count == 0)
            {
                contexts.Add(new TelemetryDiagnosticContext(
                    "IL-2 install",
                    "Not detected",
                    SrsAddress,
                    ReadSrsTelemetryPort(),
                    string.Empty,
                    string.Empty,
                    null));
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Telemetry diagnostics");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            builder.AppendLine("SRS telemetry endpoint: " + SrsAddress + ":" + ReadSrsTelemetryPort());
            builder.AppendLine("Detected IL-2 installs: " + (contexts.Any(context => !string.IsNullOrWhiteSpace(context.StartupConfigPath)) ? contexts.Count.ToString(CultureInfo.InvariantCulture) : "none"));

            TelemetryDiagnosticReport environmentReport = new TelemetryDiagnosticReport();
            environmentReport.AddRange(
                WindowsFirewallTelemetryDiagnostic.DiagnoseCurrentProcess(ReadSrsTelemetryPort()));
            builder.AppendLine();
            builder.AppendLine("SRS environment");
            builder.AppendLine("---------------");
            builder.AppendLine(environmentReport.ToDisplayText());

            foreach (TelemetryDiagnosticContext context in contexts)
            {
                TelemetryDiagnosticReport report = new TelemetryDiagnosticReport();

                report.Add(TelemetryDiagnosticSeverity.Info,
                    "IL-2 startup.cfg",
                    string.IsNullOrWhiteSpace(context.StartupConfigPath)
                        ? "No IL-2 install with data\\startup.cfg was detected."
                        : context.StartupConfigPath);

                TelemetryRepairResult repairResult;
                if (!string.IsNullOrWhiteSpace(context.StartupConfigPath)
                    && repairResults.TryGetValue(context.StartupConfigPath, out repairResult))
                {
                    report.Add(repairResult.Error == null && !repairResult.Deferred
                            ? TelemetryDiagnosticSeverity.Ok
                            : TelemetryDiagnosticSeverity.Warning,
                        repairResult.Deferred
                            ? "SRS telemetry auto-repair deferred"
                            : (repairResult.Error == null ? "SRS telemetry auto-repair" : "SRS telemetry auto-repair failed"),
                        repairResult.Deferred
                            ? "IL-2 is running. To protect your game settings, startup.cfg was not changed. Close IL-2 and run Telemetry Diagnostics again."
                            : (repairResult.Error == null
                            ? (repairResult.Changed
                                ? "startup.cfg was repaired and verified for 127.0.0.1:4322."
                                : "startup.cfg was already correctly configured and was verified.")
                            : repairResult.Error.Message));
                }

                if (context.StartupConfig == null)
                {
                    report.Add(TelemetryDiagnosticSeverity.Warning,
                        "IL-2 telemetry config not found",
                        "SRS could not read data\\startup.cfg, so third-party telemetry ports could not be compared against it.");
                }
                else
                {
                    AddStartupConfigSummary(report, context);
                }

                foreach (ITelemetryDiagnosticProvider provider in _providers)
                {
                    report.AddRange(provider.Diagnose(context));
                }

                builder.AppendLine();
                builder.AppendLine(context.DisplayName);
                builder.AppendLine(new string('-', Math.Min(72, Math.Max(12, context.DisplayName.Length))));
                builder.AppendLine("Detected from: " + context.DetectionSource);
                builder.AppendLine(report.ToDisplayText());
            }

            return builder.ToString().TrimEnd();
        }

        private static Dictionary<string, TelemetryRepairResult> RepairStartupConfigs(IEnumerable<TelemetryDiagnosticContext> contexts)
        {
            Dictionary<string, TelemetryRepairResult> results =
                new Dictionary<string, TelemetryRepairResult>(StringComparer.OrdinalIgnoreCase);

            foreach (TelemetryDiagnosticContext context in contexts
                         .Where(context => !string.IsNullOrWhiteSpace(context.StartupConfigPath))
                         .GroupBy(context => context.StartupConfigPath, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First()))
            {
                try
                {
                    if (IsIL2Running() && !StartupConfigTelemetry.IsEnabled(context.StartupConfigPath))
                    {
                        results[context.StartupConfigPath] = new TelemetryRepairResult(false, null, true);
                        continue;
                    }

                    bool changed = StartupConfigTelemetry.EnsureEnabled(
                        context.StartupConfigPath,
                        null,
                        () => !IsIL2Running());
                    results[context.StartupConfigPath] = new TelemetryRepairResult(changed, null);
                }
                catch (InvalidOperationException)
                {
                    results[context.StartupConfigPath] = new TelemetryRepairResult(false, null, true);
                }
                catch (Exception ex)
                {
                    results[context.StartupConfigPath] = new TelemetryRepairResult(false, ex);
                }
            }

            return results;
        }

        internal static bool IsIL2Running()
        {
            foreach (string processName in IL2ProcessNames)
            {
                try
                {
                    if (Process.GetProcessesByName(processName).Length > 0)
                    {
                        return true;
                    }
                }
                catch
                {
                    // A failed process query must not permit a potentially unsafe config write.
                    return true;
                }
            }

            return false;
        }

        internal static List<TelemetryDiagnosticContext> BuildContexts()
        {
            int srsPort = ReadSrsTelemetryPort();
            List<Il2InstallCandidate> candidates = DiscoverIl2InstallCandidates();
            List<TelemetryDiagnosticContext> contexts = new List<TelemetryDiagnosticContext>();

            foreach (Il2InstallCandidate candidate in candidates)
            {
                Il2TelemetryConfiguration startupConfig = null;
                if (!string.IsNullOrWhiteSpace(candidate.StartupConfigPath) && File.Exists(candidate.StartupConfigPath))
                {
                    try
                    {
                        startupConfig = Il2TelemetryConfigurationParser.Parse(candidate.StartupConfigPath);
                    }
                    catch
                    {
                        // Keep the install in the report so repair can still be attempted and any failure shown.
                        startupConfig = null;
                    }
                }

                contexts.Add(new TelemetryDiagnosticContext(
                    candidate.DisplayName,
                    candidate.DetectionSource,
                    SrsAddress,
                    srsPort,
                    candidate.InstallPath,
                    candidate.StartupConfigPath,
                    startupConfig));
            }

            return contexts
                .OrderBy(context => GetGameSortOrder(context.DisplayName))
                .ThenBy(context => context.Il2InstallPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<Il2InstallCandidate> DiscoverIl2InstallCandidates()
        {
            Dictionary<string, Il2InstallCandidate> candidates = new Dictionary<string, Il2InstallCandidate>(StringComparer.OrdinalIgnoreCase);

            string savedIl2Path = ReadInstallerPath("IL2Path");
            AddCandidate(candidates, "Saved IL-2 installer path", "IL2-SRS installer registry", savedIl2Path);
            AddSiblingInstallCandidates(candidates, savedIl2Path, "near saved IL-2 installer path");
            AddSavedInstallCandidate(candidates, "IL2GreatBattlesPath", GreatBattlesDisplayName);
            AddSavedInstallCandidate(candidates, "IL2KoreaPath", KoreaDisplayName);
            AddRunningProcessCandidates(candidates);
            AddUninstallRegistryCandidates(candidates);
            AddSteamCandidates(candidates);
            AddCommonFolderCandidates(candidates);

            return candidates.Values.ToList();
        }

        private static void AddSavedInstallCandidate(Dictionary<string, Il2InstallCandidate> candidates, string registryValue, string displayName)
        {
            string path = ReadInstallerPath(registryValue);
            AddCandidate(candidates, displayName, "IL2-SRS installer registry", path);
            AddSiblingInstallCandidates(candidates, path, "near saved " + displayName + " path");
        }

        private static void AddRunningProcessCandidates(Dictionary<string, Il2InstallCandidate> candidates)
        {
            foreach (string processName in IL2ProcessNames)
            {
                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    string processPath = SafeMainModulePath(process);
                    if (!string.IsNullOrWhiteSpace(processPath))
                    {
                        AddCandidate(candidates,
                            InferDisplayName(processPath, "Running IL-2 process"),
                            "running process " + process.ProcessName,
                            processPath);
                        AddSiblingInstallCandidates(candidates, processPath, "near running process " + process.ProcessName);
                    }
                }
            }
        }

        private static void AddUninstallRegistryCandidates(Dictionary<string, Il2InstallCandidate> candidates)
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
                                    if (!LooksLikeIl2Install(displayName))
                                    {
                                        continue;
                                    }

                                    AddCandidate(candidates, InferDisplayName(displayName, displayName), "Windows uninstall registry", Convert.ToString(appKey.GetValue("InstallLocation"), CultureInfo.InvariantCulture));
                                    AddCandidate(candidates, InferDisplayName(displayName, displayName), "Windows uninstall registry", ExtractPathFromRegistryValue(Convert.ToString(appKey.GetValue("DisplayIcon"), CultureInfo.InvariantCulture)));
                                    AddCandidate(candidates, InferDisplayName(displayName, displayName), "Windows uninstall registry", ExtractPathFromRegistryValue(Convert.ToString(appKey.GetValue("UninstallString"), CultureInfo.InvariantCulture)));
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

        private static void AddSteamCandidates(Dictionary<string, Il2InstallCandidate> candidates)
        {
            foreach (string steamRoot in GetSteamLibraryRoots())
            {
                string commonPath = Path.Combine(steamRoot, "steamapps", "common");
                if (!Directory.Exists(commonPath))
                {
                    continue;
                }

                foreach (string folderName in KnownSteamFolderNames)
                {
                    AddCandidate(candidates, InferDisplayName(folderName, folderName), "Steam library", Path.Combine(commonPath, folderName));
                }

                try
                {
                    foreach (string directory in Directory.GetDirectories(commonPath))
                    {
                        string name = Path.GetFileName(directory);
                        if (LooksLikeIl2Install(name))
                        {
                            AddCandidate(candidates, InferDisplayName(directory, "Steam IL-2 install"), "Steam library", directory);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddCommonFolderCandidates(Dictionary<string, Il2InstallCandidate> candidates)
        {
            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "1C Game Studios"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "1C Game Studios")
            };

            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                {
                    continue;
                }

                foreach (string folderName in KnownSteamFolderNames)
                {
                    AddCandidate(candidates, InferDisplayName(folderName, folderName), "common install location", Path.Combine(root, folderName));
                }

                try
                {
                    foreach (string directory in Directory.GetDirectories(root))
                    {
                        string name = Path.GetFileName(directory);
                        if (LooksLikeIl2Install(name))
                        {
                            AddCandidate(candidates, InferDisplayName(directory, "Common IL-2 install"), "common install location", directory);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddSiblingInstallCandidates(Dictionary<string, Il2InstallCandidate> candidates, string path, string source)
        {
            string root = FindValidIl2Root(path);
            if (string.IsNullOrWhiteSpace(root))
            {
                return;
            }

            AddSiblingInstallCandidatesFromRoot(candidates, root, source);

            try
            {
                DirectoryInfo parent = Directory.GetParent(root);
                if (parent != null)
                {
                    AddSiblingInstallCandidatesFromRoot(candidates, parent.FullName, source);
                }
            }
            catch
            {
            }
        }

        private static void AddSiblingInstallCandidatesFromRoot(Dictionary<string, Il2InstallCandidate> candidates, string root, string source)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return;
            }

            foreach (string folderName in KnownSteamFolderNames)
            {
                AddCandidate(candidates, InferDisplayName(folderName, folderName), source, Path.Combine(root, folderName));
            }

            try
            {
                foreach (string directory in Directory.GetDirectories(root))
                {
                    string name = Path.GetFileName(directory);
                    if (LooksLikeIl2Install(name))
                    {
                        AddCandidate(candidates, InferDisplayName(directory, "Nearby IL-2 install"), source, directory);
                    }
                }
            }
            catch
            {
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

        private static void AddSteamRoot(HashSet<string> roots, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                roots.Add(Path.GetFullPath(path));
            }
        }

        private static void AddCandidate(Dictionary<string, Il2InstallCandidate> candidates, string displayName, string source, string path)
        {
            string installPath = FindValidIl2Root(path);
            if (string.IsNullOrWhiteSpace(installPath))
            {
                return;
            }

            string startupConfigPath = Path.Combine(installPath, "data", "startup.cfg");
            string key = Path.GetFullPath(startupConfigPath);
            Il2InstallCandidate existing;
            if (candidates.TryGetValue(key, out existing))
            {
                existing.AddSource(source);
                if (IsMoreSpecificDisplayName(displayName, existing.DisplayName))
                {
                    existing.DisplayName = InferDisplayName(installPath + " " + displayName, displayName);
                }
                return;
            }

            candidates[key] = new Il2InstallCandidate(
                InferDisplayName(installPath + " " + displayName, displayName),
                installPath,
                startupConfigPath,
                source);
        }

        private static string FindValidIl2Root(string path)
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
                    current = Directory.GetParent(current)?.FullName;
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
                        && Directory.Exists(Path.Combine(candidate, "data"))
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

        private static void AddPathCandidate(List<string> candidates, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !candidates.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(path);
            }
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        }

        private static string ExtractPathFromRegistryValue(string value)
        {
            value = NormalizePath(value);
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                int closingQuote = value.IndexOf('"', 1);
                if (closingQuote > 1)
                {
                    return value.Substring(1, closingQuote - 1);
                }
            }

            int exeIndex = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exeIndex >= 0)
            {
                return value.Substring(0, exeIndex + 4);
            }

            int firstArgument = value.IndexOf(" -", StringComparison.Ordinal);
            return firstArgument > 0 ? value.Substring(0, firstArgument) : value;
        }

        private static bool LooksLikeIl2Install(string value)
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

        private static string InferDisplayName(string value, string fallback)
        {
            string lower = (value ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("korea"))
            {
                return KoreaDisplayName;
            }

            if (lower.Contains("great battles")
                || lower.Contains("battle of stalingrad")
                || lower.Contains("il-2 sturmovik"))
            {
                return GreatBattlesDisplayName;
            }

            return string.IsNullOrWhiteSpace(fallback) ? "IL-2 install" : fallback;
        }

        private static bool IsMoreSpecificDisplayName(string candidate, string existing)
        {
            return (string.Equals(candidate, GreatBattlesDisplayName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(candidate, KoreaDisplayName, StringComparison.OrdinalIgnoreCase))
                   && !string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetGameSortOrder(string displayName)
        {
            if (string.Equals(displayName, GreatBattlesDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (string.Equals(displayName, KoreaDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            return 2;
        }

        private static string SafeMainModulePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddStartupConfigSummary(TelemetryDiagnosticReport report, TelemetryDiagnosticContext context)
        {
            Il2TelemetryConfiguration config = context.StartupConfig;

            if (!config.HasTelemetrySection)
            {
                report.Add(TelemetryDiagnosticSeverity.Warning,
                    "IL-2 telemetry section missing",
                    "startup.cfg does not contain a [KEY = telemetrydevice] section.");
                return;
            }

            if (config.Enabled == true)
            {
                report.Add(TelemetryDiagnosticSeverity.Ok,
                    "IL-2 telemetry enabled",
                    "startup.cfg has telemetrydevice enable = true.");
            }
            else
            {
                report.Add(TelemetryDiagnosticSeverity.Warning,
                    "IL-2 telemetry disabled",
                    "startup.cfg has telemetrydevice enable set to false or missing.");
            }

            if (config.ContainsEndpoint(context.SrsAddress, context.SrsPort))
            {
                report.Add(TelemetryDiagnosticSeverity.Ok,
                    "SRS telemetry endpoint present",
                    context.SrsAddress + ":" + context.SrsPort + " is configured in startup.cfg.");
            }
            else
            {
                report.Add(TelemetryDiagnosticSeverity.Warning,
                    "SRS telemetry endpoint missing",
                    "startup.cfg does not currently contain " + context.SrsAddress + ":" + context.SrsPort + ".");
            }

            if (config.Endpoints.Count > 0)
            {
                report.Add(TelemetryDiagnosticSeverity.Info,
                    "Configured telemetry endpoints",
                    string.Join(", ", config.Endpoints.Select(endpoint => endpoint.ToDisplayText()).ToArray()));
            }
        }

        private static int ReadSrsTelemetryPort()
        {
            int port;
            string configuredPort = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.IL2IncomingUDP).RawValue;
            if (int.TryParse(configuredPort, NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                && port > 0
                && port <= 65535)
            {
                return port;
            }

            return DefaultSrsPort;
        }

        private static string ReadInstallerPath(string key)
        {
            try
            {
                return (string)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\IL2-SRS", key, "");
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    internal sealed class TelemetryRepairResult
    {
        public TelemetryRepairResult(bool changed, Exception error, bool deferred = false)
        {
            Changed = changed;
            Error = error;
            Deferred = deferred;
        }

        public bool Changed { get; private set; }
        public Exception Error { get; private set; }
        public bool Deferred { get; private set; }
    }

    internal interface ITelemetryDiagnosticProvider
    {
        IEnumerable<TelemetryDiagnosticItem> Diagnose(TelemetryDiagnosticContext context);
    }

    internal sealed class TelemetryDiagnosticContext
    {
        public TelemetryDiagnosticContext(
            string displayName,
            string detectionSource,
            string srsAddress,
            int srsPort,
            string il2InstallPath,
            string startupConfigPath,
            Il2TelemetryConfiguration startupConfig)
        {
            DisplayName = displayName;
            DetectionSource = detectionSource;
            SrsAddress = srsAddress;
            SrsPort = srsPort;
            Il2InstallPath = il2InstallPath;
            StartupConfigPath = startupConfigPath;
            StartupConfig = startupConfig;
        }

        public string DisplayName { get; private set; }
        public string DetectionSource { get; private set; }
        public string SrsAddress { get; private set; }
        public int SrsPort { get; private set; }
        public string Il2InstallPath { get; private set; }
        public string StartupConfigPath { get; private set; }
        public Il2TelemetryConfiguration StartupConfig { get; private set; }
    }

    internal sealed class Il2InstallCandidate
    {
        private readonly List<string> _sources = new List<string>();

        public Il2InstallCandidate(string displayName, string installPath, string startupConfigPath, string source)
        {
            DisplayName = displayName;
            InstallPath = installPath;
            StartupConfigPath = startupConfigPath;
            AddSource(source);
        }

        public string DisplayName { get; set; }
        public string InstallPath { get; private set; }
        public string StartupConfigPath { get; private set; }

        public string DetectionSource
        {
            get { return string.Join(", ", _sources.ToArray()); }
        }

        public void AddSource(string source)
        {
            if (!string.IsNullOrWhiteSpace(source) && !_sources.Contains(source, StringComparer.OrdinalIgnoreCase))
            {
                _sources.Add(source);
            }
        }
    }

    internal sealed class IL2WinWingTelemetryDiagnosticProvider : ITelemetryDiagnosticProvider
    {
        internal const string ProcessName = "IL2WinWing";
        internal const string ConfigFileName = "IL2WinWing.dll.config";
        internal const int PreferredTelemetryPort = 29373;
        internal const int DefaultWinWingPort = 16536;
        private const int SearchDirectoryLimit = 2500;
        private const int SearchMatchLimit = 6;

        public IEnumerable<TelemetryDiagnosticItem> Diagnose(TelemetryDiagnosticContext context)
        {
            List<TelemetryDiagnosticItem> items = new List<TelemetryDiagnosticItem>();
            IL2WinWingConfigSearchResult searchResult = FindConfigFiles(context);

            if (searchResult.ProcessRunning && searchResult.ConfigPaths.Count == 0)
            {
                items.Add(new TelemetryDiagnosticItem(
                    TelemetryDiagnosticSeverity.Warning,
                    "IL2WinWing running",
                    "IL2WinWing is running, but its " + ConfigFileName + " file could not be found next to the process or in common install locations."));
            }

            if (searchResult.ConfigPaths.Count == 0)
            {
                string suffix = searchResult.SearchLimitReached
                    ? " The search stopped after checking " + SearchDirectoryLimit + " folders."
                    : string.Empty;
                items.Add(new TelemetryDiagnosticItem(
                    TelemetryDiagnosticSeverity.Info,
                    "IL2WinWing not detected",
                    "No " + ConfigFileName + " file was found in common install locations." + suffix));
                return items;
            }

            foreach (string configPath in searchResult.ConfigPaths)
            {
                int? telemetryPort = ReadPort(configPath, "IL2TelemetryPort");
                int? winWingPort = ReadPort(configPath, "WWPort");
                if (!telemetryPort.HasValue)
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "IL2WinWing config unreadable",
                        configPath + " was found, but IL2TelemetryPort could not be read."));
                    continue;
                }

                if (telemetryPort.Value == context.SrsPort)
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "IL2WinWing port conflicts with SRS",
                        "IL2WinWing is configured for IL-2 telemetry port " + telemetryPort.Value + ", which is also the SRS telemetry port. Select Configure IL2WinWing to assign separate ports safely."));
                }
                else if (context.StartupConfig != null && !context.StartupConfig.ContainsEndpoint(context.SrsAddress, telemetryPort.Value))
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "IL2WinWing port missing from startup.cfg",
                        "IL2WinWing uses IL-2 telemetry port " + telemetryPort.Value + ", but startup.cfg does not contain " + context.SrsAddress + ":" + telemetryPort.Value + ". Select Configure IL2WinWing to repair it."));
                }
                else
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Ok,
                        "IL2WinWing telemetry port",
                        "IL2WinWing uses IL-2 telemetry port " + telemetryPort.Value + " at " + configPath + "."));
                }

                if (!winWingPort.HasValue)
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "IL2WinWing SimApp Pro port unreadable",
                        configPath + " was found, but WWPort could not be read."));
                }
                else if (winWingPort.Value != DefaultWinWingPort)
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "IL2WinWing SimApp Pro port differs from the default",
                        "IL2WinWing WWPort is " + winWingPort.Value + ". The standard SimApp Pro port is " + DefaultWinWingPort + "."));
                }
                else
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Ok,
                        "IL2WinWing SimApp Pro port",
                        "IL2WinWing forwards vibration data to SimApp Pro on port " + winWingPort.Value + "."));
                }
            }

            if (searchResult.SearchLimitReached)
            {
                items.Add(new TelemetryDiagnosticItem(
                    TelemetryDiagnosticSeverity.Info,
                    "IL2WinWing search limit reached",
                    "The search stopped after checking " + SearchDirectoryLimit + " folders. If IL2WinWing is installed elsewhere, check " + ConfigFileName + " manually."));
            }

            return items;
        }

        internal static int? ReadPort(string configPath, string settingName)
        {
            try
            {
                XDocument document = XDocument.Load(configPath);
                XElement setting = document.Descendants("setting")
                    .FirstOrDefault(element =>
                        string.Equals((string)element.Attribute("name"), settingName, StringComparison.OrdinalIgnoreCase));
                string value = setting?.Element("value")?.Value;
                int port;
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                    && port > 0
                    && port <= 65535)
                {
                    return port;
                }
            }
            catch
            {
            }

            return null;
        }

        internal static IL2WinWingConfigSearchResult FindConfigFiles(TelemetryDiagnosticContext context)
        {
            IL2WinWingConfigSearchResult result = new IL2WinWingConfigSearchResult();
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Process process in Process.GetProcessesByName(ProcessName))
            {
                result.ProcessRunning = true;
                string processPath = SafeMainModulePath(process);
                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    string configPath = Path.Combine(Path.GetDirectoryName(processPath), ConfigFileName);
                    if (File.Exists(configPath))
                    {
                        paths.Add(configPath);
                    }
                }
            }

            AddRoot(roots, AppDomain.CurrentDomain.BaseDirectory);
            AddRoot(roots, context.Il2InstallPath);
            AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddRoot(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
            AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            AddRoot(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

            foreach (string root in roots)
            {
                if (paths.Count >= SearchMatchLimit)
                {
                    break;
                }

                FindConfigFiles(root, paths, result);
            }

            result.ConfigPaths.AddRange(paths.Take(SearchMatchLimit));
            return result;
        }

        private static void FindConfigFiles(string root, HashSet<string> paths, IL2WinWingConfigSearchResult result)
        {
            Stack<string> pending = new Stack<string>();
            pending.Push(root);

            while (pending.Count > 0 && paths.Count < SearchMatchLimit)
            {
                if (result.VisitedDirectories >= SearchDirectoryLimit)
                {
                    result.SearchLimitReached = true;
                    return;
                }

                string current = pending.Pop();
                result.VisitedDirectories++;

                try
                {
                    string candidate = Path.Combine(current, ConfigFileName);
                    if (File.Exists(candidate))
                    {
                        paths.Add(candidate);
                    }

                    foreach (string directory in Directory.GetDirectories(current))
                    {
                        if (!ShouldSkipDirectory(directory))
                        {
                            pending.Push(directory);
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (IOException)
                {
                }
                catch (ArgumentException)
                {
                }
                catch (NotSupportedException)
                {
                }
            }
        }

        private static bool ShouldSkipDirectory(string directory)
        {
            string name = Path.GetFileName(directory);
            return name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)
                   || name.Equals("Windows", StringComparison.OrdinalIgnoreCase);
        }

        private static string SafeMainModulePath(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void AddRoot(HashSet<string> roots, string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            {
                roots.Add(Path.GetFullPath(path));
            }
        }
    }

    internal static class IL2WinWingCompatibilityRepair
    {
        internal static IL2WinWingCompatibilityRepairResult Repair(
            IEnumerable<TelemetryDiagnosticContext> contexts,
            Func<bool> writeAllowed,
            Action<string> log)
        {
            List<TelemetryDiagnosticContext> contextList = contexts == null
                ? new List<TelemetryDiagnosticContext>()
                : contexts.ToList();

            if (contextList.Count == 0)
            {
                contextList.Add(new TelemetryDiagnosticContext(
                    "IL-2 install",
                    "Not detected",
                    "127.0.0.1",
                    4322,
                    string.Empty,
                    string.Empty,
                    null));
            }

            HashSet<string> configPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> runningConfigPaths = FindRunningConfigPaths();
            foreach (string path in runningConfigPaths)
            {
                configPaths.Add(path);
            }

            foreach (TelemetryDiagnosticContext context in contextList)
            {
                IL2WinWingConfigSearchResult searchResult =
                    IL2WinWingTelemetryDiagnosticProvider.FindConfigFiles(context);
                foreach (string path in searchResult.ConfigPaths)
                {
                    configPaths.Add(path);
                }
            }

            if (runningConfigPaths.Count > 0)
            {
                configPaths.IntersectWith(runningConfigPaths);
            }

            if (configPaths.Count == 0)
            {
                return IL2WinWingCompatibilityRepairResult.Failure(
                    "IL2WinWing.dll.config could not be found. Start IL2WinWing, then try again so SRS can locate its installation folder.");
            }

            if (configPaths.Count > 1)
            {
                return IL2WinWingCompatibilityRepairResult.Failure(
                    "More than one IL2WinWing configuration was found. Start the IL2WinWing copy you use, then run this repair again. No files were changed.\n\n" +
                    string.Join("\n", configPaths));
            }

            if (writeAllowed != null && !writeAllowed())
            {
                return IL2WinWingCompatibilityRepairResult.Failure(
                    "IL-2 is running. Close IL-2 before configuring IL2WinWing compatibility.");
            }

            List<string> updatedFiles = new List<string>();
            try
            {
                foreach (string configPath in configPaths)
                {
                    if (RepairConfigFile(configPath, log))
                    {
                        updatedFiles.Add(configPath);
                    }
                }

                foreach (TelemetryDiagnosticContext context in contextList
                             .Where(item => !string.IsNullOrWhiteSpace(item.StartupConfigPath))
                             .GroupBy(item => item.StartupConfigPath, StringComparer.OrdinalIgnoreCase)
                             .Select(group => group.First()))
                {
                    bool changed = StartupConfigTelemetry.EnsureEndpoint(
                        context.StartupConfigPath,
                        context.SrsAddress,
                        IL2WinWingTelemetryDiagnosticProvider.PreferredTelemetryPort,
                        log,
                        writeAllowed);
                    if (changed)
                    {
                        updatedFiles.Add(context.StartupConfigPath);
                    }
                }
            }
            catch (Exception ex)
            {
                return IL2WinWingCompatibilityRepairResult.Failure(
                    "IL2WinWing compatibility repair failed: " + ex.Message);
            }

            string summary = updatedFiles.Count == 0
                ? "IL2WinWing compatibility was already configured correctly."
                : "IL2WinWing compatibility configured. Restart IL2WinWing before launching IL-2. Backups ending in .il2srs.bak were retained for changed files.";
            return IL2WinWingCompatibilityRepairResult.Success(summary, updatedFiles);
        }

        internal static bool RepairConfigFile(string configPath, Action<string> log)
        {
            byte[] originalBytes = File.ReadAllBytes(configPath);
            XDocument document = XDocument.Load(configPath, LoadOptions.PreserveWhitespace);
            XElement telemetryValue = FindSettingValue(document, "IL2TelemetryPort");
            XElement winWingValue = FindSettingValue(document, "WWPort");
            if (telemetryValue == null || winWingValue == null)
            {
                throw new InvalidDataException(
                    "IL2WinWing.dll.config must contain IL2TelemetryPort and WWPort settings: " + configPath);
            }

            string telemetryPort = IL2WinWingTelemetryDiagnosticProvider.PreferredTelemetryPort
                .ToString(CultureInfo.InvariantCulture);
            string winWingPort = IL2WinWingTelemetryDiagnosticProvider.DefaultWinWingPort
                .ToString(CultureInfo.InvariantCulture);
            if (telemetryValue.Value == telemetryPort && winWingValue.Value == winWingPort)
            {
                return false;
            }

            FileAttributes originalAttributes = File.GetAttributes(configPath);
            bool wasReadOnly = (originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
            string backupPath = configPath + ".il2srs.bak";
            string temporaryPath = configPath + ".il2srs.tmp";

            if (wasReadOnly)
            {
                File.SetAttributes(configPath, originalAttributes & ~FileAttributes.ReadOnly);
            }

            try
            {
                if (!File.Exists(backupPath))
                {
                    File.Copy(configPath, backupPath, false);
                }

                telemetryValue.Value = telemetryPort;
                winWingValue.Value = winWingPort;
                document.Save(temporaryPath, SaveOptions.DisableFormatting);
                File.Copy(temporaryPath, configPath, true);

                if (IL2WinWingTelemetryDiagnosticProvider.ReadPort(configPath, "IL2TelemetryPort")
                        != IL2WinWingTelemetryDiagnosticProvider.PreferredTelemetryPort
                    || IL2WinWingTelemetryDiagnosticProvider.ReadPort(configPath, "WWPort")
                        != IL2WinWingTelemetryDiagnosticProvider.DefaultWinWingPort)
                {
                    throw new IOException("Failed to verify IL2WinWing port settings after writing " + configPath);
                }

                if (log != null)
                {
                    log("Configured IL2WinWing ports at " + configPath);
                }
                return true;
            }
            catch
            {
                File.WriteAllBytes(configPath, originalBytes);
                throw;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
                finally
                {
                    if (wasReadOnly)
                    {
                        File.SetAttributes(configPath, originalAttributes);
                    }
                }
            }
        }

        internal static List<string> FindRunningIncompatibleConfigPaths()
        {
            return FindRunningConfigPaths()
                .Where(configPath => IsIncompatibleConfiguration(configPath))
                .ToList();
        }

        private static bool IsIncompatibleConfiguration(string configPath)
        {
            int? telemetryPort = IL2WinWingTelemetryDiagnosticProvider.ReadPort(
                configPath,
                "IL2TelemetryPort");
            int? winWingPort = IL2WinWingTelemetryDiagnosticProvider.ReadPort(configPath, "WWPort");
            return telemetryPort == 4322
                   || !telemetryPort.HasValue
                   || winWingPort != IL2WinWingTelemetryDiagnosticProvider.DefaultWinWingPort;
        }

        private static List<string> FindRunningConfigPaths()
        {
            HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Process process in Process.GetProcessesByName(
                         IL2WinWingTelemetryDiagnosticProvider.ProcessName))
            {
                string processPath = SafeMainModulePath(process);
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    continue;
                }

                string configPath = Path.Combine(
                    Path.GetDirectoryName(processPath),
                    IL2WinWingTelemetryDiagnosticProvider.ConfigFileName);
                if (File.Exists(configPath))
                {
                    paths.Add(configPath);
                }
            }

            return paths.ToList();
        }

        private static XElement FindSettingValue(XDocument document, string settingName)
        {
            XElement setting = document.Descendants("setting")
                .FirstOrDefault(element => string.Equals(
                    (string)element.Attribute("name"),
                    settingName,
                    StringComparison.OrdinalIgnoreCase));
            return setting == null ? null : setting.Element("value");
        }

        private static string SafeMainModulePath(Process process)
        {
            try
            {
                return process.MainModule == null ? string.Empty : process.MainModule.FileName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    internal sealed class IL2WinWingCompatibilityRepairResult
    {
        private IL2WinWingCompatibilityRepairResult(bool succeeded, string message, IEnumerable<string> updatedFiles)
        {
            Succeeded = succeeded;
            Message = message;
            UpdatedFiles = new List<string>(updatedFiles ?? Enumerable.Empty<string>());
        }

        public bool Succeeded { get; private set; }
        public string Message { get; private set; }
        public List<string> UpdatedFiles { get; private set; }

        public static IL2WinWingCompatibilityRepairResult Success(string message, IEnumerable<string> updatedFiles)
        {
            return new IL2WinWingCompatibilityRepairResult(true, message, updatedFiles);
        }

        public static IL2WinWingCompatibilityRepairResult Failure(string message)
        {
            return new IL2WinWingCompatibilityRepairResult(false, message, null);
        }
    }

    internal sealed class IL2WinWingConfigSearchResult
    {
        public IL2WinWingConfigSearchResult()
        {
            ConfigPaths = new List<string>();
        }

        public List<string> ConfigPaths { get; private set; }
        public bool ProcessRunning { get; set; }
        public int VisitedDirectories { get; set; }
        public bool SearchLimitReached { get; set; }
    }

    internal sealed class TelemetryDiagnosticReport
    {
        private readonly List<TelemetryDiagnosticItem> _items = new List<TelemetryDiagnosticItem>();

        public void Add(TelemetryDiagnosticSeverity severity, string title, string detail)
        {
            _items.Add(new TelemetryDiagnosticItem(severity, title, detail));
        }

        public void AddRange(IEnumerable<TelemetryDiagnosticItem> items)
        {
            _items.AddRange(items);
        }

        public string ToDisplayText()
        {
            StringBuilder builder = new StringBuilder();
            foreach (TelemetryDiagnosticItem item in _items)
            {
                builder.AppendLine("[" + item.SeverityLabel + "] " + item.Title);
                if (!string.IsNullOrWhiteSpace(item.Detail))
                {
                    builder.AppendLine("    " + item.Detail);
                }
            }

            return builder.ToString().TrimEnd();
        }
    }

    internal sealed class TelemetryDiagnosticItem
    {
        public TelemetryDiagnosticItem(TelemetryDiagnosticSeverity severity, string title, string detail)
        {
            Severity = severity;
            Title = title;
            Detail = detail;
        }

        public TelemetryDiagnosticSeverity Severity { get; private set; }
        public string Title { get; private set; }
        public string Detail { get; private set; }

        public string SeverityLabel
        {
            get
            {
                switch (Severity)
                {
                    case TelemetryDiagnosticSeverity.Ok:
                        return "OK";
                    case TelemetryDiagnosticSeverity.Warning:
                        return "Warning";
                    default:
                        return "Info";
                }
            }
        }
    }

    internal enum TelemetryDiagnosticSeverity
    {
        Info,
        Ok,
        Warning
    }

    internal sealed class Il2TelemetryConfiguration
    {
        public Il2TelemetryConfiguration()
        {
            Endpoints = new List<TelemetryEndpoint>();
        }

        public bool HasTelemetrySection { get; set; }
        public bool? Enabled { get; set; }
        public IList<TelemetryEndpoint> Endpoints { get; private set; }

        public bool ContainsEndpoint(string host, int port)
        {
            return Endpoints.Any(endpoint => endpoint.Matches(host, port));
        }
    }

    internal sealed class TelemetryEndpoint
    {
        public TelemetryEndpoint(string sourceKey, string host, int? port)
        {
            SourceKey = sourceKey;
            Host = host;
            Port = port;
        }

        public string SourceKey { get; private set; }
        public string Host { get; private set; }
        public int? Port { get; private set; }

        public bool Matches(string host, int port)
        {
            return Port == port && IsSameHost(Host, host);
        }

        public string ToDisplayText()
        {
            return SourceKey + "=" + Host + (Port.HasValue ? ":" + Port.Value : string.Empty);
        }

        private static bool IsSameHost(string left, string right)
        {
            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsLoopback(left) && IsLoopback(right);
        }

        private static bool IsLoopback(string host)
        {
            return string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static class Il2TelemetryConfigurationParser
    {
        private static readonly Regex TelemetrySectionRegex = new Regex(
            @"^[ \t]*\[KEY[ \t]*=[ \t]*telemetrydevice[ \t]*\][ \t]*(?:\r\n|\n|\r)(?<body>.*?)(?<end>^[ \t]*\[END\][ \t]*(?:\r\n|\n|\r|$))",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex SettingRegex = new Regex(
            @"^(?<key>[A-Za-z_][A-Za-z0-9_]*)(?<spacing>[ \t]*=[ \t]*)(?<value>.*?)(?<tail>[ \t]*(?:[#;].*)?)$",
            RegexOptions.Compiled);

        public static Il2TelemetryConfiguration Parse(string startupConfigPath)
        {
            string text = File.ReadAllText(startupConfigPath);
            Il2TelemetryConfiguration configuration = new Il2TelemetryConfiguration();
            Match section = TelemetrySectionRegex.Match(text);

            if (!section.Success)
            {
                return configuration;
            }

            configuration.HasTelemetrySection = true;
            Dictionary<string, string> settings = ParseSettings(section.Groups["body"].Value);

            string enable;
            if (settings.TryGetValue("enable", out enable))
            {
                bool enabled;
                if (bool.TryParse(Unquote(enable), out enabled))
                {
                    configuration.Enabled = enabled;
                }
            }

            AddEndpoint(configuration, "addr", settings);
            foreach (string key in settings.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                if (Regex.IsMatch(key, @"^addr\d+$", RegexOptions.IgnoreCase))
                {
                    AddEndpoint(configuration, key, settings);
                }
            }

            return configuration;
        }

        private static void AddEndpoint(Il2TelemetryConfiguration configuration, string addressKey, Dictionary<string, string> settings)
        {
            string addressValue;
            if (!settings.TryGetValue(addressKey, out addressValue))
            {
                return;
            }

            string host = Unquote(addressValue);
            int? port = null;
            string parsedHost;
            int parsedPort;

            if (TryParseHostAndPort(host, out parsedHost, out parsedPort))
            {
                host = parsedHost;
                port = parsedPort;
            }
            else
            {
                string suffix = addressKey.Length > 4 ? addressKey.Substring(4) : string.Empty;
                string portKey = "port" + suffix;
                string portValue;
                if (settings.TryGetValue(portKey, out portValue))
                {
                    int settingPort;
                    if (int.TryParse(Unquote(portValue), NumberStyles.Integer, CultureInfo.InvariantCulture, out settingPort))
                    {
                        port = settingPort;
                    }
                }
            }

            configuration.Endpoints.Add(new TelemetryEndpoint(addressKey, host, port));
        }

        private static Dictionary<string, string> ParseSettings(string body)
        {
            Dictionary<string, string> settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] lines = body.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                Match setting = SettingRegex.Match(line);
                if (setting.Success)
                {
                    settings[setting.Groups["key"].Value] = setting.Groups["value"].Value.Trim();
                }
            }

            return settings;
        }

        private static bool TryParseHostAndPort(string value, out string host, out int port)
        {
            host = value;
            port = 0;

            int separatorIndex = value.LastIndexOf(':');
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                return false;
            }

            string portText = value.Substring(separatorIndex + 1);
            if (!int.TryParse(portText, NumberStyles.Integer, CultureInfo.InvariantCulture, out port))
            {
                return false;
            }

            host = value.Substring(0, separatorIndex);
            return true;
        }

        private static string Unquote(string value)
        {
            value = value == null ? string.Empty : value.Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }
}
