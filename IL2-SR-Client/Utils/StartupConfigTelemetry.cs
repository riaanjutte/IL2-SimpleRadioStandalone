using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils
{
    internal static class StartupConfigTelemetry
    {
        private const string SrsAddress = "127.0.0.1";
        private const int SrsPort = 4322;

        private static readonly Regex TelemetrySectionRegex = new Regex(
            @"^[ \t]*\[KEY[ \t]*=[ \t]*telemetrydevice[ \t]*\][ \t]*(?:\r\n|\n|\r)(?<body>.*?)(?<end>^[ \t]*\[END\][ \t]*(?:\r\n|\n|\r|$))",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex SettingRegex = new Regex(
            @"^(?<indent>[ \t]*)(?<key>[A-Za-z_][A-Za-z0-9_]*)(?<spacing>[ \t]*=[ \t]*)(?<value>.*?)(?<tail>[ \t]*(?:[#;].*)?)$",
            RegexOptions.Compiled);

        public static bool EnsureEnabled(string cfgPath, Action<string> log)
        {
            if (string.IsNullOrWhiteSpace(cfgPath))
            {
                throw new ArgumentException("startup.cfg path is required", "cfgPath");
            }

            if (!File.Exists(cfgPath))
            {
                throw new FileNotFoundException("Unable to find IL-2 startup.cfg", cfgPath);
            }

            bool changed = false;
            RunWithRetries(delegate
            {
                FileAttributes originalAttributes = File.GetAttributes(cfgPath);
                bool wasReadOnly = (originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;

                if (wasReadOnly)
                {
                    File.SetAttributes(cfgPath, originalAttributes & ~FileAttributes.ReadOnly);
                    Log(log, "startup.cfg was read-only; temporarily made it writable");
                }

                try
                {
                    Encoding encoding;
                    string config = ReadAllText(cfgPath, out encoding);
                    bool textChanged;
                    string updatedConfig = EnsureEnabledInText(config, out textChanged);

                    if (textChanged)
                    {
                        WriteAllText(cfgPath, updatedConfig, encoding);
                        changed = true;
                        Log(log, "startup.cfg telemetrydevice section updated");
                    }
                    else
                    {
                        Log(log, "startup.cfg already contains the IL2-SRS telemetry endpoint");
                    }

                    string verifiedConfig = ReadAllText(cfgPath, out encoding);
                    if (!ContainsSrsTelemetryEndpoint(verifiedConfig))
                    {
                        throw new IOException("Failed to verify IL2-SRS telemetry endpoint in startup.cfg after writing.");
                    }
                }
                finally
                {
                    File.SetAttributes(cfgPath, originalAttributes);
                }
            });

            return changed;
        }

        internal static string EnsureEnabledInText(string config, out bool changed)
        {
            if (config == null)
            {
                config = string.Empty;
            }

            string newline = DetectNewline(config);
            Match match = TelemetrySectionRegex.Match(config);

            if (!match.Success)
            {
                changed = true;
                return AppendTelemetrySection(config, newline);
            }

            string replacement = BuildUpdatedSection(match.Value, match.Groups["body"].Value, newline);
            changed = replacement != match.Value;

            if (!changed)
            {
                return config;
            }

            return config.Substring(0, match.Index) + replacement + config.Substring(match.Index + match.Length);
        }

        private static string BuildUpdatedSection(string originalSection, string body, string newline)
        {
            string normalizedBody = NormalizeNewlines(body, newline);
            List<string> lines = new List<string>(normalizedBody.Split(new[] { newline }, StringSplitOptions.None));
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            {
                lines.RemoveAt(lines.Count - 1);
            }

            bool hasEnable = false;
            bool hasDecimation = false;
            bool hasPrimaryAddress = false;
            bool hasPrimaryPort = false;
            bool hasSrsEndpoint = false;
            HashSet<int> usedAddressIndexes = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                Match setting = SettingRegex.Match(lines[i]);
                if (!setting.Success)
                {
                    continue;
                }

                string key = setting.Groups["key"].Value;
                string value = Unquote(setting.Groups["value"].Value.Trim());
                string lowerKey = key.ToLowerInvariant();

                if (lowerKey == "enable")
                {
                    hasEnable = true;
                    if (!value.Equals("true", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = BuildSettingLine(setting, "true");
                    }
                }
                else if (lowerKey == "decimation")
                {
                    hasDecimation = true;
                }
                else if (lowerKey == "addr")
                {
                    usedAddressIndexes.Add(0);
                    if (value.Equals(SrsAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        hasPrimaryAddress = true;
                    }
                    else if (value.Equals(SrsAddress + ":" + SrsPort, StringComparison.OrdinalIgnoreCase))
                    {
                        hasSrsEndpoint = true;
                    }
                }
                else if (lowerKey == "port")
                {
                    int parsedPort;
                    if (int.TryParse(value, out parsedPort) && parsedPort == SrsPort)
                    {
                        hasPrimaryPort = true;
                    }
                }
                else if (lowerKey.StartsWith("addr", StringComparison.OrdinalIgnoreCase))
                {
                    int addressIndex;
                    if (int.TryParse(lowerKey.Substring(4), out addressIndex))
                    {
                        usedAddressIndexes.Add(addressIndex);
                    }

                    if (value.Equals(SrsAddress + ":" + SrsPort, StringComparison.OrdinalIgnoreCase))
                    {
                        hasSrsEndpoint = true;
                    }
                }
            }

            if (!hasEnable)
            {
                lines.Add("\tenable = true");
            }

            if (!hasDecimation)
            {
                lines.Add("\tdecimation = 2");
            }

            if (hasPrimaryAddress && hasPrimaryPort)
            {
                hasSrsEndpoint = true;
            }

            if (!hasSrsEndpoint)
            {
                if (!ContainsSetting(lines, "addr") && !ContainsSetting(lines, "port"))
                {
                    lines.Add("\taddr = \"" + SrsAddress + "\"");
                    lines.Add("\tport = " + SrsPort);
                }
                else
                {
                    int addressIndex = 1;
                    while (usedAddressIndexes.Contains(addressIndex))
                    {
                        addressIndex++;
                    }

                    lines.Add("\taddr" + addressIndex + " = \"" + SrsAddress + ":" + SrsPort + "\"");
                }
            }

            return "[KEY = telemetrydevice]" + newline
                   + string.Join(newline, lines) + newline
                   + "[END]" + GetSectionTrailingNewline(originalSection, newline);
        }

        private static bool ContainsSetting(IEnumerable<string> lines, string key)
        {
            foreach (string line in lines)
            {
                Match setting = SettingRegex.Match(line);
                if (setting.Success && setting.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildSettingLine(Match setting, string value)
        {
            return setting.Groups["indent"].Value
                   + setting.Groups["key"].Value
                   + setting.Groups["spacing"].Value
                   + value
                   + setting.Groups["tail"].Value;
        }

        private static string AppendTelemetrySection(string config, string newline)
        {
            StringBuilder builder = new StringBuilder(config);
            if (builder.Length > 0 && !config.EndsWith("\r\n", StringComparison.Ordinal)
                && !config.EndsWith("\n", StringComparison.Ordinal)
                && !config.EndsWith("\r", StringComparison.Ordinal))
            {
                builder.Append(newline);
            }

            if (builder.Length > 0)
            {
                builder.Append(newline);
            }

            builder.Append("[KEY = telemetrydevice]").Append(newline);
            builder.Append("\taddr = \"").Append(SrsAddress).Append("\"").Append(newline);
            builder.Append("\tdecimation = 2").Append(newline);
            builder.Append("\tenable = true").Append(newline);
            builder.Append("\tport = ").Append(SrsPort).Append(newline);
            builder.Append("[END]");

            return builder.ToString();
        }

        private static bool ContainsSrsTelemetryEndpoint(string config)
        {
            bool changed;
            return EnsureEnabledInText(config, out changed) == config && !changed;
        }

        private static string ReadAllText(string path, out Encoding encoding)
        {
            using (StreamReader reader = new StreamReader(path, true))
            {
                string text = reader.ReadToEnd();
                encoding = reader.CurrentEncoding;
                return text;
            }
        }

        private static void WriteAllText(string path, string text, Encoding encoding)
        {
            string tempPath = path + ".il2srs.tmp";
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (StreamWriter writer = new StreamWriter(stream, encoding))
                {
                    writer.Write(text);
                    writer.Flush();
                    stream.Flush(true);
                }

                File.Copy(tempPath, path, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static string DetectNewline(string text)
        {
            if (text.IndexOf("\r\n", StringComparison.Ordinal) >= 0)
            {
                return "\r\n";
            }

            if (text.IndexOf("\n", StringComparison.Ordinal) >= 0)
            {
                return "\n";
            }

            if (text.IndexOf("\r", StringComparison.Ordinal) >= 0)
            {
                return "\r";
            }

            return Environment.NewLine;
        }

        private static string NormalizeNewlines(string text, string newline)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", newline);
        }

        private static string GetSectionTrailingNewline(string section, string newline)
        {
            if (section.EndsWith("\r\n", StringComparison.Ordinal)
                || section.EndsWith("\n", StringComparison.Ordinal)
                || section.EndsWith("\r", StringComparison.Ordinal))
            {
                return newline;
            }

            return string.Empty;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }

        private static void RunWithRetries(Action action)
        {
            int[] delays = { 100, 250, 500, 1000, 2000 };
            for (int attempt = 0; attempt <= delays.Length; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (attempt < delays.Length)
                {
                    Thread.Sleep(delays[attempt]);
                }
                catch (UnauthorizedAccessException) when (attempt < delays.Length)
                {
                    Thread.Sleep(delays[attempt]);
                }
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
