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
using Microsoft.Win32;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics
{
    internal sealed class TelemetryDiagnosticsService
    {
        private const string SrsAddress = "127.0.0.1";
        private const int DefaultSrsPort = 4322;
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
            TelemetryDiagnosticContext context = BuildContext();
            TelemetryDiagnosticReport report = new TelemetryDiagnosticReport();

            report.Add(TelemetryDiagnosticSeverity.Info,
                "IL-2 startup.cfg",
                string.IsNullOrWhiteSpace(context.StartupConfigPath)
                    ? "No saved IL-2 path was found in the IL2-SRS installer registry key."
                    : context.StartupConfigPath);

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

            return report.ToDisplayText(context);
        }

        private static TelemetryDiagnosticContext BuildContext()
        {
            int srsPort = ReadSrsTelemetryPort();
            string il2Path = ReadInstallerPath("IL2Path");
            string startupConfigPath = string.IsNullOrWhiteSpace(il2Path)
                ? string.Empty
                : Path.Combine(il2Path, "data", "startup.cfg");

            Il2TelemetryConfiguration startupConfig = null;
            if (!string.IsNullOrWhiteSpace(startupConfigPath) && File.Exists(startupConfigPath))
            {
                startupConfig = Il2TelemetryConfigurationParser.Parse(startupConfigPath);
            }

            return new TelemetryDiagnosticContext(
                SrsAddress,
                srsPort,
                il2Path,
                startupConfigPath,
                startupConfig);
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

    internal interface ITelemetryDiagnosticProvider
    {
        IEnumerable<TelemetryDiagnosticItem> Diagnose(TelemetryDiagnosticContext context);
    }

    internal sealed class TelemetryDiagnosticContext
    {
        public TelemetryDiagnosticContext(
            string srsAddress,
            int srsPort,
            string il2InstallPath,
            string startupConfigPath,
            Il2TelemetryConfiguration startupConfig)
        {
            SrsAddress = srsAddress;
            SrsPort = srsPort;
            Il2InstallPath = il2InstallPath;
            StartupConfigPath = startupConfigPath;
            StartupConfig = startupConfig;
        }

        public string SrsAddress { get; private set; }
        public int SrsPort { get; private set; }
        public string Il2InstallPath { get; private set; }
        public string StartupConfigPath { get; private set; }
        public Il2TelemetryConfiguration StartupConfig { get; private set; }
    }

    internal sealed class IL2WinWingTelemetryDiagnosticProvider : ITelemetryDiagnosticProvider
    {
        private const string ProcessName = "IL2WinWing";
        private const string ConfigFileName = "IL2WinWing.dll.config";
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
                int? telemetryPort = ReadTelemetryPort(configPath);
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
                        "IL2WinWing is configured for IL-2 telemetry port " + telemetryPort.Value + ", which is also the SRS telemetry port. Change IL2WinWing's IL2TelemetryPort to another port and add a matching addrN endpoint in startup.cfg."));
                }
                else if (context.StartupConfig != null && !context.StartupConfig.ContainsEndpoint(context.SrsAddress, telemetryPort.Value))
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Warning,
                        "IL2WinWing port missing from startup.cfg",
                        "IL2WinWing uses IL-2 telemetry port " + telemetryPort.Value + ", but startup.cfg does not contain " + context.SrsAddress + ":" + telemetryPort.Value + "."));
                }
                else
                {
                    items.Add(new TelemetryDiagnosticItem(
                        TelemetryDiagnosticSeverity.Ok,
                        "IL2WinWing telemetry port",
                        "IL2WinWing uses IL-2 telemetry port " + telemetryPort.Value + " at " + configPath + "."));
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

        private static int? ReadTelemetryPort(string configPath)
        {
            try
            {
                XDocument document = XDocument.Load(configPath);
                XElement setting = document.Descendants("setting")
                    .FirstOrDefault(element =>
                        string.Equals((string)element.Attribute("name"), "IL2TelemetryPort", StringComparison.OrdinalIgnoreCase));
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

        private static IL2WinWingConfigSearchResult FindConfigFiles(TelemetryDiagnosticContext context)
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

        public string ToDisplayText(TelemetryDiagnosticContext context)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Telemetry diagnostics");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            builder.AppendLine("SRS telemetry endpoint: " + context.SrsAddress + ":" + context.SrsPort);
            builder.AppendLine();

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
