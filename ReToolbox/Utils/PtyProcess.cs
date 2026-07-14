using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ReToolbox.Utils
{
    // Runs a console process attached to a ConPTY (pseudo console) instead of a
    // plain redirected pipe. A redirected stdout is what makes tools such as winget
    // detect Console.IsOutputRedirected == true and stop streaming their progress
    // bar in real time: they buffer the redraws and only flush them in a burst when
    // a stage ends. Behind a pseudo console the tool believes it owns a real
    // terminal, so it renders progress frame by frame (via carriage returns / ANSI
    // cursor moves) and RunAsync relays each frame as a decoded, ANSI-stripped line.
    public static class PtyProcess
    {
        private const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        // Win32 PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE.
        private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;
        private static readonly int INFINITE = -1;
        private static readonly char[] LineBreaks = { '\r', '\n' };

        // CSI / OSC escape sequences emitted by virtual-terminal renderers.
        private static readonly Regex AnsiEscape =
            new(@"\u001b\][^\u0007]*\u0007|\u001b\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);

        // Other C0 control characters except CR/LF (bell, backspace, etc.).
        private static readonly Regex ControlChars =
            new(@"[\x00-\x08\x0b\x0c\x0e-\x1f]", RegexOptions.Compiled);

        /// <summary>
        /// Launches <paramref name="commandLine"/> in a pseudo console and invokes
        /// <paramref name="onLine"/> for every logical output line (carriage return
        /// or newline terminated). Completes when the process exits, or when
        /// <paramref name="cancellationToken"/> is cancelled (the child is terminated).
        /// </summary>
        public static async Task<int> RunAsync(string commandLine, Encoding encoding, Action<string> onLine, CancellationToken cancellationToken = default)
        {
            var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf<SECURITY_ATTRIBUTES>() };

            // input pipe: PTY reads stdin from inputRead (we never write input).
            // output pipe: PTY writes rendered output to outputWrite, we read outputRead.
#pragma warning disable CA1420
            // All by-ref P/Invoke args here are blittable (IntPtr, fixed-layout
            // structs, primitives), so they pin correctly under DisableRuntimeMarshalling.
            // The analyzer can't prove the struct layouts, so it warns conservatively.
            if (Native.CreatePipe(out IntPtr inputRead, out IntPtr inputWrite, ref sa, 0) == 0)
            {
                throw new Win32Exception(Marshal.GetLastSystemError());
            }
            if (Native.CreatePipe(out IntPtr outputRead, out IntPtr outputWrite, ref sa, 0) == 0)
            {
                Native.CloseHandle(inputRead);
                Native.CloseHandle(inputWrite);
                throw new Win32Exception(Marshal.GetLastSystemError());
            }

            var size = new COORD { X = 120, Y = 30 };
            int hr = Native.CreatePseudoConsole(size, inputRead, outputWrite, 0, out IntPtr hPC);
            // CreatePseudoConsole duplicated the handles it needs; release ours.
            Native.CloseHandle(inputRead);
            Native.CloseHandle(outputWrite);

            if (hr != 0)
            {
                Native.CloseHandle(inputWrite);
                Native.CloseHandle(outputRead);
                throw new Win32Exception(hr);
            }

            // Build the proc-thread attribute list that attaches the child to the PTY.
            IntPtr attrSize = IntPtr.Zero;
            Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrSize);
            IntPtr attrList = Marshal.AllocHGlobal((int)attrSize);
            Native.InitializeProcThreadAttributeList(attrList, 1, 0, ref attrSize);

            try
            {
                if (Native.UpdateProcThreadAttribute(attrList, 0,
                        (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, hPC,
                        (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero) == 0)
                {
                    throw new Win32Exception(Marshal.GetLastSystemError());
                }

                STARTUPINFOEX si = new() { lpAttributeList = attrList };
                si.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

                // CreateProcessW may mutate the command line buffer, so hand it a
                // dedicated unmanaged copy rather than a pinned managed string.
                IntPtr pCmd = Marshal.StringToHGlobalUni(commandLine);
                PROCESS_INFORMATION pi;
                try
                {
                    if (Native.CreateProcessW(IntPtr.Zero, pCmd,
                            IntPtr.Zero, IntPtr.Zero, 0,
                            EXTENDED_STARTUPINFO_PRESENT,
                            IntPtr.Zero, IntPtr.Zero, ref si, out pi) == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastSystemError());
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(pCmd);
                }

                Native.CloseHandle(pi.hThread);
                try
                {
                    // Allow the caller to abort the child (e.g. once it printed a URL
                    // we want to handle ourselves). Terminate the whole process tree;
                    // the wait then returns immediately and the finally chain still
                    // tears down the PTY and pipes.
                    using CancellationTokenRegistration reg =
                        cancellationToken.Register(() => Native.TerminateProcess(pi.hProcess, 1));

                    // Keep draining the PTY output on a background task so a full
                    // buffer can never stall the child's writes, then wait for exit.
                    Task readTask = Task.Run(() => ReadLoop(outputRead, encoding, onLine));
                    await Task.Run(() => Native.WaitForSingleObject(pi.hProcess, INFINITE))
                        .ConfigureAwait(false);

                    // Tear the PTY down first: that closes the write end of the
                    // output pipe so the reader's ReadFile returns and the task ends.
                    Native.ClosePseudoConsole(hPC);
                    hPC = IntPtr.Zero;
                    await readTask.ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (Native.GetExitCodeProcess(pi.hProcess, out uint exitCode) == 0)
                    {
                        throw new Win32Exception(Marshal.GetLastSystemError());
                    }

                    return unchecked((int)exitCode);
                }
                finally
                {
                    Native.CloseHandle(pi.hProcess);
                }
            }
            finally
            {
                Native.DeleteProcThreadAttributeList(attrList);
                Marshal.FreeHGlobal(attrList);
                if (hPC != IntPtr.Zero)
                {
                    Native.ClosePseudoConsole(hPC);
                }
                Native.CloseHandle(inputWrite);
                Native.CloseHandle(outputRead);
            }
#pragma warning restore CA1420
        }

        // Reads the PTY output pipe to EOF, decoding bytes, stripping virtual-terminal
        // escapes and emitting each CR/LF-delimited line. A partial trailing line
        // (no terminator yet) is held in pending across reads.
        private static void ReadLoop(IntPtr outputRead, Encoding encoding, Action<string> onLine)
        {
            byte[] buffer = new byte[4096];
            string pending = string.Empty;

#pragma warning disable CA1420
            while (Native.ReadFile(outputRead, buffer, (uint)buffer.Length,
                       out uint readN, IntPtr.Zero) != 0 && readN > 0)
            {
                string text = encoding.GetString(buffer, 0, (int)readN);
                text = StripControl(text);

                pending += text;
                int i;
                while ((i = pending.IndexOfAny(LineBreaks)) >= 0)
                {
                    string line = pending.Substring(0, i);
                    int sep = pending[i] == '\r' && i + 1 < pending.Length && pending[i + 1] == '\n' ? 2 : 1;
                    pending = pending.Substring(i + sep);
                    if (line.Length > 0)
                    {
                        onLine(line);
                    }
                }
            }

            if (pending.Length > 0)
            {
                onLine(pending);
            }
#pragma warning restore CA1420
        }

        private static string StripControl(string text)
        {
            if (text.IndexOf('\u001b') >= 0)
            {
                text = AnsiEscape.Replace(text, string.Empty);
            }
            // Drop leftover C0 controls after escape stripping (bell etc.), keep CR/LF.
            return ControlChars.Replace(text, string.Empty);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct COORD
        {
            public short X;
            public short Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES
        {
            public int nLength;
            public IntPtr lpSecurityDescriptor;
            public int bInheritHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public IntPtr lpReserved;
            public IntPtr lpDesktop;
            public IntPtr lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public ushort wShowWindow;
            public ushort cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        // All P/Invoke signatures below use only blittable types (IntPtr, fixed-layout
        // structs, primitives, one-dimensional byte array), so they pin and marshal
        // correctly even though this assembly disables runtime marshalling. CA1420
        // fires on any by-ref/managed P/Invoke argument because it can't verify the
        // struct layouts; suppress it for this whole interop block.
#pragma warning disable CA1420
        private static class Native
        {
            [DllImport("kernel32.dll")]
            public static extern int CreatePseudoConsole(
                COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);

            [DllImport("kernel32.dll")]
            public static extern void ClosePseudoConsole(IntPtr hPC);

            // BOOL return/params are int, not C# bool: this assembly disables runtime
            // marshalling, under which [MarshalAs] is ignored and bool is one byte,
            // which would misalign the Win32 BOOL (four bytes) and corrupt the call.

            [DllImport("kernel32.dll")]
            public static extern int CreatePipe(
                out IntPtr hReadPipe, out IntPtr hWritePipe,
                ref SECURITY_ATTRIBUTES lpPipeAttributes, uint nSize);

            [DllImport("kernel32.dll")]
            public static extern int CreateProcessW(
                IntPtr lpApplicationName, IntPtr lpCommandLine,
                IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
                int bInheritHandles,
                uint dwCreationFlags,
                IntPtr lpEnvironment, IntPtr lpCurrentDirectory,
                ref STARTUPINFOEX lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

            [DllImport("kernel32.dll")]
            public static extern int InitializeProcThreadAttributeList(
                IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);

            [DllImport("kernel32.dll")]
            public static extern int UpdateProcThreadAttribute(
                IntPtr lpAttributeList, uint dwFlags, IntPtr attribute,
                IntPtr lpValue, IntPtr cbSize,
                IntPtr lpPreviousValue, IntPtr lpReturnSize);

            [DllImport("kernel32.dll")]
            public static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

            [DllImport("kernel32.dll")]
            public static extern int ReadFile(
                IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
                out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

            [DllImport("kernel32.dll")]
            public static extern int WaitForSingleObject(IntPtr hHandle, int dwMilliseconds);

            [DllImport("kernel32.dll")]
            public static extern int GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

            [DllImport("kernel32.dll")]
            public static extern int TerminateProcess(IntPtr hProcess, int uExitCode);

            [DllImport("kernel32.dll")]
            public static extern int CloseHandle(IntPtr hObject);
        }
#pragma warning restore CA1420
    }
}
