namespace ReToolbox.Models
{
    // A Windows power scheme as reported by "powercfg /list". Each entry has a
    // stable GUID (used by all powercfg operations) and a display name; the
    // active scheme is marked so the UI can badge it.
    public sealed class PowerPlan
    {
        public string Guid { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public string Summary => $"{Name}{(IsActive ? "（当前）" : "")}";
    }
}
