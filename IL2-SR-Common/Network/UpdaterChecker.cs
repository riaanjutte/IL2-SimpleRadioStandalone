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

        public static readonly string VERSION = "1.0.4.5";
        public static readonly string RELEASE_TAG = "1.0.4.5-beta.3";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static async void CheckForUpdate(bool checkForBetaUpdates)
        {
            ReleaseInfo currentRelease = CreateCurrentReleaseInfo();
            Version currentVersion = currentRelease.Version;

#if DEBUG
            _logger.Info("Skipping update check due to DEBUG mode");
#else
            try  
            {
                var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, VERSION));
            
                var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);
            
                ReleaseInfo latestStableRelease = null;
                ReleaseInfo latestBetaRelease = null;
            
                // Retrieve last stable and beta branch release as tagged on GitHub
                foreach (Release release in releases)
                {
                    if (release.Draft)
                    {
                        continue;
                    }

                    ReleaseInfo releaseInfo;
                    if (!TryCreateReleaseInfo(release, out releaseInfo))
                    {
                        _logger.Warn($"Failed to parse GitHub release version {release.TagName}");
                        continue;
                    }

                    if (releaseInfo.IsPrerelease)
                    {
                        latestBetaRelease = GetNewerRelease(latestBetaRelease, releaseInfo);
                    }
                    else
                    {
                        latestStableRelease = GetNewerRelease(latestStableRelease, releaseInfo);
                    }
                }

                if(latestStableRelease ==null)
                {
                    _logger.Warn($"No stable releases available");
                    return;
                }
            
                // Compare latest versions with currently running version depending on user branch choice
                if (checkForBetaUpdates &&
                    latestBetaRelease != null &&
                    IsBetaUpdateAvailable(latestBetaRelease, latestStableRelease, currentRelease))
                {
                    ShowUpdateAvailableDialog("beta", latestBetaRelease.DisplayVersion, latestBetaRelease.Release.HtmlUrl, true, latestBetaRelease.Release.TagName);
                }
                else if (IsReleaseNewerThanCurrent(latestStableRelease, currentRelease))
                {
                    ShowUpdateAvailableDialog("stable", latestStableRelease.DisplayVersion, latestStableRelease.Release.HtmlUrl, false, latestStableRelease.Release.TagName);
                }
                else if (checkForBetaUpdates && latestBetaRelease != null && !IsReleaseNewerThanCurrent(latestBetaRelease, currentRelease))
                {
                    _logger.Warn($"Running latest beta version: {currentRelease.DisplayVersion}");
                }
                else if (!currentRelease.IsPrerelease && latestStableRelease.Version == currentVersion)
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

        public static void ShowUpdateAvailableDialog(string branch, string version, string url, bool beta, string releaseTag)
        {
            _logger.Warn($"New {branch} version available on GitHub: {version}");

            var result = MessageBox.Show($"New {branch} version {version} available!\n\nDo you want to auto update? \n\nYes - Launch Auto Update \n\nNo - Manual update - launches browser\n\nCancel - ignores the update",
                "Update available", MessageBoxButton.YesNoCancel, MessageBoxImage.Information);

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
    
                startInfo.Arguments = BuildUpdaterArguments(beta, releaseTag);
              
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
                    Arguments = BuildUpdaterArguments(beta, releaseTag)
                };

                Process.Start(startInfo);
            }
        }

        private static string BuildUpdaterArguments(bool beta, string releaseTag)
        {
            var arguments = beta ? "-beta" : string.Empty;

            if (!string.IsNullOrWhiteSpace(releaseTag))
            {
                if (!string.IsNullOrWhiteSpace(arguments))
                {
                    arguments += " ";
                }

                arguments += "-tag " + QuoteArgument(releaseTag);
            }

            return arguments;
        }

        private static string QuoteArgument(string argument)
        {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
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

        private static bool IsBetaUpdateAvailable(ReleaseInfo betaRelease, ReleaseInfo stableRelease, ReleaseInfo currentRelease)
        {
            return IsReleaseNewerThanCurrent(betaRelease, currentRelease) &&
                   (betaRelease.Version > stableRelease.Version || !IsReleaseNewerThanCurrent(stableRelease, currentRelease));
        }

        private static bool IsReleaseNewerThanCurrent(ReleaseInfo candidate, ReleaseInfo current)
        {
            if (candidate == null || current == null)
            {
                return false;
            }

            if (candidate.Version > current.Version)
            {
                return true;
            }

            if (candidate.Version < current.Version)
            {
                return false;
            }

            if (candidate.IsPrerelease == current.IsPrerelease)
            {
                return string.CompareOrdinal(candidate.DisplayVersion, current.DisplayVersion) > 0;
            }

            return current.IsPrerelease && !candidate.IsPrerelease;
        }

        private static ReleaseInfo GetNewerRelease(ReleaseInfo current, ReleaseInfo candidate)
        {
            if (current == null ||
                candidate.Version > current.Version ||
                (candidate.Version == current.Version && string.CompareOrdinal(candidate.Release.TagName, current.Release.TagName) > 0))
            {
                return candidate;
            }

            return current;
        }

        private static bool TryCreateReleaseInfo(Release release, out ReleaseInfo releaseInfo)
        {
            releaseInfo = null;

            if (release == null || release.Draft)
            {
                return false;
            }

            Version version;
            if (!TryParseReleaseVersion(release.TagName, out version))
            {
                return false;
            }

            releaseInfo = new ReleaseInfo
            {
                Release = release,
                Version = version,
                DisplayVersion = GetDisplayVersion(release.TagName, version),
                IsPrerelease = release.Prerelease
            };
            return true;
        }

        private static ReleaseInfo CreateCurrentReleaseInfo()
        {
            Version version;
            if (!TryParseReleaseVersion(RELEASE_TAG, out version))
            {
                version = Version.Parse(VERSION);
            }

            return new ReleaseInfo
            {
                Version = version,
                DisplayVersion = GetDisplayVersion(RELEASE_TAG, version),
                IsPrerelease = RELEASE_TAG.IndexOf("-", StringComparison.Ordinal) >= 0
            };
        }

        private static bool TryParseReleaseVersion(string tagName, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return false;
            }

            var normalized = tagName.Trim().TrimStart('v', 'V');
            if (Version.TryParse(normalized, out version))
            {
                return true;
            }

            var suffixIndex = normalized.IndexOfAny(new[] {'-', '+'});
            if (suffixIndex > 0)
            {
                normalized = normalized.Substring(0, suffixIndex);
            }

            return Version.TryParse(normalized, out version);
        }

        private static string GetDisplayVersion(string tagName, Version version)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                return version.ToString();
            }

            return tagName.Trim().TrimStart('v', 'V');
        }

        private class ReleaseInfo
        {
            public Release Release { get; set; }
            public Version Version { get; set; }
            public string DisplayVersion { get; set; }
            public bool IsPrerelease { get; set; }
        }
    }
}
