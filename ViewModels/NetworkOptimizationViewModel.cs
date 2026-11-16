using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XenomOptimizer.Models;
using XenomOptimizer.Services;

namespace XenomOptimizer.ViewModels;

/// <summary>
/// Network optimization VM with safe utilities like DNS flush, IP renew, etc.
/// Clearly explains that DNS changes affect name resolution, not in-game ping directly.
/// </summary>
public partial class NetworkOptimizationViewModel : ObservableObject
{
    private readonly ISystemDetectionService _systemDetection;
    private readonly ILogService _log;
    private readonly IAdminService _admin;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = new();

    // DNS preset options
    public ObservableCollection<DnsPreset> DnsPresets { get; } = new()
    {
        new DnsPreset { Name = "Google DNS", Primary = "8.8.8.8", Secondary = "8.8.4.4", Description = "Google's public DNS. Fast and reliable name resolution." },
        new DnsPreset { Name = "Cloudflare DNS", Primary = "1.1.1.1", Secondary = "1.0.0.1", Description = "Cloudflare's privacy-focused DNS. Generally fast name resolution." },
        new DnsPreset { Name = "OpenDNS", Primary = "208.67.222.222", Secondary = "208.67.220.220", Description = "Cisco's OpenDNS with optional filtering." },
    };

    public NetworkOptimizationViewModel(ISystemDetectionService systemDetection, ILogService log, IAdminService admin)
    {
        _systemDetection = systemDetection;
        _log = log;
        _admin = admin;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var adapters = await _systemDetection.GetNetworkAdaptersAsync();
            foreach (var adapter in adapters)
                Adapters.Add(adapter);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task FlushDnsAsync()
    {
        IsBusy = true;
        try
        {
            var result = await RunNetworkCommandAsync("ipconfig", "/flushdns");
            StatusMessage = result.Success ? "DNS cache flushed successfully." : $"Failed: {result.Output}";
            _log.Log("Network", "Flush DNS", result.Success ? "SUCCESS" : "ERROR");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RenewIpAsync()
    {
        IsBusy = true;
        try
        {
            await RunNetworkCommandAsync("ipconfig", "/release");
            var result = await RunNetworkCommandAsync("ipconfig", "/renew");
            StatusMessage = result.Success ? "IP address renewed successfully." : $"Failed: {result.Output}";
            _log.Log("Network", "Renew IP", result.Success ? "SUCCESS" : "ERROR");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetWinsockAsync()
    {
        if (!_admin.IsRunningAsAdmin)
        {
            StatusMessage = "⚠ Administrator privileges required for Winsock reset.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await RunNetworkCommandAsync("netsh", "winsock reset");
            StatusMessage = result.Success
                ? "Winsock catalog reset. A restart is required for changes to take effect."
                : $"Failed: {result.Output}";
            _log.Log("Network", "Reset Winsock", result.Success ? "SUCCESS" : "ERROR");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ResetTcpIpAsync()
    {
        if (!_admin.IsRunningAsAdmin)
        {
            StatusMessage = "⚠ Administrator privileges required for TCP/IP reset.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await RunNetworkCommandAsync("netsh", "int ip reset");
            StatusMessage = result.Success
                ? "⚠ TCP/IP stack reset. A restart is REQUIRED for changes to take effect."
                : $"Failed: {result.Output}";
            _log.Log("Network", "Reset TCP/IP", result.Success ? "SUCCESS" : "ERROR");
        }
        finally { IsBusy = false; }
    }

    private static async Task<(bool Success, string Output)> RunNetworkCommandAsync(string command, string args)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
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

public class DnsPreset
{
    public string Name { get; set; } = "";
    public string Primary { get; set; } = "";
    public string Secondary { get; set; } = "";
    public string Description { get; set; } = "";
}
