using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using XenomOptimizer.Models;

namespace XenomOptimizer.Services;

/// <summary>
/// Detects system hardware and software configuration using WMI and other system APIs.
/// All detection methods are async and handle errors gracefully.
/// </summary>
public class SystemDetectionService : ISystemDetectionService
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out FILETIME lpIdleTime,
        out FILETIME lpKernelTime,
        out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public int dwLowDateTime;
        public int dwHighDateTime;
    }

    private static ulong FileTimeToUInt64(FILETIME ft)
    {
        return ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    private readonly ILogService _log;
    private readonly object _cpuLock = new();
    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;
    private bool _hasCpuBaseline;
    private DateTime _lastCpuSampleTime = DateTime.MinValue;
    private double _lastValidCpuUsage = 0.0;

    private readonly object _gpuLock = new();
    private readonly List<PerformanceCounter> _gpuCounters = new();
    private DateTime _lastGpuScan = DateTime.MinValue;
    private bool _isGpuScanning;
    private PerformanceCounter? _diskCounter;

    // Cached hardware specs so queries execute once at startup and are instantly available
    private string? _cachedCpuModel;
    private string? _cachedGpuModel;
    private string? _cachedRamAmount;
    private string? _cachedRamDetailedSpec;
    private string? _cachedWindowsVersion;
    private string? _cachedGpuDriverVersion;
    private string? _cachedPowerPlan;
    private bool? _cachedVirtualizationEnabled;
    private string? _cachedMotherboard;
    private (string wifi, string bluetooth)? _cachedWireless;

    public void InvalidateHardwareCache()
    {
        _cachedCpuModel = null;
        _cachedGpuModel = null;
        _cachedRamAmount = null;
        _cachedRamDetailedSpec = null;
        _cachedWindowsVersion = null;
        _cachedGpuDriverVersion = null;
        _cachedPowerPlan = null;
        _cachedVirtualizationEnabled = null;
        _cachedMotherboard = null;
        _cachedWireless = null;
    }

    public SystemDetectionService(ILogService logService)
    {
        _log = logService;
        try
        {
            if (GetSystemTimes(out var i0, out var k0, out var u0))
            {
                _prevIdleTime = FileTimeToUInt64(i0);
                _prevKernelTime = FileTimeToUInt64(k0);
                _prevUserTime = FileTimeToUInt64(u0);
                _hasCpuBaseline = true;
                _lastCpuSampleTime = DateTime.UtcNow;
            }
        }
        catch { }
        try
        {
            _diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
            _diskCounter.NextValue();
        }
        catch { }
    }

    public Task<string> GetCpuModelAsync()
    {
        if (!string.IsNullOrEmpty(_cachedCpuModel))
            return Task.FromResult(_cachedCpuModel);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    _cachedCpuModel = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                    return _cachedCpuModel;
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect CPU", "ERROR", errorMessage: ex.Message);
            }
            return "Unknown CPU";
        });
    }

    public Task<string> GetGpuModelAsync()
    {
        if (!string.IsNullOrEmpty(_cachedGpuModel))
            return Task.FromResult(_cachedGpuModel);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
                var gpus = new List<string>();
                foreach (var obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name))
                        gpus.Add(name);
                }
                _cachedGpuModel = gpus.Count > 0 ? string.Join(", ", gpus) : "Unknown GPU";
                return _cachedGpuModel;
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect GPU", "ERROR", errorMessage: ex.Message);
            }
            return "Unknown GPU";
        });
    }

    public Task<string> GetRamAmountAsync()
    {
        if (!string.IsNullOrEmpty(_cachedRamAmount))
            return Task.FromResult(_cachedRamAmount);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (var obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["TotalPhysicalMemory"]?.ToString(), out var bytes))
                    {
                        var gb = bytes / (1024.0 * 1024 * 1024);
                        _cachedRamAmount = $"{gb:F1} GB";
                        return _cachedRamAmount;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect RAM", "ERROR", errorMessage: ex.Message);
            }
            return "Unknown";
        });
    }

    public Task<string> GetRamDetailedSpecAsync()
    {
        if (!string.IsNullOrEmpty(_cachedRamDetailedSpec))
            return Task.FromResult(_cachedRamDetailedSpec);

        return Task.Run(() =>
        {
            try
            {
                ulong totalBytes = 0;
                uint maxSpeed = 0;
                uint smbiosType = 0;

                using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
                foreach (var obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["Capacity"]?.ToString(), out var cap))
                        totalBytes += cap;

                    if (uint.TryParse(obj["ConfiguredClockSpeed"]?.ToString(), out var configuredSpeed) && configuredSpeed > maxSpeed)
                        maxSpeed = configuredSpeed;
                    else if (uint.TryParse(obj["Speed"]?.ToString(), out var speed) && speed > maxSpeed)
                        maxSpeed = speed;

                    if (smbiosType == 0 && uint.TryParse(obj["SMBIOSMemoryType"]?.ToString(), out var smbType) && smbType > 0)
                        smbiosType = smbType;
                }

                if (totalBytes > 0)
                {
                    var gb = Math.Round(totalBytes / (1024.0 * 1024 * 1024));
                    string ddr = smbiosType switch
                    {
                        24 => "DDR3",
                        26 => "DDR4",
                        30 => "LPDDR4",
                        34 => "DDR5",
                        35 => "LPDDR5",
                        _ => ""
                    };

                    string ddrPart = string.IsNullOrEmpty(ddr) ? "" : $" {ddr}";
                    if (maxSpeed > 0)
                    {
                        _cachedRamDetailedSpec = $"{gb:F0} GB{ddrPart} @ {maxSpeed} MHz";
                    }
                    else
                    {
                        _cachedRamDetailedSpec = $"{gb:F0} GB{ddrPart}".Trim();
                    }
                    return _cachedRamDetailedSpec;
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect detailed RAM specs", "WARNING", errorMessage: ex.Message);
            }

            var fallback = GetRamAmountAsync().GetAwaiter().GetResult();
            _cachedRamDetailedSpec = fallback;
            return fallback;
        });
    }

    public Task<string> GetWindowsVersionAsync()
    {
        if (!string.IsNullOrEmpty(_cachedWindowsVersion))
            return Task.FromResult(_cachedWindowsVersion);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString()?.Trim() ?? "";
                    var build = obj["BuildNumber"]?.ToString() ?? "";
                    _cachedWindowsVersion = $"{caption} (Build {build})";
                    return _cachedWindowsVersion;
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect Windows version", "ERROR", errorMessage: ex.Message);
            }
            return Environment.OSVersion.VersionString;
        });
    }

    public Task<string> GetGpuDriverVersionAsync()
    {
        if (!string.IsNullOrEmpty(_cachedGpuDriverVersion))
            return Task.FromResult(_cachedGpuDriverVersion);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT DriverVersion FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    var version = obj["DriverVersion"]?.ToString();
                    if (!string.IsNullOrEmpty(version))
                    {
                        _cachedGpuDriverVersion = version;
                        return version;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect GPU driver version", "ERROR", errorMessage: ex.Message);
            }
            return "Not Available";
        });
    }

    public Task<string> GetCurrentPowerPlanAsync()
    {
        if (!string.IsNullOrEmpty(_cachedPowerPlan))
            return Task.FromResult(_cachedPowerPlan);

        return Task.Run(() =>
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg",
                        Arguments = "/getactivescheme",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);

                // Output format: "Power Scheme GUID: ... (High performance)"
                var parenStart = output.LastIndexOf('(');
                var parenEnd = output.LastIndexOf(')');
                if (parenStart >= 0 && parenEnd > parenStart)
                {
                    _cachedPowerPlan = output[(parenStart + 1)..parenEnd];
                    return _cachedPowerPlan;
                }
                _cachedPowerPlan = output.Trim();
                return _cachedPowerPlan;
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect power plan", "ERROR", errorMessage: ex.Message);
            }
            return "Unknown";
        });
    }

    public Task<double> GetRamUsagePercentAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var memStatus = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(memStatus) && memStatus.ullTotalPhys > 0)
                {
                    double usedPercent = (1.0 - ((double)memStatus.ullAvailPhys / memStatus.ullTotalPhys)) * 100.0;
                    return Math.Clamp(Math.Round(usedPercent, 1), 0.0, 100.0);
                }
            }
            catch { }

            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
                foreach (var obj in searcher.Get())
                {
                    if (ulong.TryParse(obj["TotalVisibleMemorySize"]?.ToString(), out var total) &&
                        ulong.TryParse(obj["FreePhysicalMemory"]?.ToString(), out var free) && total > 0)
                    {
                        return Math.Clamp(Math.Round((1.0 - (double)free / total) * 100, 1), 0.0, 100.0);
                    }
                }
            }
            catch { }
            return 0.0;
        });
    }

    public Task<double> GetCpuUsagePercentAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_cpuLock)
                {
                    var now = DateTime.UtcNow;

                    if (!_hasCpuBaseline)
                    {
                        if (GetSystemTimes(out var i1, out var k1, out var u1))
                        {
                            Thread.Sleep(60);
                            if (GetSystemTimes(out var i2, out var k2, out var u2))
                            {
                                ulong idle1 = FileTimeToUInt64(i1);
                                ulong kernel1 = FileTimeToUInt64(k1);
                                ulong user1 = FileTimeToUInt64(u1);

                                ulong idle2 = FileTimeToUInt64(i2);
                                ulong kernel2 = FileTimeToUInt64(k2);
                                ulong user2 = FileTimeToUInt64(u2);

                                _prevIdleTime = idle2;
                                _prevKernelTime = kernel2;
                                _prevUserTime = user2;
                                _hasCpuBaseline = true;
                                _lastCpuSampleTime = DateTime.UtcNow;

                                ulong diffIdle = idle2 >= idle1 ? idle2 - idle1 : 0;
                                ulong diffKernel = kernel2 >= kernel1 ? kernel2 - kernel1 : 0;
                                ulong diffUser = user2 >= user1 ? user2 - user1 : 0;
                                ulong diffTotal = diffKernel + diffUser;

                                if (diffTotal > 0)
                                {
                                    double idleRatio = Math.Min(1.0, (double)diffIdle / diffTotal);
                                    double pct = Math.Clamp((1.0 - idleRatio) * 100.0, 0.0, 100.0);
                                    _lastValidCpuUsage = Math.Round(pct, 1);
                                    return _lastValidCpuUsage;
                                }
                            }
                        }
                        return _lastValidCpuUsage;
                    }

                    // Throttle: If sampled less than 200ms ago, return last reading to avoid zero-delta noise
                    if ((now - _lastCpuSampleTime).TotalMilliseconds < 200 && _lastValidCpuUsage > 0.0)
                    {
                        return _lastValidCpuUsage;
                    }

                    if (GetSystemTimes(out var idleFt, out var kernelFt, out var userFt))
                    {
                        ulong idle = FileTimeToUInt64(idleFt);
                        ulong kernel = FileTimeToUInt64(kernelFt);
                        ulong user = FileTimeToUInt64(userFt);

                        ulong diffIdle = idle >= _prevIdleTime ? idle - _prevIdleTime : 0;
                        ulong diffKernel = kernel >= _prevKernelTime ? kernel - _prevKernelTime : 0;
                        ulong diffUser = user >= _prevUserTime ? user - _prevUserTime : 0;
                        ulong diffTotal = diffKernel + diffUser;

                        // Only advance baseline if delta is valid (> 10000 100ns units = 1ms)
                        if (diffTotal > 10000)
                        {
                            _prevIdleTime = idle;
                            _prevKernelTime = kernel;
                            _prevUserTime = user;
                            _lastCpuSampleTime = now;

                            double idleRatio = Math.Min(1.0, (double)diffIdle / diffTotal);
                            double cpuPercent = Math.Clamp((1.0 - idleRatio) * 100.0, 0.0, 100.0);

                            // Smooth: if calculated percent is > 0, update last valid reading
                            if (cpuPercent > 0.0)
                            {
                                _lastValidCpuUsage = Math.Round(cpuPercent, 1);
                            }
                            else if (_lastValidCpuUsage > 0.0)
                            {
                                // Natural idle jitter floor: don't suddenly plunge to 0.0%
                                _lastValidCpuUsage = Math.Max(0.5, Math.Round(_lastValidCpuUsage * 0.5, 1));
                            }

                            return _lastValidCpuUsage;
                        }
                    }
                }
            }
            catch { }

            return _lastValidCpuUsage;
        });
    }

    private void EnsureGpuCountersAsync()
    {
        if (_isGpuScanning) return;
        if ((DateTime.Now - _lastGpuScan).TotalSeconds < 45 && _gpuCounters.Count > 0) return;

        _isGpuScanning = true;
        Task.Run(() =>
        {
            try
            {
                var newCounters = new List<PerformanceCounter>();
                if (PerformanceCounterCategory.Exists("GPU Engine"))
                {
                    var cat = new PerformanceCounterCategory("GPU Engine");
                    var names = cat.GetInstanceNames().Where(n => n.EndsWith("engtype_3D")).ToList();
                    foreach (var name in names)
                    {
                        try
                        {
                            var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", name);
                            counter.NextValue();
                            newCounters.Add(counter);
                        }
                        catch { }
                    }
                }

                lock (_gpuLock)
                {
                    foreach (var c in _gpuCounters) { try { c.Dispose(); } catch { } }
                    _gpuCounters.Clear();
                    _gpuCounters.AddRange(newCounters);
                    _lastGpuScan = DateTime.Now;
                }
            }
            catch { }
            finally
            {
                _isGpuScanning = false;
            }
        });
    }

    public Task<double> GetGpuUsagePercentAsync()
    {
        return Task.Run(() =>
        {
            EnsureGpuCountersAsync();

            lock (_gpuLock)
            {
                if (_gpuCounters.Count == 0) return 0.0;

                try
                {
                    float total = 0f;
                    for (int i = _gpuCounters.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            total += _gpuCounters[i].NextValue();
                        }
                        catch
                        {
                            try { _gpuCounters[i].Dispose(); } catch { }
                            _gpuCounters.RemoveAt(i);
                        }
                    }
                    return Math.Clamp(Math.Round((double)total, 1), 0.0, 100.0);
                }
                catch
                {
                    return 0.0;
                }
            }
        });
    }

    public Task<double> GetDiskUsagePercentAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                _diskCounter ??= new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                float val = _diskCounter.NextValue();
                return Math.Min(100.0, Math.Max(0.0, Math.Round((double)val, 1)));
            }
            catch
            {
                return 0.0;
            }
        });
    }

    public async Task<string> GetGpuUsageAsync()
    {
        var percent = await GetGpuUsagePercentAsync();
        return $"{percent:F0}%";
    }

    public Task<bool> IsVirtualizationEnabledAsync()
    {
        if (_cachedVirtualizationEnabled.HasValue)
            return Task.FromResult(_cachedVirtualizationEnabled.Value);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT VirtualizationFirmwareEnabled FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    if (bool.TryParse(obj["VirtualizationFirmwareEnabled"]?.ToString(), out var enabled))
                    {
                        _cachedVirtualizationEnabled = enabled;
                        return enabled;
                    }
                }
            }
            catch { }
            return false;
        });
    }

    public Task<bool> IsHyperVEnabledAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "dism.exe",
                        Arguments = "/online /get-featureinfo /featurename:Microsoft-Hyper-V",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10000);
                return output.Contains("State : Enabled", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            return false;
        });
    }

    public Task<List<NetworkAdapterInfo>> GetNetworkAdaptersAsync()
    {
        return Task.Run(() =>
        {
            var adapters = new List<NetworkAdapterInfo>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    var props = nic.GetIPProperties();
                    var ipv4 = props.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);

                    var adapter = new NetworkAdapterInfo
                    {
                        Name = nic.Name,
                        ConnectionType = nic.NetworkInterfaceType.ToString(),
                        Status = nic.OperationalStatus.ToString(),
                        Speed = nic.Speed > 0 ? $"{nic.Speed / 1_000_000} Mbps" : "Unknown",
                        MacAddress = nic.GetPhysicalAddress().ToString(),
                        IPv4Address = ipv4?.Address.ToString() ?? "Not assigned",
                        SubnetMask = ipv4?.IPv4Mask?.ToString() ?? "",
                        Gateway = string.Join(", ", props.GatewayAddresses.Select(g => g.Address.ToString())),
                        DnsServers = string.Join(", ", props.DnsAddresses.Select(d => d.ToString()))
                    };

                    adapters.Add(adapter);
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect network adapters", "ERROR", errorMessage: ex.Message);
            }
            return adapters;
        });
    }

    public Task<List<DiskDriveInfo>> GetStorageDrivesAsync()
    {
        return Task.Run(() =>
        {
            var results = new List<DiskDriveInfo>();
            try
            {
                // Query MSFT_PhysicalDisk for friendly names, bus/media types (NVMe, SSD, HDD), and SMART Health Status
                var physicalDisks = new Dictionary<string, (string Model, string MediaType, string HealthText, string HealthColor)>();
                try
                {
                    using var searcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DeviceId, FriendlyName, MediaType, BusType, HealthStatus, OperationalStatus FROM MSFT_PhysicalDisk");
                    foreach (var obj in searcher.Get())
                    {
                        string id = obj["DeviceId"]?.ToString() ?? "";
                        string name = obj["FriendlyName"]?.ToString()?.Trim() ?? "";
                        int media = Convert.ToInt32(obj["MediaType"] ?? 0);
                        int bus = Convert.ToInt32(obj["BusType"] ?? 0);
                        int health = Convert.ToInt32(obj["HealthStatus"] ?? 0);

                        string typeStr = "SSD";
                        if (bus == 17) typeStr = "NVMe SSD";
                        else if (media == 3) typeStr = "HDD";
                        else if (media == 4) typeStr = "SATA SSD";

                        string healthText = "Health | Good";
                        string healthColor = "#A3E635"; // Lime Green

                        if (health == 1)
                        {
                            healthText = "Health | Normal";
                            healthColor = "#F59E0B"; // Light Orange
                        }
                        else if (health == 2)
                        {
                            healthText = "Health | Bad";
                            healthColor = "#EF4444"; // Red
                        }

                        if (!string.IsNullOrEmpty(id))
                        {
                            physicalDisks[id] = (name, typeStr, healthText, healthColor);
                        }
                    }
                }
                catch { }

                // Query partitions to map DriveLetter -> PhysicalDisk
                var driveToPhysical = new Dictionary<string, (string Model, string MediaType, string HealthText, string HealthColor)>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    using var partSearcher = new ManagementObjectSearcher(@"root\Microsoft\Windows\Storage", "SELECT DiskNumber, DriveLetter FROM MSFT_Partition WHERE DriveLetter IS NOT NULL");
                    foreach (var p in partSearcher.Get())
                    {
                        char letter = Convert.ToChar(p["DriveLetter"] ?? '\0');
                        string diskNum = p["DiskNumber"]?.ToString() ?? "";
                        if (letter != '\0' && physicalDisks.TryGetValue(diskNum, out var pd))
                        {
                            driveToPhysical[$"{letter}:\\"] = pd;
                        }
                    }
                }
                catch { }

                // Query DriveInfo for reliable space, label, and usage metrics
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                        continue;

                    long total = drive.TotalSize;
                    long free = drive.TotalFreeSpace;
                    long used = total - free;
                    double usagePct = total > 0 ? (double)used / total * 100.0 : 0.0;

                    string model = drive.VolumeLabel;
                    string mediaType = "SSD";
                    string healthText = "Health | Good";
                    string healthColor = "#A3E635";

                    if (driveToPhysical.TryGetValue(drive.Name, out var info))
                    {
                        model = info.Model;
                        mediaType = info.MediaType;
                        healthText = info.HealthText;
                        healthColor = info.HealthColor;
                    }
                    else if (string.IsNullOrWhiteSpace(model))
                    {
                        model = $"Drive ({drive.Name.TrimEnd('\\')})";
                    }

                    results.Add(new DiskDriveInfo
                    {
                        DriveLetter = drive.Name.TrimEnd('\\'),
                        ModelName = model,
                        MediaType = mediaType,
                        TotalSizeFormatted = FormatBytes(total),
                        FreeSizeFormatted = FormatBytes(free),
                        UsedSizeFormatted = FormatBytes(used),
                        UsagePercent = Math.Round(usagePct, 1),
                        HealthText = healthText,
                        HealthColorHex = healthColor
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect storage drives", "ERROR", errorMessage: ex.Message);
            }

            return results;
        });
    }

    public Task<string> GetMotherboardModelAsync()
    {
        if (!string.IsNullOrEmpty(_cachedMotherboard))
            return Task.FromResult(_cachedMotherboard);

        return Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    string mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                    string prod = obj["Product"]?.ToString()?.Trim() ?? "";

                    if (mfg.Contains("ASUS", StringComparison.OrdinalIgnoreCase)) mfg = "ASUS";
                    else if (mfg.Contains("MSI", StringComparison.OrdinalIgnoreCase) || mfg.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase)) mfg = "MSI";
                    else if (mfg.Contains("Gigabyte", StringComparison.OrdinalIgnoreCase)) mfg = "Gigabyte";
                    else if (mfg.Contains("ASRock", StringComparison.OrdinalIgnoreCase)) mfg = "ASRock";

                    string result = string.IsNullOrEmpty(mfg) ? prod : $"{mfg} {prod}".Trim();
                    if (!string.IsNullOrEmpty(result))
                    {
                        _cachedMotherboard = result;
                        return _cachedMotherboard;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect motherboard", "ERROR", errorMessage: ex.Message);
            }

            _cachedMotherboard = "Standard Motherboard";
            return _cachedMotherboard;
        });
    }

    public Task<(string wifi, string bluetooth)> GetWirelessHardwareAsync()
    {
        if (_cachedWireless.HasValue)
            return Task.FromResult(_cachedWireless.Value);

        return Task.Run(() =>
        {
            string detectedWifi = "";
            string detectedBt = "";

            try
            {
                using var netSearcher = new ManagementObjectSearcher("SELECT Name, NetConnectionID FROM Win32_NetworkAdapter WHERE NetConnectionID IS NOT NULL OR Name LIKE '%Wi-Fi%' OR Name LIKE '%Wireless%'");
                foreach (var obj in netSearcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "";
                    string connId = obj["NetConnectionID"]?.ToString()?.Trim() ?? "";

                    if (name.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Direct", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("TAP", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (connId.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("802.11", StringComparison.OrdinalIgnoreCase))
                    {
                        detectedWifi = name;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect Wi-Fi adapter", "WARN", errorMessage: ex.Message);
            }

            try
            {
                using var btSearcher = new ManagementObjectSearcher("SELECT Name, Service FROM Win32_PnPEntity WHERE Service = 'BTHUSB' OR (Name LIKE '%Bluetooth%' AND (Service LIKE 'BTH%' OR Service LIKE 'bth%'))");
                foreach (var obj in btSearcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(name) && !name.Contains("Enumerator", StringComparison.OrdinalIgnoreCase) && !name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                    {
                        detectedBt = name;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Log("SystemDetection", "Failed to detect Bluetooth device", "WARN", errorMessage: ex.Message);
            }

            if (string.IsNullOrEmpty(detectedWifi))
                detectedWifi = "Wi-Fi 6 / 802.11ax Adapter";
            if (string.IsNullOrEmpty(detectedBt))
                detectedBt = "Wireless Bluetooth Adapter";

            _cachedWireless = (detectedWifi, detectedBt);
            return _cachedWireless.Value;
        });
    }

    private static string FormatBytes(long bytes)
    {
        double gb = bytes / (1024.0 * 1024 * 1024);
        if (gb >= 1000.0)
            return $"{gb / 1024.0:F1} TB";
        return $"{gb:F0} GB";
    }
}
