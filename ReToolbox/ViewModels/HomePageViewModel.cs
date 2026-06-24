using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ReToolbox.ViewModels
{
    public partial class HomePageViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _welcomeText = "欢迎使用 ReToolbox";

        [ObservableProperty]
        private string _subtitleText = "快速配置你的 Windows 系统";

        public ObservableCollection<QuickActionItem> QuickActions { get; }

        public HomePageViewModel()
        {
            QuickActions = new ObservableCollection<QuickActionItem>
            {
                new() { Title = "安装软件", Description = "批量安装常用软件", IconGlyph = "\uE896", TargetPage = "SoftwarePage" },
                new() { Title = "系统激活", Description = "一键激活 Windows", IconGlyph = "\uE8F1", TargetPage = "ActivationPage" },
                new() { Title = "管理更新", Description = "暂停或恢复 Windows 更新", IconGlyph = "\uE895", TargetPage = "WindowsUpdatePage" },
                new() { Title = "管理 Edge", Description = "安装、卸载和清理 Microsoft Edge", IconGlyph = "\uE774", TargetPage = "EdgeRemoverPage" },
            };
        }
    }

    public class QuickActionItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = "\uE896";
        public string TargetPage { get; set; } = string.Empty;
    }
}
