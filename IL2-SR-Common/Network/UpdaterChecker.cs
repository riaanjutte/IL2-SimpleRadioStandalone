using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Windows;
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using NLog;
using Octokit;
using WPFCustomMessageBox;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    //Quick and dirty update checker based on GitHub Published Versions
    public class UpdaterChecker
    {
        public static readonly string GITHUB_USERNAME = ReleaseMetadata.GithubUsername;
        public static readonly string GITHUB_REPOSITORY = ReleaseMetadata.GithubRepository;
        // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
        public static readonly string GITHUB_USER_AGENT = ReleaseMetadata.GithubUserAgent;

        public static readonly string MINIMUM_PROTOCOL_VERSION = ReleaseMetadata.MinimumProtocolVersion;

        public static readonly string VERSION = ReleaseMetadata.Version;
        public static readonly string RELEASE_TAG = ReleaseMetadata.ReleaseTag;

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static async void CheckForUpdate(bool checkForBetaUpdates)
        {
            var currentRelease = UpdateReleaseSelector.CreateCurrentReleaseInfo(VERSION, RELEASE_TAG);

#if DEBUG
            _logger.Info("Skipping update check due to DEBUG mode");
#else
            try  
            {
                var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, VERSION));
            
                var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);
            
                var releaseCandidates = releases.Select(ToUpdateReleaseCandidate).ToList();
                var selectedRelease = UpdateReleaseSelector.SelectClientUpdate(
                    releaseCandidates,
                    currentRelease,
                    checkForBetaUpdates);
            
                if (selectedRelease != null)
                {
                    ShowUpdateAvailableDialog(
                        selectedRelease.IsPrerelease ? "beta" : "stable",
                        selectedRelease.DisplayVersion,
                        selectedRelease.HtmlUrl,
                        selectedRelease.IsPrerelease,
                        selectedRelease.TagName);
                }
                else
                {
                    _logger.Warn($"No newer release available for current version: {currentRelease.DisplayVersion}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check for updated version");
            }
#endif
        }

        public static void ShowUpdateAvailableDialog(string branch, string version, string url, bool beta, string releaseTag)
        {
            _logger.Warn($"New {branch} version available on GitHub: {version}");

            var result = CustomMessageBox.ShowYesNoCancel(
                $"New {branch} version {version} available!\n\nDo you want to auto update? \n\nYes - Launch Auto Update \n\nNo - Manual update - launches browser\n\nCancel - ignores the update",
                "Update available",
                GetLocalizedDialogButtonText("Yes"),
                GetLocalizedDialogButtonText("No"),
                GetLocalizedDialogButtonText("Cancel"),
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    LaunchUpdater(beta, releaseTag);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to launch IL2-SRS AutoUpdater");
                    MessageBox.Show($"Unable to Auto Update - please download latest version manually",
                        "Auto Update Error", MessageBoxButton.OK, MessageBoxImage.Information);

                    OpenBrowser(url);
                }
            }
            else if (result == MessageBoxResult.No)
            {
                OpenBrowser(url);
            }
        }

        private static void OpenBrowser(string url)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        private static void LaunchUpdater(bool beta, string releaseTag)
        {
            var updaterPath = ResolveUpdaterPath();
            if (string.IsNullOrEmpty(updaterPath))
            {
                throw new FileNotFoundException(
                    $"Could not find IL2-SRS-AutoUpdater.exe in {AppDomain.CurrentDomain.BaseDirectory}");
            }

            var updaterDirectory = Path.GetDirectoryName(updaterPath);
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = principal.IsInRole(WindowsBuiltInRole.Administrator);
        
            if (!hasAdministrativeRight)
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = updaterDirectory,
                    FileName = updaterPath,
                    Verb = "runas"
                };
    
                startInfo.Arguments = UpdaterArguments.Build(beta, releaseTag);
              
                try
                {
                    Process p = Process.Start(startInfo);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show(
                        "IL2-SRS Auto Update Requires Admin Rights",
                        "UAC Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = updaterDirectory,
                    FileName = updaterPath,
                    Arguments = UpdaterArguments.Build(beta, releaseTag)
                };

                Process.Start(startInfo);
            }
        }

        private static string GetLocalizedDialogButtonText(string key)
        {
            var language = System.Threading.Thread.CurrentThread.CurrentUICulture.TwoLetterISOLanguageName;

            switch (language)
            {
                case "de":
                    switch (key)
                    {
                        case "Yes":
                            return "Ja";
                        case "No":
                            return "Nein";
                        case "Cancel":
                            return "Abbrechen";
                    }
                    break;
                case "fr":
                    switch (key)
                    {
                        case "Yes":
                            return "Oui";
                        case "No":
                            return "Non";
                        case "Cancel":
                            return "Annuler";
                    }
                    break;
                case "es":
                    switch (key)
                    {
                        case "Yes":
                            return "Sí";
                        case "No":
                            return "No";
                        case "Cancel":
                            return "Cancelar";
                    }
                    break;
                case "it":
                    switch (key)
                    {
                        case "Yes":
                            return "Sì";
                        case "No":
                            return "No";
                        case "Cancel":
                            return "Annulla";
                    }
                    break;
                case "ru":
                    switch (key)
                    {
                        case "Yes":
                            return "Да";
                        case "No":
                            return "Нет";
                        case "Cancel":
                            return "Отмена";
                    }
                    break;
            }

            return key;
        }

        private static string ResolveUpdaterPath()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDirectory, "IL2-SRS-AutoUpdater.exe"),
                Path.Combine(baseDirectory, "AutoUpdater.exe")
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private static UpdateReleaseCandidate ToUpdateReleaseCandidate(Release release)
        {
            return new UpdateReleaseCandidate
            {
                TagName = release.TagName,
                HtmlUrl = release.HtmlUrl,
                IsDraft = release.Draft,
                IsPrerelease = release.Prerelease,
                Assets = release.Assets
                    .Select(asset => new UpdateReleaseAsset
                    {
                        Name = asset.Name,
                        BrowserDownloadUrl = asset.BrowserDownloadUrl
                    })
                    .ToList()
            };
        }
    }
}
