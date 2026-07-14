using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public async Task<bool> InstallSoftwareAsync(
            SoftwareItem software,
            IProgress<LogEntry>? progress = null,
            IProgress<int>? downloadProgress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(LogEntry.Normal($"正在安装 {software.Name}..."));
            downloadProgress?.Report(0);

            bool success;

            if (!string.IsNullOrWhiteSpace(software.WingetId))
            {
                success = await InstallFromWingetAsync(software, progress, downloadProgress, cancellationToken);
            }
            else if (!string.IsNullOrWhiteSpace(software.DownloadUrl))
            {
                progress?.Report(LogEntry.Normal(
                    $"{software.Name} 的直接下载安装已禁用：缺少固定摘要或可信发布者验证。"));
                success = false;
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

        private async Task<bool> InstallFromWingetAsync(
            SoftwareItem software,
            IProgress<LogEntry>? progress,
            IProgress<int>? downloadProgress,
            CancellationToken cancellationToken)
        {
            if (!IsValidWingetId(software.WingetId))
            {
                progress?.Report(LogEntry.Normal($"无效的 winget 软件包 ID：{software.WingetId}"));
                return false;
            }
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

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMinutes(45));

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

                    string trimmed = line.Trim();
                    if (IsNoiseLine(trimmed)) return;
                    output.AppendLine(trimmed);
                    progress?.Report(LogEntry.Normal(trimmed));
                }
            }

            int exitCode;
            try
            {
                exitCode = await PtyProcess.RunAsync(
                    wingetCmd,
                    encoding,
                    OnLine,
                    timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                progress?.Report(LogEntry.Normal(
                    cancellationToken.IsCancellationRequested ? "安装已取消" : "安装超时"));
                return false;
            }
            catch (Exception ex)
            {
                // ConPTY unsupported on this host — degrade to a redirected process.
                progress?.Report(LogEntry.Normal($"（实时进度不可用：{ex.Message}）"));
                output.Clear();
                exitCode = await RunWingetWithRedirectAsync(
                    software.WingetId,
                    encoding,
                    OnLine,
                    timeoutCts.Token).ConfigureAwait(false);
            }

            downloadProgress?.Report(exitCode == 0 ? 100 : 0);
            if (exitCode != 0)
            {
                progress?.Report(LogEntry.Normal($"winget 退出代码：{exitCode}"));
                return false;
            }

            return CheckIfInstalled(software.WingetId);
        }

        // Fallback when the pseudo console cannot be created: classic redirected
        // stdio, split on \r as well as \n. winget still buffers its bar here, so the
        // progress bar won't be live, but installation completes normally.
        private async Task<int> RunWingetWithRedirectAsync(
            string wingetId,
            Encoding encoding,
            Action<string> onLine,
            CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "winget.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding,
                StandardErrorEncoding = encoding
            };
            AddWingetInstallArguments(psi, wingetId);

            using Process process = new() { StartInfo = psi };
            process.Start();

            Task stdoutTask = Task.Run(() => ReadProcessStream(process.StandardOutput.BaseStream, encoding, onLine));
            Task stderrTask = Task.Run(() => ReadProcessStream(process.StandardError.BaseStream, encoding, onLine));

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw;
            }

            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            return process.ExitCode;
        }

        private static void AddWingetInstallArguments(ProcessStartInfo psi, string wingetId)
        {
            psi.ArgumentList.Add("install");
            psi.ArgumentList.Add("--id");
            psi.ArgumentList.Add(wingetId);
            psi.ArgumentList.Add("--exact");
            psi.ArgumentList.Add("--accept-package-agreements");
            psi.ArgumentList.Add("--accept-source-agreements");
            psi.ArgumentList.Add("--silent");
            psi.ArgumentList.Add("--disable-interactivity");
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort cancellation; the original cancellation remains primary.
            }
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

        public static bool IsValidWingetId(string wingetId)
        {
            return InputValidation.IsValidWingetId(wingetId);
        }

        public bool CheckIfInstalled(string wingetId)
        {
            if (!IsValidWingetId(wingetId))
            {
                return false;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "winget.exe",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                psi.ArgumentList.Add("list");
                psi.ArgumentList.Add("--id");
                psi.ArgumentList.Add(wingetId);
                psi.ArgumentList.Add("--exact");
                psi.ArgumentList.Add("--accept-source-agreements");

                using Process process = Process.Start(psi)!;
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 && output.Contains(wingetId, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
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
