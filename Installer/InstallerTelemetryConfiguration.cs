using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Installer
{
    internal sealed class Il2RunningTelemetryConfigurationException : InvalidOperationException
    {
        public Il2RunningTelemetryConfigurationException(string startupConfigPath)
            : base("IL-2 is running. Close IL-2 before the installer repairs startup.cfg.")
        {
            StartupConfigPath = startupConfigPath;
        }

        public string StartupConfigPath { get; private set; }
    }

    internal sealed class InstallerFailurePresentation
    {
        private InstallerFailurePresentation(
            string title,
            string message,
            MessageBoxImage image,
            bool openSupportResources)
        {
            Title = title;
            Message = message;
            Image = image;
            OpenSupportResources = openSupportResources;
        }

        public string Title { get; private set; }
        public string Message { get; private set; }
        public MessageBoxImage Image { get; private set; }
        public bool OpenSupportResources { get; private set; }

        public static InstallerFailurePresentation FromException(Exception exception)
        {
            Il2RunningTelemetryConfigurationException il2Running =
                exception as Il2RunningTelemetryConfigurationException;
            if (il2Running != null)
            {
                string message =
                    "IL-2 IS STILL RUNNING\n\n"
                    + "Close IL-2 before installing or updating SRS. SRS cannot safely update its telemetry settings while the game is running.\n\n"
                    + "1. Close every running copy of IL-2 Great Battles and IL-2 Korea.\n"
                    + "2. Wait for the game to finish closing.\n"
                    + "3. Run this SRS update again.\n\n"
                    + "Affected file:\n"
                    + il2Running.StartupConfigPath;

                return new InstallerFailurePresentation(
                    "SRS UPDATE BLOCKED - CLOSE IL-2",
                    message,
                    MessageBoxImage.Warning,
                    false);
            }

            string errorMessage = "Error with installation.";
            if (exception != null && !string.IsNullOrWhiteSpace(exception.Message))
            {
                errorMessage += "\n\n" + exception.Message;
            }

            errorMessage +=
                "\n\nPlease post your installer-log.txt on the SRS Discord under IL2-SRS support if the problem continues.";

            return new InstallerFailurePresentation(
                "Installation Error",
                errorMessage,
                MessageBoxImage.Error,
                true);
        }
    }

    internal sealed class TelemetryConfigurationWarning
    {
        public TelemetryConfigurationWarning(Il2Install install, string message)
        {
            DisplayName = install.DisplayName;
            StartupConfigPath = install.StartupConfigPath;
            Message = message;
        }

        public string DisplayName { get; private set; }
        public string StartupConfigPath { get; private set; }
        public string Message { get; private set; }
    }

    internal sealed class TelemetryConfigurationResult
    {
        public TelemetryConfigurationResult(IEnumerable<Il2Install> detectedInstalls)
        {
            DetectedInstalls = (detectedInstalls ?? Enumerable.Empty<Il2Install>()).ToList();
            ConfiguredInstalls = new List<Il2Install>();
            Warnings = new List<TelemetryConfigurationWarning>();
        }

        public List<Il2Install> DetectedInstalls { get; private set; }
        public List<Il2Install> ConfiguredInstalls { get; private set; }
        public List<TelemetryConfigurationWarning> Warnings { get; private set; }
    }

    internal static class InstallerTelemetryConfiguration
    {
        public static TelemetryConfigurationResult Configure(
            IEnumerable<Il2Install> installs,
            Action<Il2Install> configure,
            Action<string> log)
        {
            if (configure == null)
            {
                throw new ArgumentNullException("configure");
            }

            TelemetryConfigurationResult result = new TelemetryConfigurationResult(installs);
            foreach (Il2Install install in result.DetectedInstalls)
            {
                try
                {
                    configure(install);
                    result.ConfiguredInstalls.Add(install);
                }
                catch (Exception ex) when (IsDamagedOrMissingConfig(ex))
                {
                    result.Warnings.Add(new TelemetryConfigurationWarning(install, ex.Message));
                    Log(log,
                        "Telemetry configuration warning for " + install.StartupConfigPath + ": " + ex.Message
                        + " SRS binary installation will continue.");
                }
            }

            return result;
        }

        private static bool IsDamagedOrMissingConfig(Exception ex)
        {
            return ex is InvalidDataException || ex is FileNotFoundException;
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
