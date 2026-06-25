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

        private void Close_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
