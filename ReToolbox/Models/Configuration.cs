using Microsoft.UI.Xaml.Controls;

namespace ReToolbox.Models
{
    public class Configuration
    {
        public string Name { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public FontIcon? Icon { get; set; }
    }
}
