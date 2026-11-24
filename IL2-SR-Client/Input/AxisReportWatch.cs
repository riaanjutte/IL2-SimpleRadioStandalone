using System.Linq;
using System.Runtime.CompilerServices;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Settings
{
    /// <summary>
    /// Until a device sends its first report, DirectInput gives it centred axes that are not its real position.
    /// Some devices, such as the X-56 throttle, only report once one of their controls moves, so reading a volume
    /// axis straight away would set the volume to 50%. A device counts as reporting once any axis differs from
    /// its first reading. Rediscovery creates a new device object, which waits for its own first report.
    /// </summary>
    internal sealed class AxisReportWatch<TDevice> where TDevice : class
    {
        private readonly ConditionalWeakTable<TDevice, DeviceReport> _reports =
            new ConditionalWeakTable<TDevice, DeviceReport>();

        public bool HasReported(TDevice device, int[] axes)
        {
            DeviceReport report;
            if (!_reports.TryGetValue(device, out report))
            {
                _reports.Add(device, new DeviceReport(axes));
                return false;
            }

            if (!report.HasReported && !report.FirstReading.SequenceEqual(axes))
            {
                report.HasReported = true;
            }

            return report.HasReported;
        }

        private sealed class DeviceReport
        {
            public DeviceReport(int[] firstReading)
            {
                FirstReading = firstReading;
            }

            public int[] FirstReading { get; }

            public bool HasReported { get; set; }
        }
    }
}
