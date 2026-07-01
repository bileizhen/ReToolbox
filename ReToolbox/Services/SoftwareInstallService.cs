using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
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

        public async Task<bool> InstallSoftwareAsync(SoftwareItem software, IProgress<string>? progress = null, IProgress<int>? downloadProgress = null)
        {
            progress?.Report($"正在安装 {software.Name}...");
            downloadProgress?.Report(0);

            bool success;

            if (!string.IsNullOrWhiteSpace(software.WingetId))
            {
                success = await InstallFromWingetAsync(software, progress, downloadProgress);
            }
            else if (!string.IsNullOrWhiteSpace(software.DownloadUrl))
            {
                success = await InstallFromUrlAsync(software, progress, downloadProgress);
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

        private async Task<bool> InstallFromWingetAsync(SoftwareItem software, IProgress<string>? progress, IProgress<int>? downloadProgress)
        {
            // Read winget output line-by-line as it arrives so the dialog log and
            // progress update in real time (ReadToEnd blocks until the process ends).
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c winget install --id {software.WingetId} --accept-package-agreements --accept-source-agreements --silent --disable-interactivity",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.GetEncoding(Encoding.Default.CodePage == 936 ? 936 : 65001),
                StandardErrorEncoding = Encoding.GetEncoding(Encoding.Default.CodePage == 936 ? 936 : 65001)
            };

            using Process process = new Process();
            process.StartInfo = psi;
            process.Start();

            var output = new StringBuilder();
            var errors = new StringBuilder();
            int lastReportedDecile = -1;

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                string line = e.Data;

                // winget redraws its progress bar with carriage returns; captured as a
                // stream each frame becomes its own line. Parse the percentage out and
                // drive the download progress bar instead of logging dozens of bars.
                int? percent = TryParseWingetProgress(line);
                if (percent.HasValue)
                {
                    downloadProgress?.Report(percent.Value);
                    // Log at most once per 10% so the log stays readable but alive.
                    int decile = percent.Value / 10;
                    if (decile != lastReportedDecile)
                    {
                        lastReportedDecile = decile;
                        progress?.Report($"下载中 {percent.Value}%");
                    }
                    return;
                }

                if (IsNoiseLine(line)) return;
                output.AppendLine(line);
                progress?.Report(line.Trim());
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                if (IsNoiseLine(e.Data)) return;
                errors.AppendLine(e.Data);
                progress?.Report(e.Data.Trim());
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
            downloadProgress?.Report(100);

            string all = output.ToString() + errors.ToString();
            return !all.Contains("失败", StringComparison.OrdinalIgnoreCase) &&
                   !all.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        // winget progress lines usually include either a percent or byte counts such
        // as "138 MB / 286 MB". Avoid relying on the rendered bar characters because
        // they vary by terminal encoding.
        private static int? TryParseWingetProgress(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            var percentMatch = Regex.Match(line, @"(?<!\d)(\d{1,3})\s*%");
            if (percentMatch.Success &&
                int.TryParse(percentMatch.Groups[1].Value, out int explicitPercent))
            {
                return Math.Clamp(explicitPercent, 0, 100);
            }

            var bytePair = Regex.Match(
                line,
                @"(\d+(?:[\.,]\d+)?)\s*([KMGT]?i?B)\s*/\s*(\d+(?:[\.,]\d+)?)\s*([KMGT]?i?B)",
                RegexOptions.IgnoreCase);
            if (!bytePair.Success)
            {
                return null;
            }

            if (TryToBytes(bytePair.Groups[1].Value, bytePair.Groups[2].Value, out long received) &&
                TryToBytes(bytePair.Groups[3].Value, bytePair.Groups[4].Value, out long total) &&
                total > 0)
            {
                int percent = (int)(received * 100 / total);
                return Math.Clamp(percent, 0, 100);
            }

            return null;
        }

        private static bool TryToBytes(string value, string unit, out long bytes)
        {
            bytes = 0;
            value = value.Replace(',', '.');

            if (!double.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double num))
            {
                return false;
            }

            bytes = unit.ToUpperInvariant() switch
            {
                "B" => (long)num,
                "KB" => (long)(num * 1024),
                "KIB" => (long)(num * 1024),
                "MB" => (long)(num * 1024 * 1024),
                "MIB" => (long)(num * 1024 * 1024),
                "GB" => (long)(num * 1024L * 1024 * 1024),
                "GIB" => (long)(num * 1024L * 1024 * 1024),
                "TB" => (long)(num * 1024L * 1024 * 1024 * 1024),
                "TIB" => (long)(num * 1024L * 1024 * 1024 * 1024),
                _ => (long)num
            };
            return true;
        }

        // winget renders an animated spinner in the terminal using carriage returns;
        // when captured as a stream these redraw as junk lines. Filter them out so
        // only meaningful status text reaches the log.
        private static bool IsNoiseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            string trimmed = line.Trim();

            // Spinner animation frames: a single "-", "\", "|" or "/" character.
            if (trimmed.Length == 1 && "-\\|/".Contains(trimmed))
            {
                return true;
            }

            // Progress redraws are handled by TryParseWingetProgress; anything still
            // mostly made of terminal drawing characters is also dropped.
            int drawingChars = trimmed.Count(ch => "█▒░■□".Contains(ch));
            if (drawingChars > 0 && drawingChars >= trimmed.Length / 2)
            {
                return true;
            }

            return false;
        }

        // Downloads and launches an installer outside of winget.
        // A DownloadUrl starting with "gh:" resolves the latest GitHub Release asset
        // (e.g. "gh:hooke007/mpv_PlayKit" -> the first .exe asset of the latest release).
        private async Task<bool> InstallFromUrlAsync(SoftwareItem software, IProgress<string>? progress = null, IProgress<int>? downloadProgress = null)
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
                    progress?.Report($"正在解析 {software.Name} 最新版本...");
                    string json = await client.GetStringAsync(api).ConfigureAwait(false);
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
                using (HttpResponseMessage response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long? total = response.Content.Headers.ContentLength;
                    using Stream remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    using Stream local = File.Create(localPath);

                    byte[] buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await remote.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                    {
                        await local.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                        received += read;

                        if (total is long size && size > 0)
                        {
                            int percent = (int)(received * 100 / size);
                            downloadProgress?.Report(percent);
                            progress?.Report($"下载中 {percent}%（{FormatBytes(received)} / {FormatBytes(size)}）");
                        }
                    }
                    downloadProgress?.Report(100);
                }

                progress?.Report($"下载完成，启动 {software.Name} 安装程序，请按提示完成...");
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = localPath;
                    process.StartInfo.UseShellExecute = true;
                    process.Start();
                    // Run the blocking wait off the UI thread so the dialog keeps
                    // rendering while the self-extractor is open.
                    await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception ex)
            {
                progress?.Report($"{software.Name} 下载安装失败：{ex.Message}");
                return false;
            }
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.#} {units[unit]}";
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
