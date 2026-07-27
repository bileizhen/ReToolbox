using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Xml.Linq;
using ReToolbox.Models;

namespace ReToolbox.Services
{
    // Reads battery presence, current charge state and health information.
    // Health is derived from "powercfg /batteryreport /xml": design capacity is
    // what the battery held when new, while full-charge capacity is what it can
    // hold now. Their ratio is the effective health percent.
    public class BatteryService
    {
        // True when Windows reports at least one battery. Desktops without a
        // battery use this to hide the battery-health section entirely.
        public bool HasBattery()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2", "SELECT * FROM Win32_Battery");
                return searcher.Get().Count > 0;
            }
            catch
            {
                return false;
            }
        }

        // Builds a health snapshot from powercfg's XML report. The generated XML
        // has a default namespace, so elements are matched by LocalName rather
        // than unqualified XName values.
        public BatteryHealth GetBatteryHealth()
        {
            var health = new BatteryHealth();
            string reportPath = Path.Combine(Path.GetTempPath(), "ReToolbox_batteryreport.xml");

            try
            {
                using var process = new Process();
                process.StartInfo.FileName = "cmd.exe";
                process.StartInfo.Arguments =
                    $"/c chcp 65001 >nul & powercfg /batteryreport /output \"{reportPath}\" /xml";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                process.Start();
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0 || !File.Exists(reportPath)) return health;

                var doc = XDocument.Load(reportPath);
                var battery = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "Battery");
                if (battery == null) return health;

                static string? Value(XElement parent, string localName) =>
                    parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

                health.Id = Value(battery, "Id") ?? string.Empty;
                health.Manufacturer = Value(battery, "Manufacturer") ?? string.Empty;
                health.DesignCapacityMwh = ParseIntSafe(Value(battery, "DesignCapacity"));
                health.FullChargeCapacityMwh = ParseIntSafe(Value(battery, "FullChargeCapacity"));
                health.CycleCount = ParseIntSafe(Value(battery, "CycleCount"));
            }
            catch
            {
                // IsValid remains false and the UI reports that information is unavailable.
            }
            finally
            {
                try { if (File.Exists(reportPath)) File.Delete(reportPath); } catch { }
            }

            return health;
        }

        // Current charge level and charging state from Win32_Battery.
        public (int Percent, bool IsCharging) GetChargeStatus()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2", "SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery");
                foreach (ManagementObject obj in searcher.Get())
                {
                    using (obj)
                    {
                        int percent = Convert.ToInt32(
                            obj["EstimatedChargeRemaining"], CultureInfo.InvariantCulture);
                        // BatteryStatus 2 means external power/charging.
                        bool charging = Convert.ToInt32(
                            obj["BatteryStatus"], CultureInfo.InvariantCulture) == 2;
                        return (percent, charging);
                    }
                }
            }
            catch
            {
            }

            return (0, false);
        }

        private static int ParseIntSafe(string? value) =>
            int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                ? n
                : 0;
    }
}
