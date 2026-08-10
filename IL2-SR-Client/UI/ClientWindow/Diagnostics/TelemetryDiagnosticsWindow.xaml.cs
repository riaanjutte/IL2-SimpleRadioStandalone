using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NLog;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.UI.ClientWindow.Diagnostics
{
    public partial class TelemetryDiagnosticsWindow : Window
    {
        private readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public TelemetryDiagnosticsWindow()
        {
            InitializeComponent();
        }

        private async void RunTelemetryDiagnostics_OnClick(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
            }

            TelemetryDiagnosticsOutput.Text = "Checking IL-2 telemetry configuration...";

            try
            {
                string report = await Task.Run(() => TelemetryDiagnosticsService.CreateDefault().BuildReportText());
                TelemetryDiagnosticsOutput.Text = report;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to run telemetry diagnostics");
                TelemetryDiagnosticsOutput.Text = "Telemetry diagnostics failed: " + ex.Message;
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                }
            }
        }

        private async void ConfigureIL2WinWing_OnClick(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "This will back up IL2WinWing.dll.config, configure IL2WinWing to receive telemetry on port 29373, configure its standard SimApp Pro port 16536, and add the IL2WinWing telemetry endpoint to every detected IL-2 startup.cfg.\n\nClose IL-2 before continuing. If more than one IL2WinWing copy exists, leave the copy you use running so SRS can identify it. Restart IL2WinWing after the repair.",
                "Configure IL2WinWing compatibility",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes)
            {
                return;
            }

            Button button = sender as Button;
            if (button != null)
            {
                button.IsEnabled = false;
            }

            TelemetryDiagnosticsOutput.Text = "Configuring IL2WinWing compatibility...";

            try
            {
                IL2WinWingCompatibilityRepairResult result = await Task.Run(() =>
                    IL2WinWingCompatibilityRepair.Repair(
                        TelemetryDiagnosticsService.BuildContexts(),
                        () => !TelemetryDiagnosticsService.IsIL2Running(),
                        message => Logger.Info(message)));

                string report = await Task.Run(() => TelemetryDiagnosticsService.CreateDefault().BuildReportText());
                TelemetryDiagnosticsOutput.Text = result.Message + Environment.NewLine + Environment.NewLine + report;

                MessageBox.Show(
                    this,
                    result.Message,
                    "Configure IL2WinWing compatibility",
                    MessageBoxButton.OK,
                    result.Succeeded ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to configure IL2WinWing compatibility");
                TelemetryDiagnosticsOutput.Text = "IL2WinWing compatibility configuration failed: " + ex.Message;
            }
            finally
            {
                if (button != null)
                {
                    button.IsEnabled = true;
                }
            }
        }

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
