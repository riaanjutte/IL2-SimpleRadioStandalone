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
using Ciribob.IL2.SimpleRadio.Standalone.Common.Network;
using Microsoft.Win32;
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
        private const string InstallRegistryPath = "HKEY_CURRENT_USER\\SOFTWARE\\IL2-SRS";
        private const string InstallRegistryValue = "SRSPath";
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
            var githubClient = new GitHubClient(new ProductHeaderValue(ReleaseMetadata.GithubUserAgent, ReleaseMetadata.Version));

            var updaterArguments = UpdaterArguments.Parse(Environment.GetCommandLineArgs());
            var targetTag = updaterArguments.ReleaseTag;
            var localRelease = GetLocalInstalledReleaseInfo();
            bool allowBeta = updaterArguments.Beta || (localRelease != null && localRelease.IsPrerelease);

            var releases = await githubClient.Repository.Release.GetAll(ReleaseMetadata.GithubUsername, ReleaseMetadata.GithubRepository);
            var releaseCandidates = releases.Select(ToUpdateReleaseCandidate).ToList();
            var latestRelease = UpdateReleaseSelector.SelectAutoUpdaterDownload(
                releaseCandidates,
                localRelease,
                allowBeta,
                targetTag);

            if (latestRelease == null)
            {
                return null;
            }
            
            changelogURL = latestRelease.HtmlUrl;
            Status.Content = (latestRelease.IsPrerelease ? "Downloading Beta Version " : "Downloading Version ") + latestRelease.DisplayVersion;
            return new Uri(latestRelease.AssetDownloadUrl);
        }

        private static UpdateReleaseInfo GetLocalInstalledReleaseInfo()
        {
            foreach (var clientPath in GetPossibleInstalledClientPaths())
            {
                Version clientVersion;
                if (TryGetProductVersion(clientPath, out clientVersion))
                {
                    Version updaterVersion;
                    if (UpdateReleaseSelector.TryParseReleaseVersion(ReleaseMetadata.Version, out updaterVersion) && clientVersion == updaterVersion)
                    {
                        return UpdateReleaseSelector.CreateCurrentReleaseInfo(ReleaseMetadata.Version, ReleaseMetadata.ReleaseTag);
                    }

                    return UpdateReleaseSelector.CreateCurrentReleaseInfo(clientVersion.ToString(), clientVersion.ToString());
                }
            }

            return null;
        }

        private static IEnumerable<string> GetPossibleInstalledClientPaths()
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            yield return Path.Combine(baseDirectory, "IL2-SR-ClientRadio.exe");

            var registryInstallPath = ReadInstalledPath();
            if (!string.IsNullOrWhiteSpace(registryInstallPath))
            {
                yield return Path.Combine(registryInstallPath, "IL2-SR-ClientRadio.exe");
            }
        }

        private static string ReadInstalledPath()
        {
            try
            {
                return Registry.GetValue(InstallRegistryPath, InstallRegistryValue, "") as string;
            }
            catch
            {
                return "";
            }
        }

        private static bool TryGetProductVersion(string filePath, out Version version)
        {
            version = null;
            if (!File.Exists(filePath))
            {
                return false;
            }

            return Version.TryParse(FileVersionInfo.GetVersionInfo(filePath).ProductVersion, out version);
        }

        public void ShowNoUpdateAvailable()
        {
            MessageBox.Show("No newer IL2-SRS update is available.",
                "SRS Auto Updater",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Close();
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
                    ShowNoUpdateAvailable();
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
