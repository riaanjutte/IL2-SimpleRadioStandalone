using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Installer
{
    internal static class StartupConfigTelemetry
    {
        private const string SrsAddress = "127.0.0.1";
        private const int SrsPort = 4322;
        private const string RecoveryBackupSuffix = ".il2srs.lastgood";
        private static readonly Regex TelemetrySectionRegex = new Regex(
            @"^[ \t]*\[KEY[ \t]*=[ \t]*telemetrydevice[ \t]*\][ \t]*(?:\r\n|\n|\r)(?<body>.*?)(?<end>^[ \t]*\[END\][ \t]*(?:\r\n|\n|\r|$))",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex SettingRegex = new Regex(
            @"^(?<indent>[ \t]*)(?<key>[A-Za-z_][A-Za-z0-9_]*)(?<spacing>[ \t]*=[ \t]*)(?<value>.*?)(?<tail>[ \t]*(?:[#;].*)?)$",
            RegexOptions.Compiled);

        private static readonly Regex CompleteSectionRegex = new Regex(
            @"^[ \t]*\[KEY[ \t]*=[^\]]+\][ \t]*(?:\r\n|\n|\r).*?^[ \t]*\[END\][ \t]*(?:\r\n|\n|\r|$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

        public static void EnsureEnabled(string cfgPath, Action<string> log)
        {
            EnsureEnabled(cfgPath, log, null);
        }

        public static void EnsureEnabled(string cfgPath, Action<string> log, Func<bool> writeAllowed)
        {
            if (string.IsNullOrWhiteSpace(cfgPath))
            {
                throw new ArgumentException("startup.cfg path is required", "cfgPath");
            }

            if (!File.Exists(cfgPath))
            {
                throw new FileNotFoundException("Unable to find IL-2 startup.cfg", cfgPath);
            }

            RunWithRetries(delegate
            {
                StartupConfigFile configFile = ReadConfigFile(cfgPath);
                EnsureHealthyConfig(configFile.Text, cfgPath);
                bool changed;
                string updatedConfig = EnsureEnabledInText(configFile.Text, out changed);

                if (!changed)
                {
                    RefreshRecoveryBackup(cfgPath, configFile, log, writeAllowed);
                    Log(log, "startup.cfg already contains the IL2-SRS telemetry endpoint");
                    return;
                }

                if (writeAllowed != null && !writeAllowed())
                {
                    throw new InvalidOperationException(
                        "IL-2 is running. Close IL-2 before the installer repairs startup.cfg.");
                }

                FileAttributes originalAttributes = File.GetAttributes(cfgPath);
                bool wasReadOnly = (originalAttributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;

                if (wasReadOnly)
                {
                    File.SetAttributes(cfgPath, originalAttributes & ~FileAttributes.ReadOnly);
                    Log(log, "startup.cfg was read-only; temporarily made it writable");
                }

                try
                {
                    WriteBackupIfMissing(cfgPath, configFile.OriginalBytes, log);
                    WriteAllText(cfgPath, updatedConfig, configFile, writeAllowed);
                    Log(log, "startup.cfg telemetrydevice section updated");

                    StartupConfigFile verifiedConfig = ReadConfigFile(cfgPath);
                    if (!ContainsSrsTelemetryEndpoint(verifiedConfig.Text))
                    {
                        throw new IOException("Failed to verify IL2-SRS telemetry endpoint in startup.cfg after writing.");
                    }

                    RefreshRecoveryBackup(cfgPath, verifiedConfig, log, writeAllowed);
                }
                finally
                {
                    if (wasReadOnly)
                    {
                        File.SetAttributes(cfgPath, originalAttributes);
                        Log(log, "startup.cfg read-only attribute restored");
                    }
                }
            });
        }

        internal static bool IsEnabled(string cfgPath)
        {
            if (string.IsNullOrWhiteSpace(cfgPath) || !File.Exists(cfgPath))
            {
                return false;
            }

            return ContainsSrsTelemetryEndpoint(ReadConfigFile(cfgPath).Text);
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

        private static StartupConfigFile ReadConfigFile(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding encoding;
            int preambleLength;
            DetectEncoding(bytes, out encoding, out preambleLength);

            return new StartupConfigFile(
                encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength),
                encoding,
                preambleLength > 0,
                bytes);
        }

        private static void DetectEncoding(byte[] bytes, out Encoding encoding, out int preambleLength)
        {
            if (StartsWith(bytes, new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
            {
                encoding = new UTF32Encoding(true, true);
                preambleLength = 4;
                return;
            }

            if (StartsWith(bytes, new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
            {
                encoding = new UTF32Encoding(false, true);
                preambleLength = 4;
                return;
            }

            if (StartsWith(bytes, new byte[] { 0xEF, 0xBB, 0xBF }))
            {
                encoding = new UTF8Encoding(true);
                preambleLength = 3;
                return;
            }

            if (StartsWith(bytes, new byte[] { 0xFE, 0xFF }))
            {
                encoding = new UnicodeEncoding(true, true);
                preambleLength = 2;
                return;
            }

            if (StartsWith(bytes, new byte[] { 0xFF, 0xFE }))
            {
                encoding = new UnicodeEncoding(false, true);
                preambleLength = 2;
                return;
            }

            try
            {
                encoding = new UTF8Encoding(false, true);
                encoding.GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                encoding = Encoding.Default;
            }

            preambleLength = 0;
        }

        private static void WriteBackupIfMissing(string path, byte[] originalBytes, Action<string> log)
        {
            string backupPath = path + ".il2srs.bak";
            if (File.Exists(backupPath))
            {
                return;
            }

            using (FileStream stream = new FileStream(backupPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(originalBytes, 0, originalBytes.Length);
                stream.Flush(true);
            }

            Log(log, "Original startup.cfg backed up to " + backupPath);
        }

        private static bool IsHealthyConfig(string text)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && CompleteSectionRegex.Matches(text).Count >= 2;
        }

        private static void EnsureHealthyConfig(string text, string path)
        {
            if (!IsHealthyConfig(text))
            {
                throw new InvalidDataException(
                    "startup.cfg is missing, empty, or incomplete. The installer will not modify it. Restore a known-good backup or let IL-2 recreate it: "
                    + path);
            }
        }

        private static void RefreshRecoveryBackup(
            string path,
            StartupConfigFile configFile,
            Action<string> log,
            Func<bool> writeAllowed)
        {
            if (writeAllowed != null && !writeAllowed())
            {
                Log(log, "Skipped startup.cfg recovery backup refresh because IL-2 is running");
                return;
            }

            string backupPath = path + RecoveryBackupSuffix;
            string tempPath = backupPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    stream.Write(configFile.OriginalBytes, 0, configFile.OriginalBytes.Length);
                    stream.Flush(true);
                }

                if (!BytesEqual(File.ReadAllBytes(path), configFile.OriginalBytes))
                {
                    throw new IOException("startup.cfg changed while the installer was creating its recovery backup.");
                }

                if (File.Exists(backupPath))
                {
                    File.Replace(tempPath, backupPath, null, true);
                }
                else
                {
                    File.Move(tempPath, backupPath);
                }

                Log(log, "Known-good startup.cfg recovery backup refreshed at " + backupPath);
            }
            catch (Exception ex)
            {
                Log(log, "Unable to refresh startup.cfg recovery backup: " + ex.Message);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static void WriteAllText(
            string path,
            string text,
            StartupConfigFile original,
            Func<bool> writeAllowed)
        {
            string tempPath = path + ".il2srs." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] body = original.Encoding.GetBytes(text);
                byte[] preamble = original.HadPreamble ? original.Encoding.GetPreamble() : new byte[0];

                using (FileStream stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                {
                    if (preamble.Length > 0)
                    {
                        stream.Write(preamble, 0, preamble.Length);
                    }

                    stream.Write(body, 0, body.Length);
                    stream.Flush(true);
                }

                if (writeAllowed != null && !writeAllowed())
                {
                    throw new InvalidOperationException(
                        "IL-2 started while the installer was preparing a startup.cfg repair. No changes were made.");
                }

                if (!BytesEqual(File.ReadAllBytes(path), original.OriginalBytes))
                {
                    throw new IOException(
                        "startup.cfg changed while SRS was preparing the telemetry repair. The repair was aborted.");
                }

                File.Replace(tempPath, path, null, true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static bool StartsWith(byte[] bytes, byte[] prefix)
        {
            if (bytes.Length < prefix.Length)
            {
                return false;
            }

            for (int i = 0; i < prefix.Length; i++)
            {
                if (bytes[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
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

        private sealed class StartupConfigFile
        {
            public StartupConfigFile(string text, Encoding encoding, bool hadPreamble, byte[] originalBytes)
            {
                Text = text;
                Encoding = encoding;
                HadPreamble = hadPreamble;
                OriginalBytes = originalBytes;
            }

            public string Text { get; private set; }
            public Encoding Encoding { get; private set; }
            public bool HadPreamble { get; private set; }
            public byte[] OriginalBytes { get; private set; }
        }
    }
}
