using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

        public async Task<bool> InstallSoftwareAsync(SoftwareItem software, IProgress<LogEntry>? progress = null, IProgress<int>? downloadProgress = null)
        {
            progress?.Report(LogEntry.Normal($"正在安装 {software.Name}..."));
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

            progress?.Report(LogEntry.Normal(success ? $"{software.Name} 安装完成" : $"{software.Name} 安装失败"));
            return success;
        }

        private async Task<bool> InstallFromWingetAsync(SoftwareItem software, IProgress<LogEntry>? progress, IProgress<int>? downloadProgress)
        {
            // winget renders its progress bar live only when it believes it is writing
            // to a real terminal. Behind a redirected pipe it sees
            // Console.IsOutputRedirected == true, buffers every redraw, and flushes
            // them in a burst when the stage ends — so the dialog freezes for the whole
            // download and then dumps 0%…100% at once. We therefore host winget inside
            // a pseudo console (ConPTY): it then sees a real terminal, redraws the bar
            // frame by frame, and we relay each frame as it arrives. If ConPTY is
            // unavailable we fall back to a redirected process, which still installs
            // fine (just without a live progress bar).
            Encoding encoding = Encoding.GetEncoding(Encoding.Default.CodePage == 936 ? 936 : 65001);
            string wingetCmd = $"winget install --id {software.WingetId} --accept-package-agreements --accept-source-agreements --silent --disable-interactivity";

            var output = new StringBuilder();
            int lastPercent = -1;
            // OnLine runs on the reader task(s); guard the shared accumulators.
            object lineLock = new();

            // When winget starts fetching a GitHub installer we abort it and download
            // through a mirror ourselves — winget's own downloader goes direct and is
            // unusably slow for github.com. Set when the URL line is observed.
            string? takeoverUrl = null;
            using var cts = new CancellationTokenSource();

            void OnLine(string line)
            {
                lock (lineLock)
                {
                    // A winget progress frame (bar + "received UNIT / total UNIT").
                    // Drive the progress bar and refresh the single status line in
                    // place instead of appending a row per redraw.
                    int? percent = TryParseWingetProgress(line);
                    if (percent.HasValue)
                    {
                        int p = percent.Value;
                        if (p != lastPercent)
                        {
                            lastPercent = p;
                            downloadProgress?.Report(p);
                        }
                        progress?.Report(LogEntry.Progress($"下载中 {p}%"));
                        return;
                    }

                    // Take over a GitHub download: winget only prints the URL once, at
                    // the very start of fetching, so the abort happens before the slow
                    // bulk transfers. Only when mirror acceleration is enabled — when
                    // it's off we let winget proceed (silent install) since a manual
                    // run gains nothing over winget's direct fetch.
                    Match urlMatch = WingetDownloadUrl.Match(line);
                    if (urlMatch.Success &&
                        GitHubMirrorHelper.IsEnabled &&
                        GitHubMirrorHelper.IsGitHubUrl(urlMatch.Groups[1].Value) &&
                        takeoverUrl is null)
                    {
                        takeoverUrl = urlMatch.Groups[1].Value;
                        progress?.Report(LogEntry.Normal(
                            $"检测到 GitHub 安装包，中止 winget 改用镜像加速下载..."));
                        cts.Cancel();
                        return;
                    }

                    string trimmed = line.Trim();
                    if (IsNoiseLine(trimmed)) return;
                    output.AppendLine(trimmed);
                    progress?.Report(LogEntry.Normal(trimmed));
                }
            }

            try
            {
                await PtyProcess.RunAsync($"cmd.exe /c {wingetCmd}", encoding, OnLine, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when we abort to take over the download. Handled below.
            }
            catch (Exception ex)
            {
                // ConPTY unsupported on this host — degrade to a redirected process.
                progress?.Report(LogEntry.Normal($"（实时进度不可用：{ex.Message}）"));
                output.Clear();
                await RunWingetWithRedirectAsync(wingetCmd, encoding, OnLine).ConfigureAwait(false);
            }

            // We caught winget just as it began a GitHub download — fetch the installer
            // through a mirror and run it directly. This skips winget's silent install,
            // but completes a 286 MB download in minutes instead of 25+ minutes.
            if (takeoverUrl is not null)
            {
                downloadProgress?.Report(0);
                using HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("ReToolbox");
                string localPath = Path.Combine(Path.GetTempPath(),
                    Path.GetFileName(new Uri(takeoverUrl).LocalPath));
                return await DownloadAndRunAsync(client, takeoverUrl, software.Name, localPath,
                    progress, downloadProgress).ConfigureAwait(false);
            }

            downloadProgress?.Report(100);
            string all = output.ToString();
            return !all.Contains("失败", StringComparison.OrdinalIgnoreCase) &&
                   !all.Contains("error", StringComparison.OrdinalIgnoreCase);
        }

        // Fallback when the pseudo console cannot be created: classic redirected
        // stdio, split on \r as well as \n. winget still buffers its bar here, so the
        // progress bar won't be live, but installation completes normally.
        private async Task RunWingetWithRedirectAsync(string wingetCmd, Encoding encoding, Action<string> onLine)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {wingetCmd}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            };

            using Process process = new();
            process.StartInfo = psi;
            process.Start();

            // Drain both pipes concurrently so a full buffer can't deadlock winget.
            Task stdoutTask = Task.Run(() => ReadProcessStream(process.StandardOutput.BaseStream, encoding, onLine));
            Task stderrTask = Task.Run(() => ReadProcessStream(process.StandardError.BaseStream, encoding, onLine));

            await Task.Run(() => process.WaitForExit()).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }

        // Reads a redirected process stream and invokes onLine for every logical
        // line, splitting on \r and \n independently. Splitting on \r too surfaces
        // carriage-return progress redraws that ReadLine/OutputDataReceived would hide.
        private static void ReadProcessStream(Stream stream, Encoding encoding, Action<string> onLine)
        {
            using StreamReader reader = new StreamReader(stream, encoding);
            StringBuilder buffer = new StringBuilder();
            int ch;
            while ((ch = reader.Read()) > 0)
            {
                if (ch == '\r' || ch == '\n')
                {
                    if (buffer.Length > 0)
                    {
                        onLine(buffer.ToString());
                        buffer.Clear();
                    }
                    continue;
                }
                buffer.Append((char)ch);
            }
            if (buffer.Length > 0)
            {
                onLine(buffer.ToString());
            }
        }

        // winget renders its progress as:  ████████▒▒▒▒  138 MB /  286 MB
        // The bar glyphs are unreliable across renderers, but the "received / total"
        // byte pair is stable, so match that directly to compute a percentage.
        private static readonly Regex ProgressByteRatio =
            new(@"(\d+(?:\.\d+)?)\s*(KB|MB|GB|B)\s*/\s*(\d+(?:\.\d+)?)\s*(KB|MB|GB|B)", RegexOptions.Compiled);

        // winget prints the installer URL as "正在下载 https://github.com/…" (zh) or
        // "Downloading https://github.com/…" (en). We watch for a GitHub URL here so we
        // can abort winget's slow direct download and fetch the file through a mirror.
        private static readonly Regex WingetDownloadUrl =
            new(@"(?:正在下载|Downloading)\s+(https?://\S+)", RegexOptions.Compiled);

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

            Match m = ProgressByteRatio.Match(line);
            if (m.Success &&
                TryToBytes(m.Groups[1].Value, m.Groups[2].Value, out long received) &&
                TryToBytes(m.Groups[3].Value, m.Groups[4].Value, out long total) &&
                total > 0)
            {
                int percent = (int)(received * 100 / total);
                return Math.Clamp(percent, 0, 100);
            }

            return null;
        }
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
        private async Task<bool> InstallFromUrlAsync(SoftwareItem software, IProgress<LogEntry>? progress = null, IProgress<int>? downloadProgress = null)
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
                    progress?.Report(LogEntry.Normal($"正在解析 {software.Name} 最新版本..."));
                    string json;
                    using (HttpResponseMessage apiResponse = await GitHubMirrorHelper.GetAsync(client, api, mirror =>
                    {
                        if (mirror is not null)
                        {
                            progress?.Report(LogEntry.Normal($"使用镜像 {new Uri(mirror).Host}"));
                        }
                    }).ConfigureAwait(false))
                    {
                        apiResponse.EnsureSuccessStatusCode();
                        json = await apiResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    Match match = Regex.Match(json, @"""browser_download_url""\s*:\s*""([^""]+\.exe)""");
                    if (!match.Success)
                    {
                        progress?.Report(LogEntry.Normal($"{software.Name} 未找到可下载的安装包"));
                        return false;
                    }

                    downloadUrl = match.Groups[1].Value;
                }

                string fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                string localPath = Path.Combine(Path.GetTempPath(), fileName);

                return await DownloadAndRunAsync(client, downloadUrl, software.Name, localPath, progress, downloadProgress)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                progress?.Report(LogEntry.Normal($"{software.Name} 下载安装失败：{ex.Message}"));
                return false;
            }
        }

        // Shared by InstallFromUrlAsync and the winget GitHub-takeover path: mirror
        // <paramref name="url"/> to temp file <paramref name="localPath"/> with a live
        // progress bar, then run the installer. Returns false on any failure.
        private async Task<bool> DownloadAndRunAsync(
            HttpClient client, string url, string displayName, string localPath,
            IProgress<LogEntry>? progress, IProgress<int>? downloadProgress)
        {
            progress?.Report(LogEntry.Normal($"正在下载 {displayName}..."));
            using (HttpResponseMessage response = await GitHubMirrorHelper.GetAsync(client, url, mirror =>
            {
                if (mirror is not null)
                {
                    progress?.Report(LogEntry.Normal($"使用镜像 {new Uri(mirror).Host}"));
                }
            }).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                using Stream remote = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using Stream local = File.Create(localPath);

                byte[] buffer = new byte[81920];
                long received = 0;
                int read;
                // Drive the bar only on a new integer percent, and refresh the single
                // "下载中 …" status line in place (Progress semantics) instead of
                // appending a row per chunk — same line that winget would redraw.
                int lastPercent = -1;
                while ((read = await remote.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                {
                    await local.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                    received += read;

                    if (total is long size && size > 0)
                    {
                        int percent = (int)(received * 100 / size);
                        if (percent != lastPercent)
                        {
                            lastPercent = percent;
                            downloadProgress?.Report(percent);
                            progress?.Report(LogEntry.Progress(
                                $"下载中 {percent}%（{FormatBytes(received)} / {FormatBytes(size)}）"));
                        }
                    }
                }
                downloadProgress?.Report(100);
            }

            progress?.Report(LogEntry.Normal($"下载完成，启动 {displayName} 安装程序，请按提示完成..."));
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
