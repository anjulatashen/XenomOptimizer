using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;

namespace XenomOptimizer.Services;

/// <summary>
/// Data model representing the remote update manifest hosted on GitHub.
/// </summary>
public class UpdateInfo
{
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("downloadUrl")]
    public string DownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    [JsonPropertyName("mandatory")]
    public bool Mandatory { get; set; } = false;
}

/// <summary>
/// Handles checking for updates from GitHub in the background, downloading update packages,
/// and executing a standalone updater script to replace files and relaunch the app.
/// </summary>
public static class UpdateService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static UpdateInfo? AvailableUpdate { get; private set; }
    public static event Action<UpdateInfo>? UpdateDetected;

    static UpdateService()
    {
        // GitHub API and Raw content require a valid User-Agent header
        HttpClient.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("XenomOptimizer-Updater", "1.0"));
    }

    /// <summary>
    /// Checks for updates asynchronously in a background thread to prevent UI freezing.
    /// Shows a prompt to the user if a newer version is available.
    /// </summary>
    /// <param name="updateJsonUrl">Remote raw URL pointing to update.json hosted on GitHub.</param>
    /// <param name="silentIfUpToDate">If true, does not display any dialog if already on the latest version.</param>
    public static void CheckForUpdatesInBackground(string updateJsonUrl, bool silentIfUpToDate = true)
    {
        Task.Run(async () =>
        {
            try
            {
                await CheckForUpdatesAsync(updateJsonUrl, silentIfUpToDate);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] Background update check failed: {ex.Message}");
                if (!silentIfUpToDate)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"Failed to check for updates:\n{ex.Message}",
                            "Update Check",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }
            }
        });
    }

    /// <summary>
    /// Asynchronously fetches update.json and evaluates version comparison.
    /// </summary>
    public static async Task CheckForUpdatesAsync(string updateJsonUrl, bool silentIfUpToDate = true)
    {
        var updateInfo = await FetchUpdateInfoAsync(updateJsonUrl);
        if (updateInfo == null || string.IsNullOrWhiteSpace(updateInfo.Version))
        {
            if (!silentIfUpToDate)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        "Could not retrieve update information from the server.",
                        "Update Check",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            }
            return;
        }

        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);
        if (Version.TryParse(updateInfo.Version, out var remoteVersion))
        {
            if (remoteVersion > currentVersion)
            {
                AvailableUpdate = updateInfo;
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    UpdateDetected?.Invoke(updateInfo);
                });

                if (!silentIfUpToDate)
                {
                    // If manually checked, switch to UI thread to show prompt
                    await Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        var notes = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                            ? ""
                            : $"\n\nRelease Notes:\n{updateInfo.ReleaseNotes}";

                        var message = $"A new version of Xenom Optimizer is available!\n\n" +
                                      $"Current Version: v{currentVersion.ToString(3)}\n" +
                                      $"Latest Version:  v{updateInfo.Version}" +
                                      notes +
                                      $"\n\nWould you like to download and install this update now?";

                        var result = MessageBox.Show(
                            message,
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            await DownloadAndApplyUpdateAsync(updateInfo);
                        }
                    });
                }
            }
            else
            {
                if (!silentIfUpToDate)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"You are running the latest version! (v{currentVersion.ToString(3)})",
                            "Update Check",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
            }
        }
    }

    /// <summary>
    /// Fetches and deserializes the update.json manifest.
    /// </summary>
    public static async Task<UpdateInfo?> FetchUpdateInfoAsync(string updateJsonUrl)
    {
        try
        {
            // Cache busting query parameter
            var uriBuilder = new UriBuilder(updateJsonUrl);
            var query = uriBuilder.Query;
            var timestampParam = $"t={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            uriBuilder.Query = string.IsNullOrEmpty(query) ? timestampParam : query.TrimStart('?') + "&" + timestampParam;

            var response = await HttpClient.GetStringAsync(uriBuilder.Uri);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var info = JsonSerializer.Deserialize<UpdateInfo>(response, options);
            if (info != null && !string.IsNullOrWhiteSpace(info.Version))
            {
                return info;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Error fetching remote update JSON: {ex.Message}");
        }

        // Local fallback if offline or GitHub CDN delay
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(baseDir, "update.json"),
                Path.GetFullPath(Path.Combine(baseDir, "..\\..\\..\\update.json")),
                @"D:\Shop\Xenom Lag Fix\update.json"
            };

            foreach (var path in candidates)
            {
                if (File.Exists(path))
                {
                    var localJson = await File.ReadAllTextAsync(path);
                    var localInfo = JsonSerializer.Deserialize<UpdateInfo>(localJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (localInfo != null && !string.IsNullOrWhiteSpace(localInfo.Version))
                    {
                        return localInfo;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] Error reading local fallback update JSON: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Downloads the update archive / executable and launches the standalone updater script.
    /// </summary>
    public static async Task DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, IProgress<double>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
        {
            MessageBox.Show("No valid download URL provided for this update.", "Update Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "XenomOptimizer_Update");
        Directory.CreateDirectory(tempDir);

        var fileExtension = Path.GetExtension(new Uri(updateInfo.DownloadUrl).AbsolutePath);
        if (string.IsNullOrEmpty(fileExtension)) fileExtension = ".zip";
        var downloadedFilePath = Path.Combine(tempDir, $"update{fileExtension}");

        try
        {
            Window? statusWin = null;
            if (progress == null)
            {
                statusWin = new Window
                {
                    Title = "Updating...",
                    Width = 380,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow
                };

                var sp = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(20),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var txt = new System.Windows.Controls.TextBlock
                {
                    Text = $"Downloading update v{updateInfo.Version}...\nPlease wait, the app will restart automatically.",
                    FontSize = 13,
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 14)
                };
                var pb = new System.Windows.Controls.ProgressBar
                {
                    IsIndeterminate = true,
                    Height = 16
                };
                sp.Children.Add(txt);
                sp.Children.Add(pb);
                statusWin.Content = sp;
                statusWin.Show();
            }

            // Download file with progress reporting
            using (var response = await HttpClient.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                await using var streamToReadFrom = await response.Content.ReadAsStreamAsync();
                await using var streamToWriteTo = File.Create(downloadedFilePath);

                var buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await streamToReadFrom.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await streamToWriteTo.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    if (totalBytes > 0 && progress != null)
                    {
                        var pct = (double)totalRead / totalBytes * 100.0;
                        progress.Report(pct);
                    }
                }
            }

            progress?.Report(100.0);
            await Task.Delay(600);

            statusWin?.Close();

            // Launch updater script
            LaunchUpdater(downloadedFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to download update:\n{ex.Message}", "Update Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Writes and executes the standalone updater batch script, then exits the current application.
    /// </summary>
    private static void LaunchUpdater(string updateFilePath)
    {
        var currentProcess = Process.GetCurrentProcess();
        var currentPid = currentProcess.Id;
        var currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "";
        var currentAppDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

        var tempDir = Path.GetDirectoryName(updateFilePath) ?? Path.GetTempPath();
        var updaterScriptPath = Path.Combine(tempDir, "updater.bat");

        var isZip = updateFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        // Self-contained batch script: waits for current PID to exit, extracts/overwrites files, relaunches exe, and self-deletes
        var scriptContent = $@"@echo off
setlocal
chcp 65001 >nul

:: Wait for the main app process (PID {currentPid}) to exit
:wait_loop
tasklist /fi ""PID eq {currentPid}"" 2>nul | find ""{currentPid}"" >nul
if errorlevel 1 goto apply_update
timeout /t 1 /nobreak >nul
goto wait_loop

:apply_update
timeout /t 1 /nobreak >nul

{(isZip
    ? $@"powershell -NoProfile -ExecutionPolicy Bypass -Command ""Expand-Archive -LiteralPath '{updateFilePath}' -DestinationPath '{currentAppDir}' -Force"""
    : $@"copy /y ""{updateFilePath}"" ""{currentExePath}"""
)}

:: Clean up downloaded update archive
del /f /q ""{updateFilePath}"" >nul 2>&1

:: Relaunch updated main application
start """" ""{currentExePath}""

:: Self delete this updater script
(goto) 2>nul & del ""%~f0""
exit
";

        File.WriteAllText(updaterScriptPath, scriptContent);

        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{updaterScriptPath}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true
        };

        Process.Start(psi);

        // Exit immediately so updater can safely replace files
        Application.Current.Dispatcher.Invoke(() =>
        {
            Application.Current.Shutdown();
        });
        Environment.Exit(0);
    }
}
