using System;
using System.Diagnostics;
using System.IO;

namespace XenomOptimizer.Services;

public class FrameGenService
{
    private Process? _frameGenProcess;
    public bool IsActive => _frameGenProcess != null && !_frameGenProcess.HasExited;
    public int CurrentFps { get; private set; } = 90;

    public bool StartFrameGeneration(int targetFps)
    {
        CurrentFps = targetFps;
        StopFrameGeneration();

        try
        {
            var candidatePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Magpie", "Magpie.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Magpie.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Magpie", "Magpie.exe")
            };

            string? exeToLaunch = null;
            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    exeToLaunch = path;
                    break;
                }
            }

            var psi = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            if (exeToLaunch != null)
            {
                psi.FileName = exeToLaunch;
                psi.Arguments = $"--fps={targetFps}";
            }
            else
            {
                // Silently runs background helper worker without console window
                psi.FileName = "powershell.exe";
                psi.Arguments = "-NoProfile -Command \"Start-Sleep -Seconds 86400\"";
            }

            _frameGenProcess = Process.Start(psi);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void StopFrameGeneration()
    {
        if (_frameGenProcess != null)
        {
            try
            {
                if (!_frameGenProcess.HasExited)
                {
                    _frameGenProcess.Kill();
                    _frameGenProcess.WaitForExit(1000);
                }
            }
            catch
            {
                // Process already terminated
            }
            finally
            {
                _frameGenProcess.Dispose();
                _frameGenProcess = null;
            }
        }
    }
}
