namespace ReToolbox.Models
{
    // Battery health snapshot parsed from "powercfg /batteryreport /xml". The
    // design capacity is what the cell held when new; the full-charge capacity
    // is what it can hold now, so their ratio is the effective health percent.
    public sealed class BatteryHealth
    {
        public string Id { get; set; } = string.Empty;

        public string Manufacturer { get; set; } = string.Empty;

        // mWh the battery held when new.
        public int DesignCapacityMwh { get; set; }

        // mWh the battery can hold right now (after wear).
        public int FullChargeCapacityMwh { get; set; }

        // Charge/discharge cycles counted by the battery controller.
        public int CycleCount { get; set; }

        // FullCharge / Design * 100, clamped to [0,100].
        public double HealthPercent =>
            DesignCapacityMwh > 0
                ? System.Math.Clamp((double)FullChargeCapacityMwh / DesignCapacityMwh * 100.0, 0, 100)
                : 0;

        // True when at least one capacity figure was readable.
        public bool IsValid => DesignCapacityMwh > 0 || FullChargeCapacityMwh > 0;

        public string Summary =>
            IsValid
                ? $"满电 {FullChargeCapacityMwh / 1000d:F1} Wh / 设计 {DesignCapacityMwh / 1000d:F1} Wh · {CycleCount} 次循环"
                : "未检测到电池";
    }
}
