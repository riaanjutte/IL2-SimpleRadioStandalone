using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Singletons;
using Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow;
using Ciribob.IL2.SimpleRadio.Standalone.Common;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics
{
    internal sealed class DiagnosticBundle
    {
        public string ZipPath { get; set; }
        public bool Redacted { get; set; }
    }

    internal static class DiagnosticBundleBuilder
    {
        private const string RepositoryIssuesUrl = "https://github.com/riaanjutte/IL2-SimpleRadioStandalone/issues/new";

        public static DiagnosticBundle Create(bool redact, string serverName, string serverAddress)
        {
            var diagnosticsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "IL2-SRS Diagnostics");
            Directory.CreateDirectory(diagnosticsDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            var zipPath = Path.Combine(diagnosticsDirectory, $"IL2-SRS-Diagnostics-{timestamp}.zip");

            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                AddTextEntry(archive, "diagnostics.txt", BuildDiagnosticsText(serverName, serverAddress), redact);
                AddLogIfPresent(archive, "clientlog.txt", redact);
                AddLogIfPresent(archive, "clientlog.old.txt", redact);
            }

            return new DiagnosticBundle
            {
                ZipPath = zipPath,
                Redacted = redact
            };
        }

        public static string BuildBugReportUrl(DiagnosticBundle bundle)
        {
            var title = Uri.EscapeDataString("[Bug] ");
            var body = Uri.EscapeDataString(
                "## What happened?\r\n\r\n" +
                "\r\n\r\n## What did you expect to happen?\r\n\r\n" +
                "\r\n\r\n## Steps to reproduce\r\n\r\n1. \r\n2. \r\n3. \r\n\r\n" +
                "## Diagnostic bundle\r\n\r\n" +
                "A diagnostic ZIP was created locally by the client.\r\n\r\n" +
                "Please review it and attach it to this issue if you are comfortable sharing it.\r\n\r\n" +
                $"Redaction enabled: {(bundle.Redacted ? "yes" : "no")}\r\n");

            return $"{RepositoryIssuesUrl}?title={title}&labels=bug&body={body}";
        }

        public static string BuildFeatureRequestUrl()
        {
            var title = Uri.EscapeDataString("[Suggestion] ");
            var body = Uri.EscapeDataString(
                "## What would you like improved?\r\n\r\n" +
                "\r\n\r\n## Why would this help?\r\n\r\n" +
                "\r\n\r\n## Any examples or screenshots?\r\n\r\n");

            return $"{RepositoryIssuesUrl}?title={title}&labels=enhancement&body={body}";
        }

        private static string BuildDiagnosticsText(string serverName, string serverAddress)
        {
            var settings = GlobalSettingsStore.Instance;
            var audioInput = AudioInputSingleton.Instance;
            var audioOutput = AudioOutputSingleton.Instance;
            var clientState = ClientStateSingleton.Instance;
            var profileStore = settings.ProfileSettingsStore;
            var builder = new StringBuilder();

            builder.AppendLine("IL2-SRS Community Edition Diagnostics");
            builder.AppendLine("Generated: " + DateTime.Now.ToString("O", CultureInfo.InvariantCulture));
            builder.AppendLine("Version: " + UpdaterChecker.VERSION);
            builder.AppendLine("Release tag: " + UpdaterChecker.RELEASE_TAG);
            builder.AppendLine("OS: " + Environment.OSVersion);
            builder.AppendLine(".NET CLR: " + Environment.Version);
            builder.AppendLine("64-bit process: " + Environment.Is64BitProcess);
            builder.AppendLine("64-bit OS: " + Environment.Is64BitOperatingSystem);
            builder.AppendLine();

            builder.AppendLine("Client State");
            builder.AppendLine("Connected: " + clientState.IsConnected);
            builder.AppendLine("Selected server name: " + EmptyIfNull(serverName));
            builder.AppendLine("Selected server address: " + EmptyIfNull(serverAddress));
            builder.AppendLine("Profile: " + EmptyIfNull(profileStore.CurrentProfileName));
            builder.AppendLine("Language: " + EmptyIfNull(settings.GetClientSetting(GlobalSettingsKeys.Language).RawValue));
            builder.AppendLine("Theme: " + EmptyIfNull(settings.GetClientSetting(GlobalSettingsKeys.Theme).RawValue));
            builder.AppendLine("VU meter: " + EmptyIfNull(settings.GetClientSetting(GlobalSettingsKeys.VuMeterStyle).RawValue));
            builder.AppendLine("3D effects enabled: " + settings.GetClientSettingBool(GlobalSettingsKeys.ThreeDEffectsEnabled));
            builder.AppendLine("Weathering enabled: " + settings.GetClientSettingBool(GlobalSettingsKeys.WeatheringEnabled));
            builder.AppendLine("Weathering opacity: " + settings.GetClientSetting(GlobalSettingsKeys.WeatheringOpacity).RawValue);
            builder.AppendLine("Beta updates enabled: " + settings.GetClientSettingBool(GlobalSettingsKeys.CheckForBetaUpdates));
            builder.AppendLine();

            builder.AppendLine("Audio Devices");
            builder.AppendLine("Microphone available: " + audioInput.MicrophoneAvailable);
            builder.AppendLine("Selected microphone: " + SafeDeviceText(audioInput.SelectedAudioInput));
            builder.AppendLine("Selected speakers: " + SafeDeviceText(audioOutput.SelectedAudioOutput));
            builder.AppendLine("Selected mic output: " + SafeDeviceText(audioOutput.SelectedMicAudioOutput));
            builder.AppendLine("Windows N detected: " + audioOutput.WindowsN);
            builder.AppendLine();

            builder.AppendLine("Profile Audio/PTT Settings");
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.RadioSwitchIsPTT);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.PTTReleaseDelay);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.SelectedRadioMutedVolume);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.RadioEffects);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.RadioEffectsClipping);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.EnableTextToSpeech);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.Radio1Channel);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.Radio2Channel);
            AppendProfileSetting(builder, profileStore, ProfileSettingsKeys.IntercomChannel);
            builder.AppendLine();

            builder.AppendLine("Input Bindings");
            foreach (var binding in profileStore.GetCurrentInputProfile().OrderBy(pair => pair.Key.ToString()))
            {
                var device = binding.Value;
                builder.AppendLine($"{binding.Key}: {device.DeviceName}; Button={device.Button}; ButtonValue={device.ButtonValue}; InstanceGuid={device.InstanceGuid}; ProductGuid={device.ProductGuid}");
            }

            builder.AppendLine();
            builder.AppendLine("Privacy note: this diagnostic summary is intended for support. If redaction was enabled, common user paths, GUIDs, IP addresses, and device ID patterns were replaced with placeholders.");
            return builder.ToString();
        }

        private static void AppendProfileSetting(StringBuilder builder, ProfileSettingsStore profileStore, ProfileSettingsKeys key)
        {
            builder.AppendLine(key + ": " + profileStore.GetClientSetting(key).RawValue);
        }

        private static string SafeDeviceText(AudioDeviceListItem item)
        {
            return string.IsNullOrWhiteSpace(item?.Text) ? string.Empty : item.Text;
        }

        private static string EmptyIfNull(string value)
        {
            return value ?? string.Empty;
        }

        private static void AddLogIfPresent(ZipArchive archive, string fileName, bool redact)
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            if (!File.Exists(path))
            {
                return;
            }

            AddTextEntry(archive, fileName, ReadAllTextShared(path), redact);
        }

        private static string ReadAllTextShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                return reader.ReadToEnd();
            }
        }

        private static void AddTextEntry(ZipArchive archive, string entryName, string text, bool redact)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, Encoding.UTF8))
            {
                writer.Write(redact ? Redact(text) : text);
            }
        }

        private static string Redact(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var redacted = text;
            redacted = Regex.Replace(redacted, @"[A-Za-z]:\\Users\\[^\\\r\n]+", @"C:\Users\<user>");
            redacted = Regex.Replace(redacted, @"(?i)(?<=/Users/)[^/\r\n]+", "<user>");
            redacted = Regex.Replace(redacted, @"\b(?:\d{1,3}\.){3}\d{1,3}\b", "<ip-address>");
            redacted = Regex.Replace(redacted, @"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", "<guid>");
            redacted = Regex.Replace(redacted, @"\{[0-9A-Fa-f\.\-]{8,}\}", "{device-id}");
            return redacted;
        }
    }
}
