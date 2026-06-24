using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
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

            bool success;

            if (!string.IsNullOrWhiteSpace(software.WingetId))
            {
                success = await InstallFromWingetAsync(software);
            }
            else if (!string.IsNullOrWhiteSpace(software.DownloadUrl))
            {
                success = await InstallFromUrlAsync(software, progress);
            }
            else
            {
                success = false;
            }

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

        private async Task<bool> InstallFromWingetAsync(SoftwareItem software)
        {
            string result = await Task.Run(() =>
                CommandHelper.RunCommand($"winget install --id {software.WingetId} --accept-package-agreements --accept-source-agreements --silent", true, true));

            return !result.Contains("失败", StringComparison.OrdinalIgnoreCase) &&
                   !result.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        // Downloads and launches an installer outside of winget.
        // A DownloadUrl starting with "gh:" resolves the latest GitHub Release asset
        // (e.g. "gh:hooke007/mpv_PlayKit" -> the first .exe asset of the latest release).
        private async Task<bool> InstallFromUrlAsync(SoftwareItem software, IProgress<string>? progress = null)
        {
            try
            {
                string downloadUrl = software.DownloadUrl;
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ReToolbox");

                if (downloadUrl.StartsWith("gh:", StringComparison.OrdinalIgnoreCase))
                {
                    string repo = downloadUrl["gh:".Length..].Trim();
                    string api = $"https://api.github.com/repos/{repo}/releases/latest";
                    string json = await client.GetStringAsync(api);
                    Match match = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]+\.exe)""");
                    if (!match.Success)
                    {
                        progress?.Report($"{software.Name} 未找到可下载的安装包");
                        return false;
                    }

                    downloadUrl = match.Groups[1].Value;
                }

                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                string localPath = Path.Combine(Path.GetTempPath(), fileName);

                progress?.Report($"正在下载 {software.Name}...");
                using (HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    using Stream remote = await response.Content.ReadAsStreamAsync();
                    using Stream local = File.Create(localPath);
                    await remote.CopyToAsync(local);
                }

                progress?.Report($"正在安装 {software.Name}，请按提示完成...");
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = localPath;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                    process.WaitForExit();
                }

                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"{software.Name} 下载安装失败：{ex.Message}");
                return false;
            }
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
            if (string.IsNullOrWhiteSpace(wingetId))
            {
                return false;
            }

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
                new() { Name = "mpv 懒人包", WingetId = "", DownloadUrl = "gh:hooke007/mpv_PlayKit", Category = "媒体", Description = "MPV 播放器整合配置懒人包", IconGlyph = "\uEC4F" },
                new() { Name = "7-Zip", WingetId = "7zip.7zip", Category = "系统工具", Description = "文件压缩工具", IconGlyph = "\uE8E5" },
                new() { Name = "PowerToys", WingetId = "Microsoft.PowerToys", Category = "系统工具", Description = "Windows 增强工具集", IconGlyph = "\uEA6D" },
                new() { Name = "Everything", WingetId = "voidtools.Everything", Category = "系统工具", Description = "文件搜索工具", IconGlyph = "\uE721" },
                new() { Name = "NanaZip", WingetId = "M2Team.NanaZip", Category = "系统工具", Description = "现代文件压缩工具（7-Zip 分支）", IconGlyph = "\uE8E5" },
                new() { Name = "Mem Reduct", WingetId = "Henry++.MemReduct", Category = "系统工具", Description = "内存监控与回收工具", IconGlyph = "\uE950" },
                new() { Name = "图吧工具箱", WingetId = "luolangaga.tubatools", Category = "系统工具", Description = "硬件检测与工具集", IconGlyph = "\uEA6D" },
                new() { Name = "TranslucentTB", WingetId = "CharlesMilette.TranslucentTB", Category = "系统工具", Description = "任务栏透明化工具", IconGlyph = "\uE737" },
                new() { Name = "Telegram", WingetId = "Telegram.TelegramDesktop", Category = "通讯", Description = "Telegram 桌面客户端", IconGlyph = "\uE8BD" },
                new() { Name = "QQ", WingetId = "Tencent.QQ.NT", Category = "通讯", Description = "腾讯 QQ", IconGlyph = "\uE8BD" },
                new() { Name = "微信", WingetId = "Tencent.WeChat", Category = "通讯", Description = "腾讯微信", IconGlyph = "\uE8BD" },
                new() { Name = "Clash Verge", WingetId = "ClashVergeRev.ClashVergeRev", Category = "网络工具", Description = "代理客户端", IconGlyph = "\uE704" },
                new() { Name = "Internet Download Manager", WingetId = "Tonec.InternetDownloadManager", Category = "下载工具", Description = "下载加速器", IconGlyph = "\uE896" },
                new() { Name = "GeForce Experience", WingetId = "Nvidia.GeForceExperience", Category = "驱动", Description = "NVIDIA 驱动管理", IconGlyph = "\uE968" },
            };
        }
    }
}
