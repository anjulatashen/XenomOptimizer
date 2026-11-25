using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace LagKillerSetup;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

            // DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2 (Windows 11 Native Rounded Corners)
            int roundCorner = 2;
            DwmSetWindowAttribute(hwnd, 33, ref roundCorner, sizeof(int));
        }
        catch { }
    }

    private string _installedExePath = string.Empty;
    private readonly Border[] _capsules;
    private readonly SolidColorBrush _filledBrush = new((Color)ColorConverter.ConvertFromString("#00E599"));
    private readonly SolidColorBrush _emptyBrush = new((Color)ColorConverter.ConvertFromString("#262830"));

    public MainWindow()
    {
        InitializeComponent();
        _capsules = new Border[]
        {
            Capsule0, Capsule1, Capsule2, Capsule3,
            Capsule4, Capsule5, Capsule6, Capsule7,
            Capsule8, Capsule9, Capsule10, Capsule11,
            Capsule12, Capsule13, Capsule14, Capsule15
        };

        var defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "LAG Killer by XENOM");
        PathTextBox.Text = defaultPath;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Installation Folder",
            InitialDirectory = PathTextBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            PathTextBox.Text = dialog.FolderName;
        }
    }

    private void UpdateCapsuleProgress(int percent, string fileName)
    {
        ProgressStatusText.Text = $"Extracting {fileName}...";
        ProgressSubText.Text = $"Now installing · {percent}%";

        // Level text (1 to 10 or 6)
        int level = Math.Clamp((int)Math.Ceiling(percent / 100.0 * 10.0), 1, 10);
        ProgressTitleText.Text = $"Level {level}";

        int activeCount = Math.Clamp((int)Math.Round(percent / 100.0 * 16.0), 0, 16);
        for (int i = 0; i < _capsules.Length; i++)
        {
            _capsules[i].Background = (i < activeCount) ? _filledBrush : _emptyBrush;
        }

        if (activeCount > 0)
        {
            ActiveHighlightBox.Visibility = Visibility.Visible;
            // 16 capsules, each Width=12, Margin=4,0 (total pitch = 20px).
            // Center of capsule i is i * 20 + 10.
            // Highlight box Width=26, so left = i * 20 + 10 - 13 = i * 20 - 3.
            int activeIndex = Math.Min(activeCount - 1, 15);
            double left = Math.Max(0, activeIndex * 20 - 3);
            Canvas.SetLeft(ActiveHighlightBox, left);
        }
        else
        {
            ActiveHighlightBox.Visibility = Visibility.Collapsed;
        }
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var targetDir = PathTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetDir)) return;

        SetupPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        UpdateCapsuleProgress(5, "Initializing installation...");

        var createDesktopShortcut = DesktopShortcutCheckBox.IsChecked == true;
        var createStartMenuShortcut = StartMenuShortcutCheckBox.IsChecked == true;
        var launchAfter = LaunchAfterCheckBox.IsChecked == true;

        await Task.Run(() =>
        {
            try
            {
                Directory.CreateDirectory(targetDir);

                // Extract embedded payload zip
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "LagKillerSetup.app_payload.zip";
                using var resourceStream = assembly.GetManifestResourceStream(resourceName);

                if (resourceStream != null)
                {
                    using var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read);
                    var count = archive.Entries.Count;
                    var current = 0;

                    foreach (var entry in archive.Entries)
                    {
                        var destinationPath = Path.Combine(targetDir, entry.FullName);
                        var dir = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);

                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }

                        current++;
                        var percent = (int)((double)current / count * 85);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateCapsuleProgress(percent, entry.Name);
                        });
                    }
                }

                _installedExePath = Path.Combine(targetDir, "XenomOptimizer.exe");

                // Pre-configure settings and initialize hardware diagnostics state
                Dispatcher.Invoke(() =>
                {
                    UpdateCapsuleProgress(90, "Analyzing PC hardware & diagnostics...");
                });

                try
                {
                    var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XenomOptimizer");
                    Directory.CreateDirectory(appDir);
                    var settingsPath = Path.Combine(appDir, "settings.json");
                    if (!File.Exists(settingsPath))
                    {
                        var defaultSettingsJson = @"{
  ""StartWithWindows"": false,
  ""MinimizeToTray"": false,
  ""AutoBackupBeforeOptimization"": true,
  ""Theme"": ""Dark"",
  ""AccentColor"": ""Cyan"",
  ""LogLevel"": ""INFO"",
  ""RestoreLastConfiguration"": false,
  ""HasRunInitialScan"": true,
  ""HasSeenWindowsGuide"": false,
  ""HasSeenOnboardingTaskV2"": false,
  ""IsDriversInstalledMode"": false,
  ""HasSkippedDriverScanner"": false,
  ""GuideStep1DriversCompleted"": false,
  ""GuideStep2ServicesCompleted"": false,
  ""GuideStep3AdvanceCompleted"": false,
  ""GuideStep4RegistryCompleted"": false,
  ""GuideStep5GameLoopCompleted"": false,
  ""GuideStep6CleanupCompleted"": false,
  ""LastActivePage"": ""Dashboard"",
  ""LastSelectedSlideIndex"": 0,
  ""ServicesPresetIndex"": 0,
  ""TargetFps"": 120,
  ""SelectedQualityIndex"": 0,
  ""IsFrameGenActive"": false
}";
                        File.WriteAllText(settingsPath, defaultSettingsJson);
                    }
                    else
                    {
                        var text = File.ReadAllText(settingsPath);
                        if (text.Contains("\"HasRunInitialScan\": false"))
                        {
                            text = text.Replace("\"HasRunInitialScan\": false", "\"HasRunInitialScan\": true");
                            File.WriteAllText(settingsPath, text);
                        }
                    }
                }
                catch { }

                // Shortcuts
                Dispatcher.Invoke(() =>
                {
                    UpdateCapsuleProgress(95, "Creating shortcuts...");
                });

                if (createDesktopShortcut)
                {
                    var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktopDir, "LAG Killer by XENOM.lnk"), _installedExePath, targetDir);
                }

                if (createStartMenuShortcut)
                {
                    var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "LAG Killer by XENOM");
                    Directory.CreateDirectory(startMenuDir);
                    CreateShortcut(Path.Combine(startMenuDir, "LAG Killer by XENOM.lnk"), _installedExePath, targetDir);
                }

                Dispatcher.Invoke(() => UpdateCapsuleProgress(100, "Installation complete!"));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Installation error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        });

        await Task.Delay(400);
        ProgressPanel.Visibility = Visibility.Collapsed;

        if (launchAfter && File.Exists(_installedExePath))
        {
            Process.Start(new ProcessStartInfo(_installedExePath) { UseShellExecute = true });
            Close();
        }
        else
        {
            CompletedPanel.Visibility = Visibility.Visible;
        }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(_installedExePath))
        {
            Process.Start(new ProcessStartInfo(_installedExePath) { UseShellExecute = true });
        }
        Close();
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir)
    {
        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDir;
                shortcut.Description = "LAG Killer by XENOM - PUBG Mobile & GameLoop Optimizer";
                shortcut.Save();
            }
        }
        catch { }
    }
}