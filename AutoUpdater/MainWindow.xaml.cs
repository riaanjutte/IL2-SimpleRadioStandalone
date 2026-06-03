using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Octokit;
using MessageBox = System.Windows.MessageBox;
using Path = System.IO.Path;

namespace AutoUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static readonly string GITHUB_USERNAME = "riaanjutte";
        public static readonly string GITHUB_REPOSITORY = "IL2-SimpleRadioStandalone";
        // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
        public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";
        private Uri _uri;
        private string _directory;
        private string _file;
        private bool _cancel = false;
        private DispatcherTimer _progressCheckTimer;
        private double _lastValue = -1;

        private string changelogURL = "";

        public MainWindow()
        {
            InitializeComponent();
            QuitSimpleRadio();
            if (IsAnotherRunning())
            {
                MessageBox.Show("Please close IL2-SimpleRadio Standalone before running", "SRS Auto Updater",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(0);

                return;
            }

            try
            {
                DownloadLatestVersion();
            }
            catch (Exception ex)
            {
                ShowError();
            }
            
        }

        private void QuitSimpleRadio()
        {
            foreach (var clsProcess in Process.GetProcesses())
            {
                if (clsProcess.ProcessName.ToLower().Trim().StartsWith("il2-sr-"))
                {
                    clsProcess.Kill();
                    clsProcess.WaitForExit(5000);
                    clsProcess.Dispose();
                }
            }
        }

        private bool IsAnotherRunning()
        {
            Process currentProcess = Process.GetCurrentProcess();
            string currentProcessName = currentProcess.ProcessName.ToLower().Trim();

            foreach (Process clsProcess in Process.GetProcesses())
            {
                if (clsProcess.Id != currentProcess.Id &&
                    clsProcess.ProcessName.ToLower().Trim() == currentProcessName)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<Uri> GetPathToLatestVersion()
        {
            Status.Content = "Finding Latest IL2-SRS Version";
            var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, "1.0.4.4"));

            var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);

            bool allowBeta = AllowBeta();
            ReleaseInfo latestRelease = null;

            // GitHub API order is not a version contract, so select the highest valid semver tag explicitly.
            foreach (Release release in releases)
            {
                ReleaseInfo releaseInfo;
                if (!TryCreateReleaseInfo(release, allowBeta, out releaseInfo))
                {
                    continue;
                }

                latestRelease = GetNewerRelease(latestRelease, releaseInfo);
            }

            if (latestRelease == null)
            {
                return null;
            }

            changelogURL = latestRelease.Release.HtmlUrl;
            Status.Content = (latestRelease.IsPrerelease ? "Downloading Beta Version " : "Downloading Version ") + latestRelease.DisplayVersion;
            return new Uri(latestRelease.Asset.BrowserDownloadUrl);
        }

        private static ReleaseInfo GetNewerRelease(ReleaseInfo current, ReleaseInfo candidate)
        {
            if (current == null ||
                candidate.Version > current.Version ||
                (candidate.Version == current.Version && current.IsPrerelease && !candidate.IsPrerelease) ||
                (candidate.Version == current.Version &&
                 candidate.IsPrerelease == current.IsPrerelease &&
                 string.CompareOrdinal(candidate.Release.TagName, current.Release.TagName) > 0))
            {
                return candidate;
            }

            return current;
        }

        private static bool TryCreateReleaseInfo(Release release, bool allowBeta, out ReleaseInfo releaseInfo)
        {
            releaseInfo = null;

            if (release == null || release.Draft || (release.Prerelease && !allowBeta))
            {
                return false;
            }

            Version releaseVersion;
            if (!TryParseReleaseVersion(release.TagName, out releaseVersion))
            {
                return false;
            }

            var asset = release.Assets.FirstOrDefault(IsReleaseZipAsset);
            if (asset == null)
            {
                return false;
            }

            releaseInfo = new ReleaseInfo
            {
                Release = release,
                Asset = asset,
                Version = releaseVersion,
                DisplayVersion = GetDisplayVersion(release.TagName, releaseVersion),
                IsPrerelease = release.Prerelease
            };
            return true;
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
            public ReleaseAsset Asset { get; set; }
            public Version Version { get; set; }
            public string DisplayVersion { get; set; }
            public bool IsPrerelease { get; set; }
        }

        private static bool IsReleaseZipAsset(ReleaseAsset asset)
        {
            return asset.Name.StartsWith("IL2-SimpleRadioStandalone", StringComparison.OrdinalIgnoreCase)
                   && asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        private bool AllowBeta()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.Trim().Equals("-beta", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                
            }

            return false;

        }

        public void ShowError()
        {
            MessageBox.Show("Error Auto Updating IL2-SRS - Please check internet connection and try again \n\nAlternatively: \n1. Download the latest IL2-SimpleRadioStandalone.zip from the SRS Github Release page\n2. Extract all the files to a temporary directory\n3. Run the installer.",
                "Auto Updater Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Close();
        }

        public async void DownloadLatestVersion()
        {
            try
            {
                _uri = await GetPathToLatestVersion();
                if (_uri == null)
                {
                    ShowError();
                    return;
                }

                _directory = GetTemporaryDirectory();
                _file = _directory + "\\temp.zip";

                using (WebClient wc = new MyWebClient())
                {

                    wc.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)");
                    wc.DownloadProgressChanged += DownloadProgressChanged;
                    wc.DownloadFileAsync(_uri, _file);
                    wc.DownloadFileCompleted += DownloadComplete;

                    //check download progress periodically - if the download is stalled we dont get told by anything
                    _progressCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                    _progressCheckTimer.Tick += CheckProgress;
                    _progressCheckTimer.Start();

                }
            }
            catch (Exception ex)
            {
               ShowError();
            }
        }

        private void CheckProgress(object sender, EventArgs e)
        {
            if (_lastValue == DownloadProgress.Value)
            {
                //no progress
                ShowError();
            }

            _lastValue = DownloadProgress.Value;


        }

        private void DownloadComplete(object sender, AsyncCompletedEventArgs e)
        {
            if (!_cancel)
            {
                ZipFile.ExtractToDirectory(_file, Path.Combine(_directory, "extract"));

                Thread.Sleep(400);

                ProcessStartInfo procInfo = new ProcessStartInfo();
                procInfo.WorkingDirectory = Path.Combine(_directory, "extract"); 
                procInfo.Arguments = "-autoupdate";
                procInfo.FileName = Path.Combine(Path.Combine(_directory, "extract"), "installer.exe");
                procInfo.UseShellExecute = false;
                Process.Start(procInfo);
            } 
            
            Close();
        }

        public string GetTemporaryDirectory()
        {
            string tempFolder = Path.GetTempFileName();
            File.Delete(tempFolder);
            Directory.CreateDirectory(tempFolder);

            return tempFolder;
        }

        private void DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            DownloadProgress.Value = e.ProgressPercentage;
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            _cancel = true;
            Close();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            _cancel = true;
            _progressCheckTimer?.Stop();
        }
    }
}
