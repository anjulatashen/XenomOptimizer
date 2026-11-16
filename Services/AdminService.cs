using System.Diagnostics;
using System.Security.Principal;

namespace XenomOptimizer.Services;

/// <summary>
/// Manages administrator privilege detection and elevation requests.
/// Follows the principle of requesting elevation only when needed.
/// </summary>
public class AdminService : IAdminService
{
    public bool IsRunningAsAdmin
    {
        get
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }

    public void RequestElevation()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (exePath == null) return;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            System.Windows.Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // User cancelled the UAC prompt
        }
    }

    public async Task<(bool Success, string Output)> RunElevatedCommandAsync(string command, string arguments)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return (process.ExitCode == 0, output + error);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
