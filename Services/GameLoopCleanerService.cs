using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace XenomOptimizer.Services;

public class GameLoopCleanerService
{
    public int KillTelemetryAndDaemons()
    {
        var targets = new[] { "Syzs_dl_svr.exe", "aow_exe.exe", "QmEmulatorService.exe", "AndroidProcess.exe" };
        int killed = 0;

        foreach (var target in targets)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = $"/F /T /IM {target}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(1500);
                    if (proc.ExitCode == 0)
                    {
                        killed++;
                    }
                }
            }
            catch
            {
            }
        }

        return killed;
    }

    public int DeepCachePurge()
    {
        int deletedCount = 0;
        var tempDir = Path.GetTempPath();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var cacheDirs = new[]
        {
            tempDir,
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(appData, "Tencent", "TxGameAssistant", "UI", "ShaderCache"),
            Path.Combine(localAppData, "Tencent", "MobileGamePC", "ShaderCache")
        };

        foreach (var cDir in cacheDirs)
        {
            if (!Directory.Exists(cDir))
            {
                continue;
            }

            try
            {
                var files = Directory.GetFiles(cDir, "*.*", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch
                    {
                    }
                }

                if (cDir == tempDir)
                {
                    var dirs = Directory.GetDirectories(cDir);
                    foreach (var dir in dirs)
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            deletedCount++;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }

        return deletedCount;
    }

    public bool ForceDedicatedGpu()
    {
        try
        {
            var executables = new[]
            {
                @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"C:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe",
                @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe",
                @"C:\Program Files (x86)\TxGameAssistant\AppMarket\AppMarket.exe",
                @"C:\Program Files (x86)\TxGameAssistant\ui\AndroidEmulator.exe",
                @"D:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"D:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe"
            };

            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences");
            if (key != null)
            {
                foreach (var exe in executables)
                {
                    key.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
                }
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    public bool ApplyGameLoopConfig(int width, int height, int engineCode)
    {
        try
        {
            var paths = new[]
            {
                @"Software\Tencent\MobileGamePC\VM",
                @"Software\Tencent\MobileGamePC"
            };

            foreach (var p in paths)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(p);
                    if (key != null)
                    {
                        key.SetValue("Width", width, RegistryValueKind.DWord);
                        key.SetValue("Height", height, RegistryValueKind.DWord);
                        key.SetValue("RenderEngine", engineCode, RegistryValueKind.DWord);
                    }
                }
                catch
                {
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
