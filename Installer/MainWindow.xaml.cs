using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LagKillerSetup;

public partial class MainWindow : Window
{
    private string _installedExePath = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
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

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var targetDir = PathTextBox.Text.Trim();
        if (string.IsNullOrEmpty(targetDir)) return;

        SetupPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;

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
                        var percent = (int)((double)current / count * 80);
                        Dispatcher.Invoke(() =>
                        {
                            InstallProgressBar.Value = percent;
                            ProgressStatusText.Text = $"Extracting {entry.Name}...";
                        });
                    }
                }

                _installedExePath = Path.Combine(targetDir, "XenomOptimizer.exe");

                // Shortcuts
                Dispatcher.Invoke(() =>
                {
                    InstallProgressBar.Value = 90;
                    ProgressStatusText.Text = "Creating shortcuts...";
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

                Dispatcher.Invoke(() => InstallProgressBar.Value = 100);
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
