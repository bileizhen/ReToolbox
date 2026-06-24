using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ReToolbox.Models;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    public class SoftwareInstallService
    {
        private readonly List<SoftwareItem> _softwareItems;

        public SoftwareInstallService()
        {
            _softwareItems = GetDefaultSoftwareList();
        }

        public List<SoftwareItem> GetSoftwareList() => _softwareItems;

        public async Task<bool> InstallSoftwareAsync(SoftwareItem software, IProgress<string>? progress = null)
        {
            progress?.Report($"正在安装 {software.Name}...");

            string result = await Task.Run(() =>
                CommandHelper.RunCommand($"winget install --id {software.WingetId} --accept-package-agreements --accept-source-agreements --silent", true, true));

            bool success = !result.Contains("失败", StringComparison.OrdinalIgnoreCase) &&
                           !result.Contains("error", StringComparison.OrdinalIgnoreCase);

            if (success)
            {
                software.IsInstalled = true;
                software.InstallStatus = "已安装";
            }
            else
            {
                software.InstallStatus = "安装失败";
            }

            progress?.Report(success ? $"{software.Name} 安装完成" : $"{software.Name} 安装失败");
            return success;
        }

        public async Task InstallSelectedSoftwareAsync(List<SoftwareItem> selectedItems, IProgress<(string, int)>? progress = null)
        {
            int total = selectedItems.Count;
            int completed = 0;

            foreach (var item in selectedItems)
            {
                progress?.Report(($"正在安装 {item.Name}... ({completed + 1}/{total})", completed * 100 / total));
                await InstallSoftwareAsync(item);
                completed++;
            }

            progress?.Report(("所有软件安装完成", 100));
        }

        public bool CheckIfInstalled(string wingetId)
        {
            string result = CommandHelper.RunCommand($"winget list --id {wingetId} --accept-source-agreements", true, true);
            return result.Contains(wingetId, StringComparison.OrdinalIgnoreCase);
        }

        private List<SoftwareItem> GetDefaultSoftwareList()
        {
            return new List<SoftwareItem>
            {
                new() { Name = "Google Chrome", WingetId = "Google.Chrome", Category = "浏览器", Description = "Google 网页浏览器", IconGlyph = "\uE774" },
                new() { Name = "Mozilla Firefox", WingetId = "Mozilla.Firefox", Category = "浏览器", Description = "Firefox 网页浏览器", IconGlyph = "\uE774" },
                new() { Name = "Visual Studio Code", WingetId = "Microsoft.VisualStudioCode", Category = "开发工具", Description = "代码编辑器", IconGlyph = "\uE943" },
                new() { Name = "Git", WingetId = "Git.Git", Category = "开发工具", Description = "版本控制系统", IconGlyph = "\uE943" },
                new() { Name = "Windows Terminal", WingetId = "Microsoft.WindowsTerminal", Category = "开发工具", Description = "终端模拟器", IconGlyph = "\uE756" },
                new() { Name = "VLC", WingetId = "VideoLAN.VLC", Category = "媒体", Description = "多媒体播放器", IconGlyph = "\uEC4F" },
                new() { Name = "7-Zip", WingetId = "7zip.7zip", Category = "系统工具", Description = "文件压缩工具", IconGlyph = "\uE8E5" },
                new() { Name = "PowerToys", WingetId = "Microsoft.PowerToys", Category = "系统工具", Description = "Windows 增强工具集", IconGlyph = "\uEA6D" },
                new() { Name = "Telegram", WingetId = "Telegram.TelegramDesktop", Category = "通讯", Description = "Telegram 桌面客户端", IconGlyph = "\uE8BD" },
                new() { Name = "Notion", WingetId = "Notion.Notion", Category = "办公", Description = "笔记与协作工具", IconGlyph = "\uE8A5" },
                new() { Name = "Everything", WingetId = "voidtools.Everything", Category = "系统工具", Description = "文件搜索工具", IconGlyph = "\uE721" },
                new() { Name = "GeForce Experience", WingetId = "Nvidia.GeForceExperience", Category = "驱动", Description = "NVIDIA 驱动管理", IconGlyph = "\uE968" },
            };
        }
    }
}
