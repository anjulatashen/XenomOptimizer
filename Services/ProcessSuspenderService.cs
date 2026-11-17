using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XenomOptimizer.Services;

public class ProcessSuspenderService
{
    [DllImport("ntdll.dll")]
    private static extern uint NtSuspendProcess(IntPtr processHandle);

    [DllImport("ntdll.dll")]
    private static extern uint NtResumeProcess(IntPtr processHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

    private static readonly HashSet<string> TargetProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "discord",
        "steamwebhelper",
        "epicgameslauncher",
        "onedrive",
        "googledrivesync",
        "spotify"
    };

    private readonly HashSet<int> _suspendedPids = new();
    private readonly object _lock = new();

    public bool IsActive { get; private set; }
    public int SuspendedCount
    {
        get
        {
            lock (_lock)
            {
                return _suspendedPids.Count;
            }
        }
    }

    public int SuspendBackgroundProcesses()
    {
        lock (_lock)
        {
            var processes = Process.GetProcesses();
            int newlySuspended = 0;

            foreach (var proc in processes)
            {
                try
                {
                    if (TargetProcessNames.Contains(proc.ProcessName) && !_suspendedPids.Contains(proc.Id))
                    {
                        var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, proc.Id);
                        if (hProc != IntPtr.Zero)
                        {
                            try
                            {
                                if (NtSuspendProcess(hProc) == 0)
                                {
                                    _suspendedPids.Add(proc.Id);
                                    newlySuspended++;
                                }
                            }
                            finally
                            {
                                CloseHandle(hProc);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore processes with access restrictions
                }
                finally
                {
                    proc.Dispose();
                }
            }

            IsActive = _suspendedPids.Count > 0;
            return _suspendedPids.Count;
        }
    }

    public void ResumeAllProcesses()
    {
        lock (_lock)
        {
            foreach (var pid in _suspendedPids)
            {
                try
                {
                    var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
                    if (hProc != IntPtr.Zero)
                    {
                        try
                        {
                            NtResumeProcess(hProc);
                        }
                        finally
                        {
                            CloseHandle(hProc);
                        }
                    }
                }
                catch
                {
                    // Process may have exited
                }
            }

            _suspendedPids.Clear();
            IsActive = false;
        }
    }
}
