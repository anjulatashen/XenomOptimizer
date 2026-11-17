using System.Configuration;

using System.Data;

using System.Windows;

using Microsoft.Extensions.DependencyInjection;

using XenomOptimizer.Services;

using XenomOptimizer.ViewModels;



namespace XenomOptimizer;



public partial class App : Application

{

    public static IServiceProvider Services { get; private set; } = null!;



    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Administrator auto-elevation disabled during development/testing per user request (will re-enable for final build)
        /*
        if (!e.Args.Contains("--screenshot") && !e.Args.Contains("--screenshot-splash") && !e.Args.Contains("--screenshot-scanned"))
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                if (!principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        foreach (var arg in e.Args)
                        {
                            startInfo.ArgumentList.Add(arg);
                        }
                        System.Diagnostics.Process.Start(startInfo);
                    }
                    Shutdown();
                    return;
                }
            }
            catch
            {
                // User declined UAC or error occurred
                Shutdown();
                return;
            }
        }
        */

        var services = new ServiceCollection();

        ConfigureServices(services);

        Services = services.BuildServiceProvider();



        // Initialize logging

        var logger = Services.GetRequiredService<ILogService>();

        logger.Log("Application", "LAG KILLER by XENOM started", "INFO");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                logger.Log("Application", $"Unhandled AppDomain exception: {ex.Message}", "FATAL", errorMessage: ex.ToString());
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            logger.Log("Application", $"Unhandled Dispatcher exception: {args.Exception.Message}", "FATAL", errorMessage: args.Exception.ToString());
        };



        // Asynchronously preload driver scanner animation frames so click is instantaneous (0ms hitch)

        XenomOptimizer.Views.WindowsOptimizationView.EnsureFramesPreloaded();



        var mainWindow = new MainWindow();

        mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();

        MainWindow = mainWindow;

        ShutdownMode = ShutdownMode.OnMainWindowClose;



        if (e.Args.Contains("--screenshot-splash"))

        {

            var splashWindow = new XenomOptimizer.Views.SplashScreenWindow(mainWindow);

            splashWindow.Show();



            var timer = new System.Windows.Threading.DispatcherTimer

            {

                Interval = TimeSpan.FromMilliseconds(500)

            };

            timer.Tick += (s, ev) =>

            {

                timer.Stop();

                try

                {

                    int w = (int)splashWindow.ActualWidth;

                    int h = (int)splashWindow.ActualHeight;

                    if (w <= 0) w = 360;

                    if (h <= 0) h = 360;

                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(

                        w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

                    rtb.Render(splashWindow);

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();

                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));



                    string outPath = @"C:\Users\anjul\.gemini\antigravity-ide\brain\fff55577-e109-4644-9a6b-bd9fece556cc\splash_preview.png";

                    var outIndex = Array.IndexOf(e.Args, "--out");

                    if (outIndex >= 0 && outIndex + 1 < e.Args.Length)

                    {

                        outPath = e.Args[outIndex + 1];

                    }



                    var outDir = Path.GetDirectoryName(outPath);

                    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);



                    using (var fs = System.IO.File.Open(outPath, FileMode.Create))

                    {

                        encoder.Save(fs);

                    }

                }

                catch (Exception ex)

                {

                    File.WriteAllText(@"C:\Users\anjul\.gemini\antigravity\brain\71b74b8b-8fa5-4dce-a4ef-4fbb8a50d260\err.log", ex.ToString());

                }

                Shutdown();

            };

            timer.Start();

            return;

        }



        if (e.Args.Contains("--screenshot") || e.Args.Contains("--screenshot-scanned"))

        {

            bool scanFirst = e.Args.Contains("--screenshot-scanned");

            mainWindow.Show();



            if (mainWindow.DataContext is MainViewModel mvm)

            {

                var pageArgIndex = Array.IndexOf(e.Args, "--page");

                if (pageArgIndex >= 0 && pageArgIndex + 1 < e.Args.Length)

                {

                    var targetPage = e.Args[pageArgIndex + 1];

                    mvm.NavigateToCommand.Execute(targetPage);



                    if (mvm.CurrentPage is GameLoopOptimizationViewModel gvm)
                    {
                        var tabArgIndex = Array.IndexOf(e.Args, "--subtab");
                        if (tabArgIndex >= 0 && tabArgIndex + 1 < e.Args.Length && int.TryParse(e.Args[tabArgIndex + 1], out var tIdx))
                        {
                            gvm.SelectedSubTab = tIdx;
                        }
                    }

                    if (mvm.CurrentPage is WindowsOptimizationViewModel wvm)

                    {

                        var slideArgIndex = Array.IndexOf(e.Args, "--slide");

                        if (slideArgIndex >= 0 && slideArgIndex + 1 < e.Args.Length && int.TryParse(e.Args[slideArgIndex + 1], out var sIdx))

                        {

                            wvm.SelectedSlideIndex = sIdx;

                        }



                        if (e.Args.Contains("--no-guide"))

                        {

                            wvm.IsInstructionModalVisible = false;

                        }

                        if (e.Args.Contains("--warning-modal"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.IsDriverWarningModalVisible = true;

                        }

                        if (e.Args.Contains("--download-flyout-empty") || (e.Args.Contains("--download-flyout") && !e.Args.Contains("--download-flyout-progress") && !e.Args.Contains("--download-flyout-completed")))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.IsDownloadFlyoutOpen = true;

                            wvm.HasDownloadItem = false;

                        }

                        if (e.Args.Contains("--update-available"))

                        {

                            wvm.IsAppUpdateAvailable = true;

                            wvm.AppUpdateVersion = "v1.0.1";

                            wvm.AppUpdateNotes = "— In-Game 120 FPS & Ultra Extreme Graphics\n— Custom Resolutions & 4:3 iPad View Mode\n— ROG Phone 8 & Flagship Device Profiles\n— DirectX+, OpenGL+ & Vulkan Rendering Backend\n— Host CPU Cores & System Memory Allocator\n— One-Click PC Doctor & HVCI Bypass\n— Safe Emulator Close & Shader Cache Cleaner";

                        }

                        if (e.Args.Contains("--update-installing"))
                        {
                            wvm.IsInstructionModalVisible = false;
                            wvm.IsDownloadFlyoutOpen = true;
                            wvm.IsAppUpdateAvailable = true;
                            wvm.IsDownloadingAppUpdate = true;
                            wvm.AppUpdateStep = 1;
                            wvm.AppUpdateStatusText = "Preparation";
                            wvm.AppUpdateProgressSubtitle = "Installing Xenom Optimizer v1.0.1...";
                            wvm.AppUpdateProgressPercent = 25.0;
                        }

                        if (e.Args.Contains("--download-flyout-progress"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.IsDownloadFlyoutOpen = true;

                            wvm.HasDownloadItem = true;

                            wvm.IsDownloadingDriverBooster = true;

                            wvm.IsDownloadCompleted = false;

                            wvm.DownloadProgressPercent = 65.0;

                            wvm.DownloadSpeedText = "8.4 MB/s";

                            wvm.DownloadSizeText = "24.8 MB / 38.5 MB";

                            wvm.DownloadStatusText = "Downloading... 65%";

                        }

                        if (e.Args.Contains("--download-flyout-downloading"))
                        {
                            wvm.IsInstructionModalVisible = false;
                            wvm.IsDownloadFlyoutOpen = true;
                            wvm.IsMultiDownloadViewActive = true;
                            wvm.IsSingleDownloadCompletedView = false;
                            wvm.IsDownloadCompleted = false;
                            wvm.CompletedDownloads.Clear();

                            var item0 = new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                            {
                                Id = "drv_chipset",
                                Title = "Motherboard Chipset Driver",
                                FilePath = "amd_chipset_installer.exe",
                                DetailsText = "Downloading... • 24.5/50.0 MB",
                                IconGlyph = "\uE7F8",
                                IconColor = "#D1FE29",
                                IsDownloading = true,
                                DownloadProgressPercent = 52.0,
                                DownloadProgressWidth = 0.52 * 396.0,
                                SpeedText = "12.4 MB/s",
                                IsInstalled = false
                            };
                            wvm.CompletedDownloads.Add(item0);
                            wvm.DownloadSubtitleText = "Downloading 1 of 4 drivers...";
                        }

                        if (e.Args.Contains("--download-flyout-done-flash"))
                        {
                            wvm.IsInstructionModalVisible = false;
                            wvm.IsDownloadFlyoutOpen = true;
                            wvm.IsMultiDownloadViewActive = true;
                            wvm.IsSingleDownloadCompletedView = false;
                            wvm.IsDownloadCompleted = false;
                            wvm.CompletedDownloads.Clear();

                            var itemDone = new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                            {
                                Id = "drv_directx",
                                Title = "DirectX Runtimes",
                                FilePath = "dxwebsetup.exe",
                                DetailsText = "2:54 PM • 288 KB",
                                IconGlyph = "\uE7FC",
                                IconColor = "#38BDF8",
                                IsDownloading = true,
                                IsDownloadCompletedFlash = true,
                                DownloadOverlayOpacity = 1.0,
                                DownloadProgressPercent = 100.0,
                                DownloadProgressWidth = 396.0,
                                SpeedText = "",
                                IsInstalled = false
                            };
                            wvm.CompletedDownloads.Add(itemDone);
                            wvm.DownloadSubtitleText = "Downloading 2 of 4 drivers...";
                        }

                        if (e.Args.Contains("--download-flyout-completed") || e.Args.Contains("--download-flyout-revealed") || e.Args.Contains("--download-flyout-single") || e.Args.Contains("--download-flyout-multi"))
                        {
                            wvm.IsInstructionModalVisible = false;
                            wvm.IsDownloadFlyoutOpen = true;
                            wvm.HasDownloadItem = true;
                            wvm.IsDownloadCompleted = true;
                            wvm.DownloadProgressPercent = 100;
                            wvm.DownloadSpeedText = "";
                            wvm.DownloadFinalSizeText = "32.6 MB";
                            wvm.DownloadSizeText = "";
                            wvm.DownloadStatusText = "Download Complete ✓ Ready to Install";
                            wvm.RefreshCompletedDownloadsList();

                            if (e.Args.Contains("--download-flyout-multi") || e.Args.Contains("--download-flyout-revealed"))
                            {
                                wvm.CompletedDownloads.Clear();
                                wvm.CompletedDownloads.Add(new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                                {
                                    Id = "driver_booster",
                                    Title = "Driver Booster 12",
                                    FilePath = "driver_booster_setup.exe",
                                    TimeText = "2:41 PM",
                                    SizeText = "29.9 MB",
                                    DetailsText = "2:41 PM • 29.9 MB",
                                    IconGlyph = "\uE71D",
                                    IconColor = "#D1FE29",
                                    IsInstalled = false,
                                    Kind = XenomOptimizer.ViewModels.DownloadItemKindType.DriverBooster
                                });
                                wvm.CompletedDownloads.Add(new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                                {
                                    Id = "directx",
                                    Title = "DirectX End-User Runtimes",
                                    FilePath = "dxwebsetup.exe",
                                    TimeText = "2:54 PM",
                                    SizeText = "288 KB",
                                    DetailsText = "2:54 PM • 288 KB",
                                    IconGlyph = "\uE7FC",
                                    IconColor = "#38BDF8",
                                    IsInstalled = true,
                                    Kind = XenomOptimizer.ViewModels.DownloadItemKindType.DirectX
                                });
                                wvm.CompletedDownloads.Add(new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                                {
                                    Id = "drv_chipset",
                                    Title = "Motherboard Chipset Driver",
                                    FilePath = "amd_chipset_installer.exe",
                                    TimeText = "2:54 PM",
                                    SizeText = "0.1 MB",
                                    DetailsText = "2:54 PM • 0.1 MB",
                                    IconGlyph = "\uE7F8",
                                    IconColor = "#D1FE29",
                                    IsInstalled = false
                                });
                                wvm.CompletedDownloads.Add(new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                                {
                                    Id = "drv_gpu",
                                    Title = "GPU Graphics Driver",
                                    FilePath = "nvidia_driver_setup.exe",
                                    TimeText = "2:54 PM",
                                    SizeText = "500 MB",
                                    DetailsText = "2:54 PM • 500 MB",
                                    IconGlyph = "\uE7F4",
                                    IconColor = "#D1FE29",
                                    IsInstalled = false
                                });
                                wvm.CompletedDownloads.Add(new XenomOptimizer.ViewModels.DownloadHistoryItemViewModel
                                {
                                    Id = "drv_wireless",
                                    Title = "Wi-Fi & Bluetooth Wireless Drivers",
                                    FilePath = "intel_wireless_setup.exe",
                                    TimeText = "2:54 PM",
                                    SizeText = "120 MB",
                                    DetailsText = "2:54 PM • 120 MB",
                                    IconGlyph = "\uE701",
                                    IconColor = "#D1FE29",
                                    IsInstalled = false
                                });

                                wvm.IsMultiDownloadViewActive = true;
                                wvm.IsSingleDownloadCompletedView = false;
                                wvm.DownloadSubtitleText = $"{wvm.CompletedDownloads.Count} Items Completed • Ready to install";
                                if (e.Args.Contains("--download-flyout-revealed") && wvm.CompletedDownloads.Count > 0)
                                {
                                    wvm.CompletedDownloads[0].IsActionsRevealed = true;
                                }
                            }
                            else if (e.Args.Contains("--download-flyout-single") || wvm.CompletedDownloads.Count == 1)
                            {
                                wvm.IsMultiDownloadViewActive = true;
                                wvm.IsSingleDownloadCompletedView = true;
                                wvm.DownloadSubtitleText = "1 Item Completed • Ready to install";
                            }
                        }

                        if (e.Args.Contains("--guide-modal"))
                        {
                            wvm.UpdateGuideProgress();
                            wvm.IsInstructionModalVisible = true;
                        }

                        if (e.Args.Contains("--guide-modal-experienced"))

                        {

                            wvm.IsInstructionModalVisible = true;

                            wvm.SelectedOnboardingPath = 1;

                        }

                        if (e.Args.Contains("--experienced-mode"))

                        {

                            wvm.SetSetupModeCommand.Execute("Experienced");

                        }

                        if (e.Args.Contains("--new-user-mode"))

                        {

                            wvm.SetSetupModeCommand.Execute("NewUser");

                        }

                        if (e.Args.Contains("--download-completed-slide0"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.IsDownloadCompleted = true;

                            wvm.SelectedSlideIndex = 0;

                        }

                        if (e.Args.Contains("--start-driver-scan"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.StartDriverScanCommand.Execute(null);

                        }


                        if (e.Args.Contains("--driver-scan-completed"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.HasStartedScan = true;

                            wvm.IsScanningDrivers = false;

                            wvm.HasCompletedDriverScan = true;

                            wvm.IsHoverGreenEnabled = true;

                            wvm.IsInstallButtonRevealed = e.Args.Contains("--install-revealed");

                            wvm.MissingDriverCount = 3;

                            wvm.PostScanHeadlineText = "Successful! You Need to Download 3 Drivers";

                            wvm.PostScanSubtitleText = "3 missing driver components detected for your PC";

                        }

                        if (e.Args.Contains("--rescan-cycle"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.HasStartedScan = true;

                            wvm.IsScanningDrivers = false;

                            wvm.HasCompletedDriverScan = true;

                            wvm.IsHoverGreenEnabled = true;

                            wvm.OpenDriverInstallationViewCommand.Execute(null);
                            if (e.Args.Contains("--rescan-active"))
                            {
                                wvm.IsRescanningDrivers = true;
                                foreach (var d in wvm.DriverItems) d.IsRescanning = true;
                            }
                            if (e.Args.Contains("--rescan-done"))
                            {
                                wvm.IsRescanCompleted = true;
                            }
                            if (e.Args.Contains("--driver-booster-prompt"))
                            {
                                wvm.IsDriverBoosterPromptVisible = true;
                            }
                            if (e.Args.Contains("--driver-warning-notice"))
                            {
                                wvm.IsDriverWarningModalVisible = true;
                            }
                            if (e.Args.Contains("--rescan-hidden"))
                            {
                                wvm.HasRescannedDrivers = true;
                                wvm.IsRescanButtonVisible = false;
                            }
                            if (e.Args.Contains("--drivers-downloaded"))
                            {
                                foreach (var d in wvm.DriverItems)
                                {
                                    if (!d.IsInstalled)
                                    {
                                        d.IsDownloaded = true;
                                        d.PrimaryButtonText = "Install";
                                    }
                                }
                                wvm.UpdateDriverActionStates();
                            }
                            if (e.Args.Contains("--drivers-installed"))
                            {
                                foreach (var d in wvm.DriverItems)
                                {
                                    d.IsInstalled = true;
                                    d.PrimaryButtonText = "Installed";
                                }
                                wvm.UpdateDriverActionStates();
                            }

                            if (e.Args.Contains("--rescan-wiping"))
                            {
                                wvm.IsRescanCompleted = true;
                            }

                            wvm.RescanDriversCommand.Execute(null);

                        }

                        if (e.Args.Contains("--driver-installation-view"))

                        {

                            wvm.IsInstructionModalVisible = false;

                            wvm.OpenDriverInstallationViewCommand.Execute(null);
                            if (e.Args.Contains("--rescan-active"))
                            {
                                wvm.IsRescanningDrivers = true;
                                foreach (var d in wvm.DriverItems) d.IsRescanning = true;
                            }
                            if (e.Args.Contains("--rescan-done"))
                            {
                                wvm.IsRescanCompleted = true;
                            }
                            if (e.Args.Contains("--driver-booster-prompt"))
                            {
                                wvm.IsDriverBoosterPromptVisible = true;
                            }
                            if (e.Args.Contains("--driver-warning-notice"))
                            {
                                wvm.IsDriverWarningModalVisible = true;
                            }
                            if (e.Args.Contains("--rescan-hidden"))
                            {
                                wvm.HasRescannedDrivers = true;
                                wvm.IsRescanButtonVisible = false;
                            }
                            if (e.Args.Contains("--drivers-downloaded"))
                            {
                                foreach (var d in wvm.DriverItems)
                                {
                                    if (!d.IsInstalled)
                                    {
                                        d.IsDownloaded = true;
                                        d.PrimaryButtonText = "Install";
                                    }
                                }
                                wvm.UpdateDriverActionStates();
                            }
                            if (e.Args.Contains("--drivers-installed"))
                            {
                                foreach (var d in wvm.DriverItems)
                                {
                                    d.IsInstalled = true;
                                    d.PrimaryButtonText = "Installed";
                                }
                                wvm.UpdateDriverActionStates();
                            }


                        }

                    }

                }

                else if (scanFirst && mvm.CurrentPage is DashboardViewModel dvm)

                {

                    _ = Task.Run(async () =>

                    {

                        await Task.Delay(500);

                        await Dispatcher.InvokeAsync(async () =>

                        {

                            await dvm.ScanSystemCommand.ExecuteAsync(null);

                        });

                    });

                }

            }



            var timer = new System.Windows.Threading.DispatcherTimer

            {

                Interval = TimeSpan.FromSeconds(scanFirst ? 5.0 : 3.0)

            };

            timer.Tick += (s, ev) =>

            {

                timer.Stop();

                try

                {

                    int w = (int)mainWindow.ActualWidth;

                    int h = (int)mainWindow.ActualHeight;

                    if (w <= 0) w = 1180;

                    if (h <= 0) h = 720;

                    if (mainWindow.DataContext is MainViewModel mainVm)
                    {
                        if (e.Args.Contains("--drivers-downloading"))
                        {
                            if (mainVm.WindowsVm.DriverItems.Count > 0)
                            {
                                var d0 = mainVm.WindowsVm.DriverItems[0];
                                d0.IsDownloading = true;
                                d0.DownloadProgressRatio = 0.55;
                            }
                            if (mainVm.WindowsVm.DriverItems.Count > 1)
                            {
                                var d1 = mainVm.WindowsVm.DriverItems[1];
                                d1.IsDownloading = true;
                                d1.DownloadProgressRatio = 0.25;
                            }
                            mainVm.WindowsVm.UpdateDriverActionStates();
                        }
                        if (e.Args.Contains("--drivers-downloaded"))
                        {
                            foreach (var d in mainVm.WindowsVm.DriverItems)
                            {
                                if (!d.IsInstalled)
                                {
                                    d.IsDownloaded = true;
                                    d.PrimaryButtonText = "Install";
                                }
                            }
                            mainVm.WindowsVm.IsDriverBoosterDownloaded = true;
                            mainVm.WindowsVm.UpdateDriverActionStates();
                        }
                        if (e.Args.Contains("--drivers-installing"))
                        {
                            mainVm.WindowsVm.IsInstallingAllDrivers = true;
                            if (mainVm.WindowsVm.DriverItems.Count > 0)
                            {
                                var d0 = mainVm.WindowsVm.DriverItems[0];
                                d0.IsInstalling = true;
                            }
                            mainVm.WindowsVm.UpdateDriverActionStates();
                        }
                        if (e.Args.Contains("--drivers-installed"))
                        {
                            foreach (var d in mainVm.WindowsVm.DriverItems)
                            {
                                d.IsInstalled = true;
                                d.PrimaryButtonText = "Installed";
                            }
                            mainVm.WindowsVm.IsDriverBoosterDownloaded = true;
                            mainVm.WindowsVm.IsDriverBoosterInstalled = true;
                            mainVm.WindowsVm.UpdateDriverActionStates();
                        }

                        if (e.Args.Contains("--scroll-downloads"))
                        {
                            var sv = mainWindow.DownloadsModal?.CompletedDownloadsScrollViewer;
                            sv?.ScrollToVerticalOffset(74);
                        }
                        if (e.Args.Contains("--scroll-downloads-bottom"))
                        {
                            var sv = mainWindow.DownloadsModal?.CompletedDownloadsScrollViewer;
                            sv?.ScrollToBottom();
                        }
                        if (e.Args.Contains("--show-scrollbar"))
                        {
                            var sv = mainWindow.DownloadsModal?.CompletedDownloadsScrollViewer;
                            if (sv != null)
                            {
                                sv.ApplyTemplate();
                                if (sv.Template.FindName("PART_VerticalScrollBar", sv) is System.Windows.Controls.Primitives.ScrollBar sb)
                                {
                                    sb.BeginAnimation(UIElement.OpacityProperty, null);
                                    sb.Opacity = 1.0;
                                    sb.Visibility = Visibility.Visible;
                                }
                            }
                        }

                        if (e.Args.Contains("--scroll-downloads"))
                        {
                            var sv = mainWindow.DownloadsModal?.CompletedDownloadsScrollViewer;
                            sv?.ScrollToVerticalOffset(74);
                        }
                        if (e.Args.Contains("--scroll-downloads-bottom"))
                        {
                            var sv = mainWindow.DownloadsModal?.CompletedDownloadsScrollViewer;
                            sv?.ScrollToBottom();
                        }
                        if (e.Args.Contains("--show-scrollbar"))
                        {
                            var sv = mainWindow.DownloadsModal?.CompletedDownloadsScrollViewer;
                            if (sv != null)
                            {
                                sv.ApplyTemplate();
                                if (sv.Template.FindName("PART_VerticalScrollBar", sv) is System.Windows.Controls.Primitives.ScrollBar sb)
                                {
                                    sb.BeginAnimation(UIElement.OpacityProperty, null);
                                    sb.Opacity = 1.0;
                                    sb.Visibility = Visibility.Visible;
                                }
                            }
                        }
                    }

                    mainWindow.UpdateLayout();

                    var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(

                        w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);

                    rtb.Render(mainWindow);

                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();

                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));



                    string outPath = @"C:\Users\anjul\.gemini\antigravity-ide\brain\42eff9fe-48d3-49fc-bc10-1b1d8b71f66d\gameloop_view_preview.png";

                    var outIndex = Array.IndexOf(e.Args, "--out");

                    if (outIndex >= 0 && outIndex + 1 < e.Args.Length)

                    {

                        outPath = e.Args[outIndex + 1];

                    }



                    var outDir = Path.GetDirectoryName(outPath);

                    if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);



                    using (var fs = System.IO.File.Open(outPath, FileMode.Create))
                    {
                        encoder.Save(fs);
                    }
                    File.WriteAllText(@"C:\Users\anjul\.gemini\antigravity\brain\71b74b8b-8fa5-4dce-a4ef-4fbb8a50d260\shot_success.log", $"Saved: {outPath}");
                }
                catch (Exception ex)
                {
                    File.WriteAllText(@"C:\Users\anjul\.gemini\antigravity\brain\71b74b8b-8fa5-4dce-a4ef-4fbb8a50d260\err_shot.log", ex.ToString());
                }

                Shutdown();

            };

            timer.Start();

            return;

        }



        var splash = new XenomOptimizer.Views.SplashScreenWindow(mainWindow);

        splash.Show();

    }



    private static void ConfigureServices(IServiceCollection services)

    {

        // Services

        services.AddSingleton<ILogService, LogService>();

        services.AddSingleton<IBackupService, BackupService>();

        services.AddSingleton<ISystemDetectionService, SystemDetectionService>();

        services.AddSingleton<IGameLoopDetectionService, GameLoopDetectionService>();

        services.AddSingleton<IRegistryService, RegistryService>();

        services.AddSingleton<IAdminService, AdminService>();

        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddSingleton<IGameLoopOptimizerService, GameLoopOptimizerService>();



        // ViewModels

        services.AddTransient<MainViewModel>();

        services.AddTransient<DashboardViewModel>();

        services.AddSingleton<WindowsOptimizationViewModel>();

        services.AddTransient<GameLoopOptimizationViewModel>();

        services.AddTransient<RegistryOptimizationViewModel>();

        services.AddTransient<PubgOptimizationViewModel>();

        services.AddTransient<NetworkOptimizationViewModel>();

        services.AddTransient<CleanupViewModel>();

        services.AddTransient<BackupRestoreViewModel>();

        services.AddTransient<SystemInfoViewModel>();

        services.AddTransient<SettingsViewModel>();

        services.AddSingleton<OptimizerFrameGenViewModel>();

    }

}

