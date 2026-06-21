using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Localization;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Settings;
using Ciribob.IL2.SimpleRadio.Standalone.Client.Utils;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Targets.Wrappers;
using Sentry;

namespace IL2_SR_Client
{
    /// <summary>
    ///     Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool loggingReady = false;
        private static Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly string[] FlattenableBrushKeys =
        {
            "MilPanelFaceBrush",
            "MilBtnDarkFaceBrush",
            "MilBtnDarkHoverBrush",
            "MilBtnDarkPressedBrush",
            "MilBtnBrassFaceBrush",
            "MilBtnBrassHoverBrush",
            "MilBtnBrassPressedBrush",
            "MilScrewBrush",
            "MilLampBezelBrush",
            "OverlayEquipmentPanelBrush",
            "OverlayRaisedControlBrush",
            "OverlayPressedControlBrush",
            "OverlayAccentRaisedBrush"
        };

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ApplyUiTheme();
        }

        /// <summary>
        /// Swaps the default bakelite palette for the selected variant. Theme brushes
        /// are consumed through DynamicResource so open windows can repaint without a
        /// client restart.
        /// </summary>
        public static void ApplyUiTheme()
        {
            try
            {
                var theme = GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.Theme).RawValue;
                var palette = SelectedPalette(theme);
                var resources = Current.Resources;
                ClearFlattenedBrushOverrides(resources);

                if (palette == null)
                {
                    EnsurePalette(resources, "Themes/MilitaryPalette.xaml");
                    ApplyThreeDEffectSetting();
                    ApplyVuMeterStyle();
                    return; // bakelite is the default, already merged via App.xaml
                }

                EnsurePalette(resources, palette);
                ApplyThreeDEffectSetting();
                ApplyVuMeterStyle();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to apply UI theme, falling back to default");
            }
        }

        private static void EnsurePalette(ResourceDictionary resources, string palette)
        {
            var dictionaries = resources.MergedDictionaries;
            for (var i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (IsThemePaletteDictionary(source))
                {
                    dictionaries[i] = new ResourceDictionary
                    {
                        Source = new Uri(palette, UriKind.Relative)
                    };
                    return;
                }
            }

            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(palette, UriKind.Relative)
            });
        }

        private static bool IsThemePaletteDictionary(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return false;
            }

            var normalizedSource = source.Replace('\\', '/');
            return normalizedSource.IndexOf("MilitaryPalette", StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedSource.IndexOf("/MilitaryPalette", StringComparison.OrdinalIgnoreCase) >= 0
                   || normalizedSource.IndexOf("Themes/MilitaryPalette", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static void ApplyThreeDEffectSetting()
        {
            ClearFlattenedBrushOverrides(Current.Resources);
            var effectsEnabled = GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.ThreeDEffectsEnabled);
            Current.Resources["ThreeDEffectOpacity"] = effectsEnabled ? 1.0 : 0.0;
            Current.Resources["ThreeDGlassOpacity"] = effectsEnabled ? 1.0 : 0.0;
            Current.Resources["ThreeDScrewOpacity"] = effectsEnabled ? 1.0 : 0.0;
            Current.Resources["ThreeDHighlightBrush"] = effectsEnabled
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38FFF6DC"))
                : Brushes.Transparent;
            Current.Resources["ThreeDSoftHighlightBrush"] = effectsEnabled
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2BFFF6DC"))
                : Brushes.Transparent;
            Current.Resources["ThreeDDarkRimBrush"] = effectsEnabled
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15100A"))
                : BrushFromResource("MilBorderBrush", Brushes.Black);

            if (effectsEnabled)
            {
                return;
            }

            foreach (var key in FlattenableBrushKeys)
            {
                var flatBrush = FlattenBrush(Current.Resources[key] as Brush);
                if (flatBrush != null)
                {
                    Current.Resources[key] = flatBrush;
                }
            }
        }

        public static void ApplyVuMeterStyle()
        {
            var style = NormalizeThemeName(GlobalSettingsStore.Instance.GetClientSetting(GlobalSettingsKeys.VuMeterStyle).RawValue);
            if (style != "light" && style != "dark")
            {
                Current.Resources.Remove("MilVuFaceBrush");
                Current.Resources.Remove("MilVuMarkBrush");
                Current.Resources.Remove("MilVuNeedleBrush");
                return;
            }

            var useDarkMeter = style == "dark";

            Current.Resources["MilVuFaceBrush"] = useDarkMeter
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#221D14"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E7C8"));
            Current.Resources["MilVuMarkBrush"] = useDarkMeter
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3E7C8"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A251C"));
            Current.Resources["MilVuNeedleBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D33124"));
        }

        private static void ClearFlattenedBrushOverrides(ResourceDictionary resources)
        {
            foreach (var key in FlattenableBrushKeys)
            {
                resources.Remove(key);
            }
        }

        private static Brush BrushFromResource(string key, Brush fallback)
        {
            return Current.Resources[key] as Brush ?? fallback;
        }

        private static SolidColorBrush FlattenBrush(Brush brush)
        {
            var solidBrush = brush as SolidColorBrush;
            if (solidBrush != null)
            {
                return new SolidColorBrush(solidBrush.Color);
            }

            var gradientBrush = brush as GradientBrush;
            if (gradientBrush == null || gradientBrush.GradientStops.Count == 0)
            {
                return null;
            }

            return new SolidColorBrush(gradientBrush.GradientStops[gradientBrush.GradientStops.Count / 2].Color);
        }

        private static string SelectedPalette(string theme)
        {
            var normalizedTheme = NormalizeThemeName(theme);

            if (normalizedTheme == "grey")
            {
                return "Themes/MilitaryPaletteGrey.xaml";
            }

            if (normalizedTheme == "rafgreengrey" || normalizedTheme == "rafgreygreen")
            {
                return "Themes/MilitaryPaletteRafGreyGreen.xaml";
            }

            if (normalizedTheme == "usaafolivedrab" || normalizedTheme == "olivedrab")
            {
                return "Themes/MilitaryPaletteUsaafOliveDrab.xaml";
            }

            if (normalizedTheme == "luftwafferlm66" || normalizedTheme == "rlm66")
            {
                return "Themes/MilitaryPaletteLuftwaffeRlm66.xaml";
            }

            if (normalizedTheme == "sovieta14steelgrey" || normalizedTheme == "a14steelgrey" || normalizedTheme == "sovietsteelgrey")
            {
                return "Themes/MilitaryPaletteSovietA14SteelGrey.xaml";
            }

            if (normalizedTheme == "ivory")
            {
                return "Themes/MilitaryPaletteIvory.xaml";
            }

            if (normalizedTheme == "white")
            {
                return "Themes/MilitaryPaletteWhite.xaml";
            }

            return null;
        }

        private static string NormalizeThemeName(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(theme.Length);
            foreach (var character in theme)
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString();
        }

        public App()
        {
            SentrySdk.Init("https://602501536e994652b8c7a3d3a399ffd2@o414743.ingest.sentry.io/5315044");
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionHandler);

            var location = AppDomain.CurrentDomain.BaseDirectory;
            //var location = Assembly.GetExecutingAssembly().Location;

            //check for opus.dll
            if (!File.Exists(location + "\\opus.dll"))
            {
                MessageBox.Show(
                    $"You are missing the opus.dll - Reinstall using the Installer and don't move the client from the installation directory!",
                    "Installation Error!", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            }
            if (!File.Exists(location + "\\speexdsp.dll"))
            {

                MessageBox.Show(
                    $"You are missing the speexdsp.dll - Reinstall using the Installer and don't move the client from the installation directory!",
                    "Installation Error!", MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            }

            SetupLogging();

            ListArgs();
            LocalizationManager.Initialize(GlobalSettingsStore.Instance);

#if !DEBUG
            if (IsClientRunning())
            {
                //check environment flag

                var args = Environment.GetCommandLineArgs();
                var allowMultiple = false;

                foreach (var arg in args)
                {
                    if (arg.Contains("-allowMultiple"))
                    {
                        //restart flag to promote to admin
                        allowMultiple = true;
                    }
                }

                if (GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.AllowMultipleInstances) || allowMultiple)
                {
                    Logger.Warn("Another SRS instance is already running, allowing multiple instances due to config setting");
                }
                else
                {
                    Logger.Warn("Another SRS instance is already running, preventing second instance startup");

                    MessageBoxResult result = MessageBox.Show(
                    "Another instance of the SimpleRadio client is already running!\n\nThis one will now quit. Check your system tray for the SRS Icon",
                    "Multiple SimpleRadio clients started!",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);


                    Environment.Exit(0);
                    return;
                }
            }
#endif

            if (RequireAdmin())
            {
                return;
            }

            VerifyIL2StartupTelemetry();

            InitNotificationIcon();

        }

        private void ListArgs()
        {
            Logger.Info("Arguments:");
            var args = Environment.GetCommandLineArgs();
            foreach (var s in args)
            {
                Logger.Info(s);
            }
        }

        private bool RequireAdmin()
        {
            if (!GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin))
            {
                return false;
            }
            
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = principal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!hasAdministrativeRight && GlobalSettingsStore.Instance.GetClientSettingBool(GlobalSettingsKeys.RequireAdmin))
            {
                Task.Factory.StartNew(() =>
                {
                    var location = AppDomain.CurrentDomain.BaseDirectory;

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = "\"" + location + "\"",
                        FileName = "IL2-SR-ClientRadio.exe",
                        Verb = "runas",
                        Arguments = GetArgsString() + " -allowMultiple"
                    };
                    try
                    {
                        Process p = Process.Start(startInfo);

                        //shutdown this process as another has started
                        Dispatcher?.BeginInvoke(new Action(() =>
                        {
                            if (_notifyIcon != null)
                                _notifyIcon.Visible = false;

                            Environment.Exit(0);
                        }));
                    }
                    catch (System.ComponentModel.Win32Exception ex)
                    {
                        MessageBox.Show(
                                "SRS Requires admin rights to be able to read keyboard input in the background. \n\nIf you do not use any keyboard binds for SRS and want to stop this message - Disable Require Admin Rights in SRS Settings\n\nSRS will continue without admin rights but keyboard binds will not work!",
                                "UAC Error", MessageBoxButton.OK, MessageBoxImage.Warning);

                    }
                });

                return true;
            }

            return false;
        }

        private void VerifyIL2StartupTelemetry()
        {
            string il2Path = ReadInstallerPath("IL2Path");
            if (string.IsNullOrWhiteSpace(il2Path))
            {
                Logger.Info("No saved IL-2 path found; skipping startup.cfg telemetry check");
                return;
            }

            string cfgPath = Path.Combine(il2Path, "data", "startup.cfg");
            if (!File.Exists(cfgPath))
            {
                Logger.Warn($"Saved IL-2 path does not contain data\\startup.cfg; skipping telemetry check. Path: {il2Path}");
                return;
            }

            try
            {
                bool repaired = StartupConfigTelemetry.EnsureEnabled(cfgPath, message => Logger.Info(message));
                if (repaired)
                {
                    Logger.Info($"Repaired IL-2 startup.cfg telemetry settings at {cfgPath}");
                }
                else
                {
                    Logger.Info($"Verified IL-2 startup.cfg telemetry settings at {cfgPath}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unable to verify or repair IL-2 startup.cfg telemetry settings at {cfgPath}");
                MessageBox.Show(
                    "SRS could not verify or repair the IL-2 telemetry settings in startup.cfg.\n\n" +
                    "Auto-connect and in-game radio data may not work until startup.cfg contains an enabled telemetrydevice section for 127.0.0.1:4322.\n\n" +
                    "Try running SRS as administrator, or reinstall/repair using the installer.",
                    "IL2-SRS Telemetry Check",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private string ReadInstallerPath(string key)
        {
            try
            {
                return (string)Registry.GetValue("HKEY_CURRENT_USER\\SOFTWARE\\IL2-SRS", key, "");
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unable to read installer registry path {key}");
                return "";
            }
        }

        private string GetArgsString()
        {
            StringBuilder builder = new StringBuilder();
            var args = Environment.GetCommandLineArgs();
            foreach (var s in args)
            {
                if (builder.Length > 0)
                {
                    builder.Append(" ");
                }

                if (s.Contains("-cfg="))
                {
                    var str = s.Replace("-cfg=", "-cfg=\"");

                    builder.Append(str);
                    builder.Append("\"");
                }
                else if (s.Contains("IL2-SR-ClientRadio.exe"))
                {
                    ///ignore
                }
                else
                {
                    builder.Append(s);
                }
            }

            return builder.ToString();
        }

        private bool IsClientRunning()
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

        /* 
         * Changes to the logging configuration in this method must be replicated in
         * this VS project's NLog.config file
         */
        private void SetupLogging()
        {
            // If there is a configuration file then this will already be set
            if(LogManager.Configuration != null)
            {
                loggingReady = true;
                return;
            }

            var config = new LoggingConfiguration();
            var fileTarget = new FileTarget
            {
                FileName = "clientlog.txt",
                ArchiveFileName = "clientlog.old.txt",
                MaxArchiveFiles = 1,
                ArchiveAboveSize = 104857600,
                Layout =
                @"${longdate} | ${logger} | ${message} ${exception:format=toString,Data:maxInnerExceptionLevel=1}"
            };

            var wrapper = new AsyncTargetWrapper(fileTarget, 5000, AsyncTargetWrapperOverflowAction.Discard);
            config.AddTarget("asyncFileTarget", wrapper);

#if DEBUG
            config.LoggingRules.Add( new LoggingRule("*", LogLevel.Debug, wrapper));
#else
            config.LoggingRules.Add( new LoggingRule("*", LogLevel.Info, wrapper));
#endif
            LogManager.Configuration = config;
            loggingReady = true;

            Logger = LogManager.GetCurrentClassLogger();
        }


        private void InitNotificationIcon()
        {
            if(_notifyIcon != null)
            {
                return;
            }
            System.Windows.Forms.MenuItem notifyIconContextMenuShow = new System.Windows.Forms.MenuItem
            {
                Index = 0,
                Text = LocalizationManager.Get("Show")
            };
            notifyIconContextMenuShow.Click += new EventHandler(NotifyIcon_Show);

            System.Windows.Forms.MenuItem notifyIconContextMenuQuit = new System.Windows.Forms.MenuItem
            {
                Index = 1,
                Text = LocalizationManager.Get("Quit")
            };
            notifyIconContextMenuQuit.Click += new EventHandler(NotifyIcon_Quit);

            System.Windows.Forms.ContextMenu notifyIconContextMenu = new System.Windows.Forms.ContextMenu();
            notifyIconContextMenu.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { notifyIconContextMenuShow, notifyIconContextMenuQuit });

            _notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = Ciribob.IL2.SimpleRadio.Standalone.Client.Properties.Resources.audio_headset,
                Visible = true
            };
            _notifyIcon.ContextMenu = notifyIconContextMenu;
            _notifyIcon.DoubleClick += new EventHandler(NotifyIcon_Show);

        }

        private void NotifyIcon_Show(object sender, EventArgs args)
        {
            MainWindow.Show();
            MainWindow.WindowState = WindowState.Normal;
        }

        private void NotifyIcon_Quit(object sender, EventArgs args)
        {
            MainWindow.Close();

        }

        protected override void OnExit(ExitEventArgs e)
        {
            if(_notifyIcon !=null)
                _notifyIcon.Visible = false;
            base.OnExit(e);
        }

        private void UnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs e)
        {
            if (loggingReady)
            {
                Logger logger = LogManager.GetCurrentClassLogger();
                logger.Error((Exception) e.ExceptionObject, "Received unhandled exception, {0}", e.IsTerminating ? "exiting" : "continuing");
            }
        }
    }
}
