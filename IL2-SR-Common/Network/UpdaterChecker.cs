using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using Octokit;
using Application = System.Windows.Application;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common
{
    //Quick and dirty update checker based on GitHub Published Versions
    public class UpdaterChecker
    {
        public static readonly string GITHUB_USERNAME = "riaanjutte";
        public static readonly string GITHUB_REPOSITORY = "IL2-SimpleRadioStandalone";
        // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
        public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";

        public static readonly string MINIMUM_PROTOCOL_VERSION = "1.0.0.0";

        public static readonly string VERSION = "1.0.4.0";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static async void CheckForUpdate(bool checkForBetaUpdates)
        {
            Version currentVersion = Version.Parse(VERSION);

#if DEBUG
            _logger.Info("Skipping update check due to DEBUG mode");
#else
            try  
            {
                var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, VERSION));
            
                var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);
            
                Version latestStableVersion = new Version();
                Release latestStableRelease = null;
                Version latestBetaVersion = new Version();
                Release latestBetaRelease = null;
            
                // Retrieve last stable and beta branch release as tagged on GitHub
                foreach (Release release in releases)
                {
                    Version releaseVersion;
            
                    if (Version.TryParse(release.TagName.Replace("v", ""), out releaseVersion))
                    {
                        if (release.Prerelease && releaseVersion > latestBetaVersion)
                        {
                            latestBetaRelease = release;
                            latestBetaVersion = releaseVersion;
                        }
                        else if (!release.Prerelease && releaseVersion > latestStableVersion)
                        {
                            latestStableRelease = release;
                            latestStableVersion = releaseVersion;
                        }
                    }
                    else
                    {
                        _logger.Warn($"Failed to parse GitHub release version {release.TagName}");
                    }
                }

                if(latestStableRelease ==null)
                {
                    _logger.Warn($"No releases available");
                    return;
                }
            
                // Compare latest versions with currently running version depending on user branch choice
                if (checkForBetaUpdates && latestBetaVersion > currentVersion)
                {
                    ShowUpdateAvailableDialog("beta", latestBetaVersion, latestBetaRelease.HtmlUrl, true);
                }
                else if (latestStableVersion > currentVersion)
                {
                    ShowUpdateAvailableDialog("stable", latestStableVersion, latestStableRelease.HtmlUrl, false);
                }
                else if (checkForBetaUpdates && latestBetaVersion == currentVersion)
                {
                    _logger.Warn($"Running latest beta version: {currentVersion}");
                }
                else if (latestStableVersion == currentVersion)
                {
                    _logger.Warn($"Running latest stable version: {currentVersion}");
                }
                else
                {
                    _logger.Warn($"Running development version: {currentVersion}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check for updated version");
            }
#endif
        }

        public static void ShowUpdateAvailableDialog(string branch, Version version, string url, bool beta)
        {
            _logger.Warn($"New {branch} version available on GitHub: {version}");

            var result = MessageBox.Show($"New {branch} version {version} available!\n\nDo you want to auto update? \n\nYes - Launch Auto Update \n\nNo - Manual update - launches browser\n\nCancel - ignores the update",
                "Update available", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    LaunchUpdater(beta);
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

        private static void LaunchUpdater(bool beta)
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
    
                if (beta)
                {
                    startInfo.Arguments = "-beta";
                }
              
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
                    Arguments = beta ? "-beta" : string.Empty
                };

                Process.Start(startInfo);
            }
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
    }
}
