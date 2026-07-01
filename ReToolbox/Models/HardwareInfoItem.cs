namespace ReToolbox.Models
{
    public sealed class HardwareInfoItem
    {
        public required string Label { get; init; }

        public required string Value { get; set; }

        public string? BrandKey { get; set; }

        public bool IsVerified { get; set; }
    }
}
