using System;



using System.Collections.Generic;



using System.ComponentModel;



using System.IO;



using System.Threading.Tasks;



using System.Windows;



using System.Windows.Controls;



using System.Windows.Input;



using System.Windows.Media;



using System.Windows.Media.Animation;



using System.Windows.Media.Imaging;



using System.Windows.Threading;



using XenomOptimizer.ViewModels;







namespace XenomOptimizer.Views;







public partial class WindowsOptimizationView : UserControl



{



    private static readonly List<BitmapSource> _cachedFrames = new();



    private static Task? _preloadTask;







    private DispatcherTimer? _frameTimer;



    private DispatcherTimer? _autoWipeTimer;



    private DispatcherTimer? _startDelayTimer;



    private DispatcherTimer? _endTextDelayTimer;



    private bool _isSuccessStateActive = false;



    private bool _hasWipeStarted = false;



    private bool _hasWipeCompleted = false;



    private int _currentFrameIndex = 0;



    private WindowsOptimizationViewModel? _subscribedVm;







    public WindowsOptimizationView()



    {



        InitializeComponent();



        PrepareEntranceElementsForInitialLoad();



        EnsureFramesPreloaded();

        if (_subscribedVm?.IsDownloadCompleted == true || _subscribedVm?.IsAppUpdateAvailable == true)

        {

            StartDownloadBumpAnimation();

        }



        var cmdArgs = Environment.GetCommandLineArgs();

        if (cmdArgs.Contains("--rescan-wiping"))

        {

            if (CollapsedRescanBtn != null)

            {

                CollapsedRescanBtn.ApplyTemplate();

                if (CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) is Border lime)

                {

                    lime.HorizontalAlignment = HorizontalAlignment.Right;

                    lime.CornerRadius = new CornerRadius(0, 10, 10, 0);

                    lime.Width = 67;

                }

                if (CollapsedRescanBtn.Template?.FindName("RescanDoneContent", CollapsedRescanBtn) is FrameworkElement done) done.Visibility = Visibility.Collapsed;

                if (CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) is FrameworkElement dots) dots.Visibility = Visibility.Collapsed;

            }

        }

        else if (cmdArgs.Contains("--rescan-done"))

        {

            if (CollapsedRescanBtn != null)

            {

                CollapsedRescanBtn.ApplyTemplate();

                if (CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) is Border lime) lime.Width = 134;

                if (CollapsedRescanBtn.Template?.FindName("RescanDoneContent", CollapsedRescanBtn) is FrameworkElement done) done.Visibility = Visibility.Visible;

                if (CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) is FrameworkElement dots) dots.Visibility = Visibility.Collapsed;

            }

        }

        else if (cmdArgs.Contains("--rescan-active"))

        {

            if (CollapsedRescanBtn != null)

            {

                CollapsedRescanBtn.ApplyTemplate();

                if (CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) is Border lime) lime.Width = 134;

                if (CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) is FrameworkElement dots) dots.Visibility = Visibility.Visible;

            }

        }



        if (cmdArgs.Contains("--driver-scroll-test"))

        {

            Loaded += (s, e) =>

            {

                Dispatcher.BeginInvoke(new Action(() =>

                {

                    DriverInstallationScrollViewer?.ScrollToVerticalOffset(120);

                }), System.Windows.Threading.DispatcherPriority.Loaded);

            };

        }



        if (cmdArgs.Contains("--driver-scroll-bottom"))

        {

            Loaded += (s, e) =>

            {

                Dispatcher.BeginInvoke(new Action(() =>

                {

                    DriverInstallationScrollViewer?.ScrollToBottom();

                }), System.Windows.Threading.DispatcherPriority.Loaded);

            };

        }



        Loaded += OnLoaded;



        Unloaded += OnUnloaded;



        DataContextChanged += OnDataContextChanged;



    }



    private void DriverInstallationScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)

    {

        if (TopDriverFadeBorder != null)

        {

            double offset = e.VerticalOffset;

            if (offset <= 0.5)

            {

                TopDriverFadeBorder.Opacity = 0.0;

            }

            else

            {

                TopDriverFadeBorder.Opacity = Math.Min(1.0, offset / 25.0);

            }

        }



        if (BottomDriverFadeBorder != null)

        {

            double remaining = e.ExtentHeight - (e.VerticalOffset + e.ViewportHeight);

            if (remaining <= 0.5)

            {

                BottomDriverFadeBorder.Opacity = 0.0;

            }

            else

            {

                BottomDriverFadeBorder.Opacity = Math.Min(1.0, remaining / 35.0);

            }

        }

    }







    public static void EnsureFramesPreloaded()



    {



        EnsureGuideFramesPreloaded();



        if (_preloadTask != null) return;



        _preloadTask = Task.Run(() =>



        {



            try



            {



                string baseDir = AppDomain.CurrentDomain.BaseDirectory;



                string framesDir = Path.Combine(baseDir, "Assets", "ScannerFrames");



                if (!Directory.Exists(framesDir))



                {



                    framesDir = @"d:\Shop\Xenom Lag Fix\Assets\ScannerFrames";



                }



                if (!Directory.Exists(framesDir)) return;







                var files = Directory.GetFiles(framesDir, "frame_*.png");



                Array.Sort(files);







                var list = new List<BitmapSource>(files.Length);



                foreach (var file in files)



                {



                    var bmp = new BitmapImage();



                    bmp.BeginInit();



                    bmp.UriSource = new Uri(file, UriKind.Absolute);



                    bmp.CacheOption = BitmapCacheOption.OnLoad;



                    bmp.EndInit();



                    bmp.Freeze();



                    list.Add(bmp);



                }







                lock (_cachedFrames)



                {



                    _cachedFrames.Clear();



                    _cachedFrames.AddRange(list);



                }



            }



            catch { }



        });



    }



    public static void EnsureGuideFramesPreloaded()
    {
        InstructionGuideModalControl.EnsureGuideFramesPreloaded();
    }







    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)



    {



        if (_subscribedVm != null)



        {



            _subscribedVm.PropertyChanged -= OnViewModelPropertyChanged;



            _subscribedVm.ScanAnimationRequested -= OnScanAnimationRequested;



            _subscribedVm.LockedButtonClicked -= OnLockedButtonClicked;



            _subscribedVm.LockedSlideRequested -= OnLockedSlideRequested;



            _subscribedVm.RequestEntranceAnimation -= OnRequestEntranceAnimation;



        }







        if (DataContext is WindowsOptimizationViewModel vm)



        {



            _subscribedVm = vm;



            _subscribedVm.PropertyChanged += OnViewModelPropertyChanged;



            _subscribedVm.ScanAnimationRequested += OnScanAnimationRequested;



            _subscribedVm.LockedButtonClicked += OnLockedButtonClicked;



            _subscribedVm.LockedSlideRequested += OnLockedSlideRequested;



            _subscribedVm.RequestEntranceAnimation += OnRequestEntranceAnimation;







            UpdateDriverInstallationVisualState(vm.IsDriverInstallationViewActive, animate: false);

            ApplyHeaderButtonsVisualState(vm.HasCompletedDriverScan, animate: false);







            if (vm.IsScanningDrivers)



            {



                StopShimmerAnimation();



                StartAnimation();



            }



            else if (vm.HasCompletedDriverScan)



            {



                ShowRestingFrame();



            }



            else



            {



                StartShimmerAnimation();



            }



        }



    }







    private void OnScanAnimationRequested(object? sender, EventArgs e)
    {
        _entranceAnimationEpoch++;
        if (PreScanCenterText != null)
        {
            PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
            PreScanCenterText.Visibility = Visibility.Collapsed;
            PreScanCenterText.Opacity = 0.0;
        }
        if (PreScanTextTrans != null)
        {
            PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
            PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
            PreScanTextTrans.X = 0;
            PreScanTextTrans.Y = 0;
        }

        Dispatcher.InvokeAsync(() =>
        {
            StopShimmerAnimation();
            StartAnimation();
        }, DispatcherPriority.Render);
    }







    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)



    {



        if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsScanningDrivers))
        {
            if (_subscribedVm?.IsScanningDrivers == true)
            {
                _entranceAnimationEpoch++;
                if (PreScanCenterText != null)
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }
                if (PreScanTextTrans != null)
                {
                    PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                    PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                    PreScanTextTrans.X = 0;
                    PreScanTextTrans.Y = 0;
                }
                if (PreScanTextTrans != null)
                {
                    PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                    PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                    PreScanTextTrans.X = 0;
                    PreScanTextTrans.Y = 0;
                }

                StopShimmerAnimation();
                StartAnimation();
            }
            else if (_subscribedVm?.HasStartedScan == false)
            {
                StopAnimation();
                StartShimmerAnimation();
            }
        }
        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.HasStartedScan))
        {
            if (_subscribedVm?.HasStartedScan == true)
            {
                _entranceAnimationEpoch++;
                if (PreScanCenterText != null)
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }
                if (PreScanTextTrans != null)
                {
                    PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                    PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                    PreScanTextTrans.X = 0;
                    PreScanTextTrans.Y = 0;
                }
            }
            else if (_subscribedVm?.HasStartedScan == false)
            {
                StopAnimation();
                StartShimmerAnimation();
            }
        }



        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDriverInstallationViewActive))

        {

            if (_subscribedVm != null)

            {

                MainContentScrollViewer?.ScrollToTop();

                DriverInstallationScrollViewer?.ScrollToTop();

                TriggerEntranceAnimation(force: true, animateScrollDots: false, rightColumnOnly: true);

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsInstructionModalVisible))

        {

            if (_subscribedVm?.IsInstructionModalVisible == true)

            {

                PlayGuideOpenAnimation();

            }

            else

            {

                PlayGuideCloseAnimation();

            }

        }



        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.HasCompletedDriverScan))

        {

            if (_subscribedVm?.HasCompletedDriverScan == true)

            {

                ApplyHeaderButtonsVisualState(isScanned: true, animate: true);

            }

            else

            {

                ApplyHeaderButtonsVisualState(isScanned: false, animate: true);

            }

        }



        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.SelectedSlideIndex) ||

                 e.PropertyName == nameof(WindowsOptimizationViewModel.IsDriversIncludedMode))

        {

            MainContentScrollViewer?.ScrollToTop();



            if (_subscribedVm?.IsDriversSlideActive == true && _subscribedVm.HasCompletedDriverScan)

            {

                ShowHeaderStatsImmediate();

            }

            else

            {

                HideHeaderStatsImmediate();

            }



            TriggerEntranceAnimation(force: true, animateScrollDots: false);

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.SelectedAdvanceSubTabIndex))
        {
            AnimateAdvanceSubTabSwitch();
        }



        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsRescanningDrivers))

        {

            if (_subscribedVm?.IsRescanningDrivers == true)

            {

                StartRescanScanningVisual();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsRescanCompleted))

        {

            if (_subscribedVm?.IsRescanCompleted == true)

            {

                StartRescanCompletedVisual();

            }

            else

            {

                ResetRescanVisual();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDownloadingDriverBooster))

        {

            if (_subscribedVm?.IsDownloadingDriverBooster == true)

            {

                StopDownloadBumpAnimation();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDownloadCompleted))

        {

            if (_subscribedVm?.IsDownloadCompleted == true)

            {

                StartDownloadCompletedVisual();

            }

            else

            {

                StopDownloadBumpAnimation();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsAppUpdateAvailable))

        {

            if (_subscribedVm?.IsAppUpdateAvailable == true)

            {

                StartDownloadBumpAnimation();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsInstallingAllDrivers))

        {

            if (_subscribedVm?.IsInstallingAllDrivers == true)

            {

                StopInstallAllDriversBumpAnimation();

            }

            else if (_subscribedVm?.IsDriverInstallationViewActive == true)

            {

                StartInstallAllDriversBumpAnimation();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.AreAllDriversInstalled))

        {

            if (_subscribedVm?.AreAllDriversInstalled == true &&

                !_subscribedVm.IsDriverBoosterDownloaded &&

                !_subscribedVm.IsDriverBoosterInstalled)

            {

                StartDriverBoosterBumpAnimation();

            }

            else

            {

                StopDriverBoosterBumpAnimation();

            }

        }

        else if (e.PropertyName == nameof(WindowsOptimizationViewModel.IsDriverBoosterDownloaded) ||

                 e.PropertyName == nameof(WindowsOptimizationViewModel.IsDriverBoosterInstalled))

        {

            if (_subscribedVm?.IsDriverBoosterDownloaded == true || _subscribedVm?.IsDriverBoosterInstalled == true)

            {

                StopDriverBoosterBumpAnimation();

            }

        }



    }







    private void OnLoaded(object sender, RoutedEventArgs e)



    {



        UpdateCardClip();



        if (CardBackgroundShape != null)



        {



            CardBackgroundShape.SizeChanged += (s, ev) => UpdateCardClip();



        }







        EnsureFramesPreloaded();







        if (_subscribedVm != null)

        {

            UpdateDriverInstallationVisualState(_subscribedVm.IsDriverInstallationViewActive, animate: false);

            ApplyHeaderButtonsVisualState(_subscribedVm.HasCompletedDriverScan, animate: false);



            if (_subscribedVm.IsInstructionModalVisible)

            {

                PlayGuideOpenAnimation();

            }



            if (_subscribedVm.AreAllDriversInstalled && !_subscribedVm.IsDriverBoosterDownloaded && !_subscribedVm.IsDriverBoosterInstalled)

            {

                StartDriverBoosterBumpAnimation();

            }

        }







        if (DataContext is WindowsOptimizationViewModel vm && vm.IsScanningDrivers)



        {



            StopShimmerAnimation();



            StartAnimation();



        }



        else if (DataContext is WindowsOptimizationViewModel vm2 && vm2.HasCompletedDriverScan)



        {



            ShowRestingFrame();



        }



        else



        {



            StartShimmerAnimation();



        }



        if (DataContext is WindowsOptimizationViewModel winVm)

        {

            if (winVm.IsDriverInstallationViewActive)

            {

                UpdateDriverInstallationVisualState(true, animate: false);

                ApplyHeaderButtonsVisualState(winVm.HasCompletedDriverScan, animate: false);

            }

            if (winVm.IsScanningDrivers || winVm.HasStartedScan || winVm.HasCompletedDriverScan)
            {
                if (PreScanCenterText != null)
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }
                if (PreScanTextTrans != null)
                {
                    PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                    PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                    PreScanTextTrans.X = 0;
                    PreScanTextTrans.Y = 0;
                }
            }

            Dispatcher.InvokeAsync(() => TriggerEntranceAnimation(force: true, animateScrollDots: true), DispatcherPriority.Loaded);

        }

    }



    private DateTime _lastEntranceAnimationTime = DateTime.MinValue;
    private int _entranceAnimationEpoch = 0;
    private int _subTabSwitchEpoch = 0;

    private void AnimateAdvanceSubTabSwitch()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.InvokeAsync(AnimateAdvanceSubTabSwitch);
            return;
        }

        if (_subscribedVm == null) return;
        int tab = _subscribedVm.SelectedAdvanceSubTabIndex;
        int currentEpoch = ++_subTabSwitchEpoch;

        // Reset and clear any existing animations cleanly to prevent queuing/freezing on rapid clicks
        AdvanceTabActiveContainer?.BeginAnimation(UIElement.OpacityProperty, null);
        LowEndTabActiveContainer?.BeginAnimation(UIElement.OpacityProperty, null);
        AdvanceTabActiveTranslate?.BeginAnimation(TranslateTransform.XProperty, null);
        AdvanceTabActiveTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
        LowEndTabActiveTranslate?.BeginAnimation(TranslateTransform.XProperty, null);
        LowEndTabActiveTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
        AdvanceTabActiveScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        AdvanceTabActiveScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        LowEndTabActiveScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        LowEndTabActiveScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        AdvanceSubTabCardMask?.BeginAnimation(FrameworkElement.MarginProperty, null);
        AdvanceSubTabCardMask?.BeginAnimation(FrameworkElement.WidthProperty, null);

        AdvanceTweaksPanel?.BeginAnimation(UIElement.OpacityProperty, null);
        LowEndTweaksPanel?.BeginAnimation(UIElement.OpacityProperty, null);
        AdvanceTweaksTranslate?.BeginAnimation(TranslateTransform.XProperty, null);
        AdvanceTweaksTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
        LowEndTweaksTranslate?.BeginAnimation(TranslateTransform.XProperty, null);
        LowEndTweaksTranslate?.BeginAnimation(TranslateTransform.YProperty, null);
        AdvanceTweaksScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        AdvanceTweaksScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        LowEndTweaksScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        LowEndTweaksScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        AdvanceBottomFadeBrd?.BeginAnimation(UIElement.OpacityProperty, null);

        // Apple Dynamic Island fluid spring & ease physics (matching Swith anime.mp4)
        var smoothEaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };
        var smoothEaseIn = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fluidQuartOut = new QuarticEase { EasingMode = EasingMode.EaseOut };
        var springBackOut = new BackEase { Amplitude = 0.20, EasingMode = EasingMode.EaseOut };

        var exitDur = TimeSpan.FromMilliseconds(200);
        var enterDur = TimeSpan.FromMilliseconds(380);
        var morphDur = TimeSpan.FromMilliseconds(390);

        if (tab == 1) // Switch to Low-End PC (Tab 1)
        {
            // 1. Advance Tab Header fades & scales out slightly to the left
            if (AdvanceTabActiveContainer != null)
            {
                AdvanceTabActiveContainer.IsHitTestVisible = false;
                var fadeOut = new DoubleAnimation { To = 0.0, Duration = exitDur, EasingFunction = smoothEaseOut };
                AdvanceTabActiveContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            if (AdvanceTabActiveTranslate != null)
            {
                var slideOut = new DoubleAnimation { To = -18.0, Duration = exitDur, EasingFunction = smoothEaseOut };
                AdvanceTabActiveTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            if (AdvanceTabActiveScale != null)
            {
                var scaleOut = new DoubleAnimation { To = 0.95, Duration = exitDur, EasingFunction = smoothEaseOut };
                AdvanceTabActiveScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                AdvanceTabActiveScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            }

            // 2. Low-End PC Tab Header floats in from right with subtle spring bloom
            if (LowEndTabActiveContainer != null)
            {
                LowEndTabActiveContainer.IsHitTestVisible = true;
                if (LowEndTabActiveTranslate != null)
                {
                    LowEndTabActiveTranslate.X = 22.0;
                    var slideIn = new DoubleAnimation { To = 0.0, Duration = enterDur, EasingFunction = fluidQuartOut };
                    LowEndTabActiveTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);
                }
                if (LowEndTabActiveScale != null)
                {
                    LowEndTabActiveScale.ScaleX = 0.95;
                    LowEndTabActiveScale.ScaleY = 0.95;
                    var scaleIn = new DoubleAnimation { To = 1.0, Duration = enterDur, EasingFunction = springBackOut };
                    LowEndTabActiveScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
                    LowEndTabActiveScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
                }
                var fadeIn = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(320), EasingFunction = smoothEaseOut };
                LowEndTabActiveContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            // 3. Notch Mask smoothly stretches and glides across to under Low-End PC tab
            if (AdvanceSubTabCardMask != null)
            {
                var marginAnim = new ThicknessAnimation { To = new Thickness(256, 0, 0, 0), Duration = morphDur, EasingFunction = fluidQuartOut };
                var widthAnim = new DoubleAnimation { To = 279.0, Duration = morphDur, EasingFunction = fluidQuartOut };
                AdvanceSubTabCardMask.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
                AdvanceSubTabCardMask.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }

            // 4. Container top-left contour adapts
            if (AdvanceSubTabContentContainer != null)
            {
                AdvanceSubTabContentContainer.CornerRadius = new CornerRadius(22, 22, 22, 22);
            }

            // 5. Card Panels: True organic cross-fade with Dynamic Island elastic bloom
            if (AdvanceTweaksPanel != null)
            {
                AdvanceTweaksPanel.IsHitTestVisible = false;
                var fadeOut = new DoubleAnimation { To = 0.0, Duration = exitDur, EasingFunction = smoothEaseIn };
                fadeOut.Completed += (s, e) =>
                {
                    if (_subTabSwitchEpoch == currentEpoch && AdvanceTweaksPanel != null)
                    {
                        AdvanceTweaksPanel.Visibility = Visibility.Collapsed;
                    }
                };
                AdvanceTweaksPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);

                if (AdvanceTweaksTranslate != null)
                {
                    var slideX = new DoubleAnimation { To = -20.0, Duration = exitDur, EasingFunction = smoothEaseIn };
                    var slideY = new DoubleAnimation { To = 6.0, Duration = exitDur, EasingFunction = smoothEaseIn };
                    AdvanceTweaksTranslate.BeginAnimation(TranslateTransform.XProperty, slideX);
                    AdvanceTweaksTranslate.BeginAnimation(TranslateTransform.YProperty, slideY);
                }
                if (AdvanceTweaksScale != null)
                {
                    var scaleAnim = new DoubleAnimation { To = 0.96, Duration = exitDur, EasingFunction = smoothEaseIn };
                    AdvanceTweaksScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    AdvanceTweaksScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            }

            if (LowEndTweaksPanel != null)
            {
                LowEndTweaksPanel.Visibility = Visibility.Visible;
                LowEndTweaksPanel.IsHitTestVisible = true;
                LowEndTweaksPanel.Opacity = 0.0;

                if (LowEndTweaksTranslate != null)
                {
                    LowEndTweaksTranslate.X = 24.0;
                    LowEndTweaksTranslate.Y = 8.0;
                    var slideX = new DoubleAnimation(24.0, 0.0, enterDur) { EasingFunction = fluidQuartOut };
                    var slideY = new DoubleAnimation(8.0, 0.0, enterDur) { EasingFunction = fluidQuartOut };
                    LowEndTweaksTranslate.BeginAnimation(TranslateTransform.XProperty, slideX);
                    LowEndTweaksTranslate.BeginAnimation(TranslateTransform.YProperty, slideY);
                }
                if (LowEndTweaksScale != null)
                {
                    LowEndTweaksScale.ScaleX = 0.96;
                    LowEndTweaksScale.ScaleY = 0.96;
                    var scaleAnim = new DoubleAnimation(0.96, 1.0, enterDur) { EasingFunction = springBackOut };
                    LowEndTweaksScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    LowEndTweaksScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = smoothEaseOut };
                LowEndTweaksPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            // 6. Bottom Fade fades out on Low-End PC
            if (AdvanceBottomFadeBrd != null)
            {
                var fadeOut = new DoubleAnimation { To = 0.0, Duration = exitDur, EasingFunction = smoothEaseOut };
                AdvanceBottomFadeBrd.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
        }
        else // Switch to Advance Tweaks (Tab 0)
        {
            // 1. Low-End PC Tab Header fades & scales out slightly to the right
            if (LowEndTabActiveContainer != null)
            {
                LowEndTabActiveContainer.IsHitTestVisible = false;
                var fadeOut = new DoubleAnimation { To = 0.0, Duration = exitDur, EasingFunction = smoothEaseOut };
                LowEndTabActiveContainer.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            if (LowEndTabActiveTranslate != null)
            {
                var slideOut = new DoubleAnimation { To = 18.0, Duration = exitDur, EasingFunction = smoothEaseOut };
                LowEndTabActiveTranslate.BeginAnimation(TranslateTransform.XProperty, slideOut);
            }
            if (LowEndTabActiveScale != null)
            {
                var scaleOut = new DoubleAnimation { To = 0.95, Duration = exitDur, EasingFunction = smoothEaseOut };
                LowEndTabActiveScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOut);
                LowEndTabActiveScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOut);
            }

            // 2. Advance Tab Header floats in from left with subtle spring bloom
            if (AdvanceTabActiveContainer != null)
            {
                AdvanceTabActiveContainer.IsHitTestVisible = true;
                if (AdvanceTabActiveTranslate != null)
                {
                    AdvanceTabActiveTranslate.X = -22.0;
                    var slideIn = new DoubleAnimation { To = 0.0, Duration = enterDur, EasingFunction = fluidQuartOut };
                    AdvanceTabActiveTranslate.BeginAnimation(TranslateTransform.XProperty, slideIn);
                }
                if (AdvanceTabActiveScale != null)
                {
                    AdvanceTabActiveScale.ScaleX = 0.95;
                    AdvanceTabActiveScale.ScaleY = 0.95;
                    var scaleIn = new DoubleAnimation { To = 1.0, Duration = enterDur, EasingFunction = springBackOut };
                    AdvanceTabActiveScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleIn);
                    AdvanceTabActiveScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleIn);
                }
                var fadeIn = new DoubleAnimation { To = 1.0, Duration = TimeSpan.FromMilliseconds(320), EasingFunction = smoothEaseOut };
                AdvanceTabActiveContainer.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            // 3. Notch Mask slides back under Advance Tweaks tab
            if (AdvanceSubTabCardMask != null)
            {
                var marginAnim = new ThicknessAnimation { To = new Thickness(1, 0, 0, 0), Duration = morphDur, EasingFunction = fluidQuartOut };
                var widthAnim = new DoubleAnimation { To = 323.0, Duration = morphDur, EasingFunction = fluidQuartOut };
                AdvanceSubTabCardMask.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
                AdvanceSubTabCardMask.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
            }

            // 4. Container top-left contour adapts
            if (AdvanceSubTabContentContainer != null)
            {
                AdvanceSubTabContentContainer.CornerRadius = new CornerRadius(0, 22, 22, 22);
            }

            // 5. Card Panels: True organic cross-fade with Dynamic Island elastic bloom
            if (LowEndTweaksPanel != null)
            {
                LowEndTweaksPanel.IsHitTestVisible = false;
                var fadeOut = new DoubleAnimation { To = 0.0, Duration = exitDur, EasingFunction = smoothEaseIn };
                fadeOut.Completed += (s, e) =>
                {
                    if (_subTabSwitchEpoch == currentEpoch && LowEndTweaksPanel != null)
                    {
                        LowEndTweaksPanel.Visibility = Visibility.Collapsed;
                    }
                };
                LowEndTweaksPanel.BeginAnimation(UIElement.OpacityProperty, fadeOut);

                if (LowEndTweaksTranslate != null)
                {
                    var slideX = new DoubleAnimation { To = 20.0, Duration = exitDur, EasingFunction = smoothEaseIn };
                    var slideY = new DoubleAnimation { To = 6.0, Duration = exitDur, EasingFunction = smoothEaseIn };
                    LowEndTweaksTranslate.BeginAnimation(TranslateTransform.XProperty, slideX);
                    LowEndTweaksTranslate.BeginAnimation(TranslateTransform.YProperty, slideY);
                }
                if (LowEndTweaksScale != null)
                {
                    var scaleAnim = new DoubleAnimation { To = 0.96, Duration = exitDur, EasingFunction = smoothEaseIn };
                    LowEndTweaksScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    LowEndTweaksScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            }

            if (AdvanceTweaksPanel != null)
            {
                AdvanceTweaksPanel.Visibility = Visibility.Visible;
                AdvanceTweaksPanel.IsHitTestVisible = true;
                AdvanceTweaksPanel.Opacity = 0.0;

                if (AdvanceTweaksTranslate != null)
                {
                    AdvanceTweaksTranslate.X = -24.0;
                    AdvanceTweaksTranslate.Y = 8.0;
                    var slideX = new DoubleAnimation(-24.0, 0.0, enterDur) { EasingFunction = fluidQuartOut };
                    var slideY = new DoubleAnimation(8.0, 0.0, enterDur) { EasingFunction = fluidQuartOut };
                    AdvanceTweaksTranslate.BeginAnimation(TranslateTransform.XProperty, slideX);
                    AdvanceTweaksTranslate.BeginAnimation(TranslateTransform.YProperty, slideY);
                }
                if (AdvanceTweaksScale != null)
                {
                    AdvanceTweaksScale.ScaleX = 0.96;
                    AdvanceTweaksScale.ScaleY = 0.96;
                    var scaleAnim = new DoubleAnimation(0.96, 1.0, enterDur) { EasingFunction = springBackOut };
                    AdvanceTweaksScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    AdvanceTweaksScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300)) { EasingFunction = smoothEaseOut };
                AdvanceTweaksPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }

            // 6. Bottom Fade fades in on Advance Tweaks
            if (AdvanceBottomFadeBrd != null)
            {
                var fadeIn = new DoubleAnimation { To = 1.0, Duration = enterDur, EasingFunction = smoothEaseOut };
                AdvanceBottomFadeBrd.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        AdvanceSubTabScrollViewer?.ScrollToTop();
    }

    private void PrepareEntranceElementsForInitialLoad()
    {
        try
        {
            PrepareElement(PageHeaderGrid, fromY: -10, fromScale: 0.99);
            PrepareElement(ProfileCardBorder, fromY: -10, fromScale: 0.99);
            PrepareElement(DriverScannerCardGrid, fromY: -10, fromScale: 0.99);
            PrepareElement(DashboardCollapsedBar, fromY: -10, fromScale: 0.99);
            PrepareElement(DriverInstallationHost, fromY: -10, fromScale: 0.99);
            PrepareElement(ServicesSlideHost, fromY: -10, fromScale: 0.99);
            PrepareElement(AdvanceSlideHost, fromY: -10, fromScale: 0.99);



            if (_subscribedVm?.HasCompletedDriverScan == true || _subscribedVm?.IsScanningDrivers == true || _subscribedVm?.HasStartedScan == true)
            {
                if (PreScanCenterText != null)
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }
                if (PostScanCenterText != null)
                {
                    if (_subscribedVm?.HasCompletedDriverScan == true)
                    {
                        PostScanCenterText.Visibility = Visibility.Visible;
                        PostScanCenterText.Opacity = 0.0;
                        if (PostScanTextTrans != null) PostScanTextTrans.Y = 16;
                    }
                    else
                    {
                        PostScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                        PostScanCenterText.Visibility = Visibility.Collapsed;
                        PostScanCenterText.Opacity = 0.0;
                    }
                }
            }
            else
            {
                if (PostScanCenterText != null)
                {
                    PostScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PostScanCenterText.Visibility = Visibility.Collapsed;
                    PostScanCenterText.Opacity = 0.0;
                }
                if (PreScanCenterText != null)
                {
                    PreScanCenterText.Visibility = Visibility.Visible;
                    PreScanCenterText.Opacity = 0.0;
                    if (PreScanTextTrans != null) PreScanTextTrans.Y = 16;
                }
            }

        }

        catch { }

    }



    private void PrepareElement(UIElement? element, double fromY, double fromScale)

    {

        if (element == null) return;

        try

        {

            if (element is FrameworkElement fe)

            {

                fe.RenderTransformOrigin = new Point(0.5, 0.5);

            }

            var tg = new TransformGroup();

            tg.Children.Add(new ScaleTransform(fromScale, fromScale));

            tg.Children.Add(new TranslateTransform(0, fromY));

            element.RenderTransform = tg;

            element.Opacity = 0.0;

        }

        catch { }

    }



    private void OnRequestEntranceAnimation(bool animateScrollDots)

    {

        Dispatcher.InvokeAsync(() => TriggerEntranceAnimation(force: true, animateScrollDots: animateScrollDots), DispatcherPriority.Normal);

    }



    public void TriggerEntranceAnimation(bool force = false, bool animateScrollDots = true, bool rightColumnOnly = false)

    {

        if (DataContext is not WindowsOptimizationViewModel vm) return;



        // Debounce if multiple triggers fire in quick succession (< 200ms), unless forced on initial load

        if (!force && (DateTime.UtcNow - _lastEntranceAnimationTime).TotalMilliseconds < 200)

            return;



        _lastEntranceAnimationTime = DateTime.UtcNow;
        int currentEpoch = ++_entranceAnimationEpoch;



        if (!rightColumnOnly)
        {
            // 1. Top Header Row (Title, Subtitle, 3 Stat Badges, Guide & Download buttons)
            AnimateEntranceItem(PageHeaderGrid, delayMs: 0, fromY: -18, fromScale: 0.98, durationMs: 460, bounce: false);
            if (vm.SelectedSlideIndex == 0)
            {
                ApplyHeaderButtonsVisualState(vm.HasCompletedDriverScan, animate: false);
            }
            else
            {
                HideHeaderStatsImmediate();
            }
        }
        else
        {
            if (vm.SelectedSlideIndex == 0)
            {
                ApplyHeaderButtonsVisualState(vm.HasCompletedDriverScan, animate: false);
            }
            else
            {
                HideHeaderStatsImmediate();
            }
        }

        if (vm.SelectedSlideIndex == 0)
        {
            if (!rightColumnOnly)
            {
                // 2. Main Containers: Left Profile Card
                // Drops down and settles with a lively spring bounce
                AnimateEntranceItem(ProfileCardBorder, delayMs: 60, fromY: -26, fromScale: 0.95, durationMs: 520, bounce: true);

                // 3. Left Profile Card Sub-elements:
                // 3a. Profile Header (Avatar + Xenom name + Windows 11 Pro)
                AnimateEntranceItem(ProfileHeaderGrid, delayMs: 140, fromY: -14, fromScale: 0.96, durationMs: 480, bounce: true);

                // 3b. Hardware Specifications Card (OS, CPU, GPU, RAM, DirectX, Power)
                AnimateEntranceItem(SpecsCardBorder, delayMs: 190, fromY: -16, fromScale: 0.96, durationMs: 500, bounce: true);
                AnimateEntranceItem(SpecsListStack, delayMs: 240, fromY: 12, fromScale: 0.98, durationMs: 440, bounce: false);

                // 3c. Bottom 6 Stat Tiles Card Outer Container
                AnimateEntranceItem(StatsTilesOuterCard, delayMs: 250, fromY: -16, fromScale: 0.96, durationMs: 500, bounce: true);

                // 3d. The 6 Stat Tiles - Cascading 2x3 pairs with lively bounce
                // Row 0: Windows License & OS Version
                AnimateEntranceItem(StatTile1, delayMs: 280, fromY: -14, fromScale: 0.92, durationMs: 480, bounce: true);
                AnimateEntranceItem(StatTile2, delayMs: 320, fromY: -14, fromScale: 0.92, durationMs: 480, bounce: true);

                // Row 1: Processor Cores & RAM Frequency
                AnimateEntranceItem(StatTile3, delayMs: 360, fromY: -14, fromScale: 0.92, durationMs: 480, bounce: true);
                AnimateEntranceItem(StatTile4, delayMs: 400, fromY: -14, fromScale: 0.92, durationMs: 480, bounce: true);

                // Row 2: GPU VRAM & OS Partition Type (NVMe SSD)
                AnimateEntranceItem(StatTile5, delayMs: 440, fromY: -14, fromScale: 0.92, durationMs: 480, bounce: true);
                AnimateEntranceItem(StatTile6, delayMs: 480, fromY: -14, fromScale: 0.92, durationMs: 480, bounce: true);

                if (vm.HasCompletedDriverScan)
                {
                    AnimateHeaderStatsEntrance();
                }
            }

            if (vm.IsDriverInstallationViewActive)
            {
                UpdateDriverInstallationVisualState(true, animate: false);
                AnimateEntranceItem(DashboardCollapsedBar, delayMs: 0, fromY: -16, fromScale: 0.96, durationMs: 480, bounce: true);
                AnimateEntranceItem(DriverInstallationHost, delayMs: 60, fromY: -20, fromScale: 0.95, durationMs: 500, bounce: true);
                StartInstallAllDriversBumpAnimation();
            }
            else
            {
                UpdateDriverInstallationVisualState(false, animate: false);
                // Right Scanner Card: Drops down with lively spring bounce
                AnimateEntranceItem(DriverScannerCardGrid, delayMs: 90, fromY: -26, fromScale: 0.95, durationMs: 520, bounce: true);

                // Scanner Header ("Driver Scanner" + "Skip to Driver Installation >")
                AnimateEntranceItem(ScannerHeaderGrid, delayMs: 160, fromY: -14, fromScale: 0.96, durationMs: 480, bounce: true);

                if (vm.HasCompletedDriverScan)
                {
                    if (PreScanCenterText != null)
                    {
                        PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                        PreScanCenterText.Visibility = Visibility.Collapsed;
                        PreScanCenterText.Opacity = 0.0;
                    }
                    if (PostScanCenterText != null && PostScanTextTrans != null)
                    {
                        PostScanCenterText.Visibility = Visibility.Visible;
                        AnimateTranslateAndFade(PostScanCenterText, PostScanTextTrans, delayMs: 220, fromY: 16, durationMs: 460);
                    }
                }
                else if (vm.IsScanningDrivers || vm.HasStartedScan)
                {
                    if (PreScanCenterText != null)
                    {
                        PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                        PreScanCenterText.Visibility = Visibility.Collapsed;
                        PreScanCenterText.Opacity = 0.0;
                    }
                    if (PostScanCenterText != null)
                    {
                        PostScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                        PostScanCenterText.Visibility = Visibility.Collapsed;
                        PostScanCenterText.Opacity = 0.0;
                    }
                }
                else
                {
                    if (PostScanCenterText != null)
                    {
                        PostScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                        PostScanCenterText.Visibility = Visibility.Collapsed;
                        PostScanCenterText.Opacity = 0.0;
                    }
                    if (PreScanCenterText != null && PreScanTextTrans != null)
                    {
                        PreScanCenterText.Visibility = Visibility.Visible;
                        AnimateTranslateAndFade(PreScanCenterText, PreScanTextTrans, delayMs: 220, fromY: 16, durationMs: 460);
                    }
                    StartShimmerAnimation();
                }

                // Notch Button (Driver Scan / Advanced Drivers)
                AnimateEntranceItem(DriverNotchBtn, delayMs: 300, fromY: 18, fromScale: 0.92, durationMs: 500, bounce: true);

                // Bottom Extra Drivers Card (Driver Booster banner)
                AnimateEntranceItem(DriverBoosterBottomCard, delayMs: 360, fromY: 18, fromScale: 0.95, durationMs: 500, bounce: true);
            }
        }
        else if (vm.SelectedSlideIndex == 1) // SLIDE 1: WINDOWS SERVICES
        {
            HideHeaderStatsImmediate();
            AnimateEntranceItem(ServicesSlideHost, delayMs: 40, fromY: -20, fromScale: 0.96, durationMs: 480, bounce: true);
        }
        else if (vm.SelectedSlideIndex == 2) // SLIDE 2: ADVANCE OPTIMIZATIONS
        {
            HideHeaderStatsImmediate();
            AnimateEntranceItem(AdvanceSlideHost, delayMs: 40, fromY: -20, fromScale: 0.96, durationMs: 480, bounce: true);
        }

        if (!rightColumnOnly)
        {
            // 4. Bottom Pagination Dots (Slide 1 | 2 | 3 pills)
            if (animateScrollDots)
            {
                AnimateEntranceItem(BottomPaginationDotsBorder, delayMs: 30, fromY: 8, fromScale: 0.98, durationMs: 200, bounce: false);
                if (vm.IsDownloadCompleted)
                {
                    StartDownloadBumpAnimation();
                }
            }
            else if (BottomPaginationDotsBorder != null)
            {
                BottomPaginationDotsBorder.BeginAnimation(UIElement.OpacityProperty, null);
                BottomPaginationDotsBorder.Opacity = 1.0;
                BottomPaginationDotsBorder.RenderTransform = null;
            }
        }
        else if (BottomPaginationDotsBorder != null)
        {
            BottomPaginationDotsBorder.BeginAnimation(UIElement.OpacityProperty, null);
            BottomPaginationDotsBorder.Opacity = 1.0;
            BottomPaginationDotsBorder.RenderTransform = null;
        }
    }



    private void AnimateEntranceItem(

        UIElement? element,

        int delayMs,

        double fromY = -22,

        double fromScale = 0.95,

        int durationMs = 500,

        bool bounce = true)

    {

        if (element == null) return;



        if (element is FrameworkElement fe)

        {

            fe.RenderTransformOrigin = new Point(0.5, 0.5);

        }



        var tg = new TransformGroup();

        var scale = new ScaleTransform(fromScale, fromScale);

        var trans = new TranslateTransform(0, fromY);

        tg.Children.Add(scale);

        tg.Children.Add(trans);

        element.RenderTransform = tg;

        element.Opacity = 0.0;



        IEasingFunction ease = bounce

            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 }

            : new CubicEase { EasingMode = EasingMode.EaseOut };



        TimeSpan beginTime = TimeSpan.FromMilliseconds(delayMs);

        TimeSpan duration = TimeSpan.FromMilliseconds(durationMs);



        var opacityAnim = new DoubleAnimation(0.0, 1.0, duration)

        {

            BeginTime = beginTime,

            EasingFunction = ease,

            FillBehavior = FillBehavior.HoldEnd

        };



        var transAnim = new DoubleAnimation(fromY, 0.0, duration)

        {

            BeginTime = beginTime,

            EasingFunction = ease,

            FillBehavior = FillBehavior.HoldEnd

        };



        var scaleXAnim = new DoubleAnimation(fromScale, 1.0, duration)

        {

            BeginTime = beginTime,

            EasingFunction = ease,

            FillBehavior = FillBehavior.HoldEnd

        };



        var scaleYAnim = new DoubleAnimation(fromScale, 1.0, duration)

        {

            BeginTime = beginTime,

            EasingFunction = ease,

            FillBehavior = FillBehavior.HoldEnd

        };



        transAnim.Completed += (s, e) =>

        {

            try

            {

                element.BeginAnimation(UIElement.OpacityProperty, null);

                element.Opacity = 1.0;



                trans.BeginAnimation(TranslateTransform.YProperty, null);

                trans.Y = 0;



                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);

                scale.ScaleX = 1.0;

                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

                scale.ScaleY = 1.0;

            }

            catch { }

        };



        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

        trans.BeginAnimation(TranslateTransform.YProperty, transAnim);

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleXAnim);

        scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleYAnim);

    }



    private void AnimateTranslateAndFade(

        UIElement element,

        TranslateTransform trans,

        int delayMs,

        double fromY = 16,

        int durationMs = 460)

    {
        int captureEpoch = _entranceAnimationEpoch;

        if (element == PreScanCenterText)
        {
            var curVm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;
            if (curVm?.IsScanningDrivers == true || curVm?.HasStartedScan == true || curVm?.HasCompletedDriverScan == true)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
                element.Visibility = Visibility.Collapsed;
                element.Opacity = 0.0;
                trans.BeginAnimation(TranslateTransform.XProperty, null);
                trans.BeginAnimation(TranslateTransform.YProperty, null);
                trans.X = 0;
                trans.Y = 0;
                return;
            }
        }

        element.Opacity = 0.0;

        trans.Y = fromY;



        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 };

        var beginTime = TimeSpan.FromMilliseconds(delayMs);

        var duration = TimeSpan.FromMilliseconds(durationMs);



        var opacityAnim = new DoubleAnimation(0.0, 1.0, duration)

        {

            BeginTime = beginTime,

            EasingFunction = ease,

            FillBehavior = FillBehavior.HoldEnd

        };



        var transAnim = new DoubleAnimation(fromY, 0.0, duration)

        {

            BeginTime = beginTime,

            EasingFunction = ease,

            FillBehavior = FillBehavior.HoldEnd

        };



        transAnim.Completed += (s, e) =>

        {

            try

            {
                if (captureEpoch != _entranceAnimationEpoch)
                {
                    return;
                }

                var curVm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;
                if (element == PreScanCenterText && (curVm?.IsScanningDrivers == true || curVm?.HasStartedScan == true || curVm?.HasCompletedDriverScan == true))
                {
                    element.BeginAnimation(UIElement.OpacityProperty, null);
                    element.Visibility = Visibility.Collapsed;
                    element.Opacity = 0.0;
                    return;
                }

                element.BeginAnimation(UIElement.OpacityProperty, null);

                element.Opacity = 1.0;



                trans.BeginAnimation(TranslateTransform.YProperty, null);

                trans.Y = 0;

            }

            catch { }

        };



        element.BeginAnimation(UIElement.OpacityProperty, opacityAnim);

        trans.BeginAnimation(TranslateTransform.YProperty, transAnim);

    }



    private void OnUnloaded(object sender, RoutedEventArgs e)



    {



        StopInstallAllDriversBumpAnimation();



        StopDownloadBumpAnimation();



        StopDriverBoosterBumpAnimation();



        _autoWipeTimer?.Stop();



        StopAnimation();



        StopShimmerAnimation();



        _lockedLockPhaseTimer?.Stop();

        _lockedTextPhaseTimer?.Stop();

        _lockedGapTimer?.Stop();



    }







    private void StartShimmerAnimation()



    {



        try



        {



            var anim = new DoubleAnimation



            {



                From = -1.2,



                To = 1.2,



                Duration = TimeSpan.FromSeconds(4.0),



                RepeatBehavior = RepeatBehavior.Forever



            };



            ShimmerTransform?.BeginAnimation(TranslateTransform.XProperty, anim);



            PostScanShimmerTransform?.BeginAnimation(TranslateTransform.XProperty, anim);



        }



        catch { }



    }







    private void StopShimmerAnimation()



    {



        try



        {



            ShimmerTransform?.BeginAnimation(TranslateTransform.XProperty, null);



            PostScanShimmerTransform?.BeginAnimation(TranslateTransform.XProperty, null);



        }



        catch { }



    }







    private void UpdateCardClip()



    {



        try



        {



            if (CardBackgroundShape != null && GifHostGrid != null)



            {



                var geom = CardBackgroundShape.RenderedGeometry;



                if (geom != null && !geom.IsEmpty())



                {



                    GifHostGrid.Clip = geom;



                }



            }



        }



        catch { }



    }







    private void StartAnimation()
    {
        try
        {
            if (DriverScannerGifImage == null) return;

            _entranceAnimationEpoch++;
            if (PreScanCenterText != null)
            {
                PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                PreScanCenterText.Visibility = Visibility.Collapsed;
                PreScanCenterText.Opacity = 0.0;
            }
            if (PreScanTextTrans != null)
            {
                PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                PreScanTextTrans.X = 0;
                PreScanTextTrans.Y = 0;
            }

            if (_startDelayTimer != null || _frameTimer?.IsEnabled == true) return;







            DriverScannerGifImage.Source = null;



            DriverScannerGifImage.Opacity = 0;







            _startDelayTimer?.Stop();



            _startDelayTimer = null;



            _endTextDelayTimer?.Stop();



            _endTextDelayTimer = null;



            _frameTimer?.Stop();



            _autoWipeTimer?.Stop();



            AnimatePreScanTextExit();



            StopShimmerAnimation();



            _currentFrameIndex = 0;



            _isSuccessStateActive = false;



            _hasWipeStarted = false;



            _hasWipeCompleted = false;







            if (_subscribedVm != null)



            {



                _subscribedVm.IsHoverGreenEnabled = false;



            }







            if (DriverNotchBtn?.Template?.FindName("BlackFillOverlay", DriverNotchBtn) is Border overlay)



            {



                overlay.BeginAnimation(FrameworkElement.WidthProperty, null);



                overlay.Width = 0;



                overlay.Margin = new Thickness(-3, -2, 0, -2);



                overlay.CornerRadius = new CornerRadius(18, 16, 16, 26);



                overlay.Visibility = Visibility.Collapsed;



            }



            if (DriverNotchBtn?.Template?.FindName("InstallContentGrid", DriverNotchBtn) is Grid installGrid)



            {



                installGrid.Margin = new Thickness(3, 2, 0, 2);



            }



            if (DriverNotchBtn?.Template?.FindName("BaseBorder", DriverNotchBtn) is Border baseBorder)



            {



                baseBorder.BeginAnimation(UIElement.OpacityProperty, null);



                baseBorder.ClearValue(UIElement.OpacityProperty);



                baseBorder.ClearValue(UIElement.VisibilityProperty);



                baseBorder.ClearValue(Border.BackgroundProperty);



                baseBorder.Visibility = Visibility.Visible;



                baseBorder.Opacity = 1;



            }



            if (DriverNotchBtn?.Template?.FindName("SuccessContent", DriverNotchBtn) is FrameworkElement successContent)



            {



                successContent.ClearValue(UIElement.VisibilityProperty);



                successContent.Visibility = Visibility.Collapsed;



            }







            _frameTimer = new DispatcherTimer(DispatcherPriority.Render)



            {



                Interval = TimeSpan.FromMilliseconds(40) // 25 FPS = ~11.92s for 298 frames



            };







            // Distinct 850ms delay before GIF blooms, giving PreScanCenterText time to smoothly wipe to the left, followed by a calm pause



            _startDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(850) };



            _startDelayTimer.Tick += (sDelay, eDelay) =>



            {



                _startDelayTimer.Stop();
                _startDelayTimer = null;

                if (PreScanCenterText != null)
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }

                if (_cachedFrames.Count > 0 && DriverScannerGifImage != null)



                {



                    DriverScannerGifImage.Source = _cachedFrames[0];



                    DriverScannerGifImage.Opacity = 0;



                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))



                    {



                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



                    };



                    DriverScannerGifImage.BeginAnimation(UIElement.OpacityProperty, fadeIn);



                }



                _frameTimer?.Start();



            };



            _startDelayTimer.Start();







            _frameTimer.Tick += (s, ev) =>
            {
                if (PreScanCenterText != null && (PreScanCenterText.Visibility != Visibility.Collapsed || PreScanCenterText.Opacity > 0.0))
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }
                _currentFrameIndex++;







                if (_currentFrameIndex < _cachedFrames.Count)



                {



                    DriverScannerGifImage.Source = _cachedFrames[_currentFrameIndex];



                    // Live progress reaches 100% as Scan anime finishes (frame 257), frames 258-297 are the swallow & vanish finale



                    double pct = Math.Min(100.0, Math.Round((double)_currentFrameIndex / 257.0 * 100.0, 1));



                    _subscribedVm?.UpdateScanProgress(pct);



                }



                else



                {



                    // Animation has reached the final frame (frame 297) - blob has completely vanished into pure black!



                    _frameTimer?.Stop();







                    if (DriverScannerGifImage != null)



                    {



                        DriverScannerGifImage.Source = null;



                    }







                    // Complete the scan in ViewModel



                    if (_subscribedVm != null)



                    {



                        _subscribedVm.IsHoverGreenEnabled = false;



                        _ = _subscribedVm.FinalizeDriverScanAsync();



                    }







                    // Clean pause after blob vanishes before bringing in the success text (matching user request)



                    _endTextDelayTimer?.Stop();



                    _endTextDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };



                    _endTextDelayTimer.Tick += (sEnd, eEnd) =>



                    {



                        _endTextDelayTimer.Stop();



                        _endTextDelayTimer = null;



                        StartShimmerAnimation();



                        AnimatePostScanTextEntrance();



                    };



                    _endTextDelayTimer.Start();







                    // Activate Success state! The button now shows vibrant Lime Green with ✓ Success



                    _isSuccessStateActive = true;



                    _hasWipeStarted = false;



                    _hasWipeCompleted = false;







                    // Ensure initial properties on BlackFillOverlay & BaseBorder for the wipe



                    if (DriverNotchBtn?.Template?.FindName("BlackFillOverlay", DriverNotchBtn) is Border overlayBorder)



                    {



                        overlayBorder.BeginAnimation(FrameworkElement.WidthProperty, null);



                        overlayBorder.Width = 0;



                        overlayBorder.Margin = new Thickness(-3, -2, 0, -2);



                        overlayBorder.CornerRadius = new CornerRadius(18, 16, 16, 26);



                        overlayBorder.Visibility = Visibility.Visible;



                    }



                    if (DriverNotchBtn?.Template?.FindName("InstallContentGrid", DriverNotchBtn) is Grid postInstallGrid)



                    {



                        postInstallGrid.Margin = new Thickness(3, 2, 0, 2);



                    }



                    if (DriverNotchBtn?.Template?.FindName("BaseBorder", DriverNotchBtn) is Border baseBrd)



                    {



                        baseBrd.Opacity = 1;



                        baseBrd.Visibility = Visibility.Visible;



                    }



                    if (DriverNotchBtn?.Template?.FindName("SuccessContent", DriverNotchBtn) is FrameworkElement sc)



                    {



                        sc.Visibility = Visibility.Visible;



                    }







                    // Hold on Lime Success for 1.4s, then smoothly wipe black across the green button



                    _autoWipeTimer?.Stop();



                    _autoWipeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2800) };



                    _autoWipeTimer.Tick += (s2, ev2) =>



                    {



                        _autoWipeTimer.Stop();



                        StartWipeAnimation();



                    };



                    _autoWipeTimer.Start();



                }



            };







            // NOTE: _frameTimer.Start() is intentionally called INSIDE _startDelayTimer.Tick after the 850ms pause!



        }



        catch (Exception ex)



        {



            System.Diagnostics.Debug.WriteLine($"[DriverScanner] Animation error: {ex.Message}");



        }



    }







    private void TriggerEarlyWipe()



    {



        try



        {



            if (_isSuccessStateActive && !_hasWipeStarted)



            {



                _autoWipeTimer?.Stop();



                StartWipeAnimation();



            }



        }



        catch { }



    }







    private void DriverNotchBtn_MouseEnter(object sender, MouseEventArgs e)



    {



        TriggerEarlyWipe();



    }







    private void DriverNotchBtn_MouseMove(object sender, MouseEventArgs e)



    {



        TriggerEarlyWipe();



    }







    private void DriverNotchBtn_PreviewMouseMove(object sender, MouseEventArgs e)



    {



        TriggerEarlyWipe();



    }







    private void DriverNotchBtn_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        TriggerEarlyWipe();

        var vm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;
        if (vm != null && !vm.HasCompletedDriverScan && !vm.IsScanningDrivers && !vm.HasStartedScan)
        {
            _entranceAnimationEpoch++;
            if (PreScanCenterText != null)
            {
                PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                PreScanCenterText.Visibility = Visibility.Collapsed;
                PreScanCenterText.Opacity = 0.0;
            }
            if (PreScanTextTrans != null)
            {
                PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                PreScanTextTrans.X = 0;
                PreScanTextTrans.Y = 0;
            }
        }
    }







    private void DriverNotchBtn_MouseLeave(object sender, MouseEventArgs e)



    {



        try



        {



            if (_hasWipeCompleted)



            {



                // Wipe completed and user cursor left the button.

                // Arm hover green so from now on, hovering turns it signature Lime Green!

                var targetVm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;

                if (targetVm != null)

                {

                    targetVm.IsHoverGreenEnabled = true;

                }



            }



        }



        catch { }



    }







    private void StartWipeAnimation()



    {



        try



        {



            if (_hasWipeStarted) return;



            _hasWipeStarted = true;



            _isSuccessStateActive = false;



            _autoWipeTimer?.Stop();







            var overlay = DriverNotchBtn?.Template?.FindName("BlackFillOverlay", DriverNotchBtn) as Border;



            var baseBorder = DriverNotchBtn?.Template?.FindName("BaseBorder", DriverNotchBtn) as Border;



            var successContent = DriverNotchBtn?.Template?.FindName("SuccessContent", DriverNotchBtn) as FrameworkElement;



            var installGrid = DriverNotchBtn?.Template?.FindName("InstallContentGrid", DriverNotchBtn) as Grid;







            if (overlay == null) return;







            overlay.Visibility = Visibility.Visible;



            if (baseBorder != null)



            {



                baseBorder.Visibility = Visibility.Visible;



                baseBorder.Opacity = 1;



            }







            double startWidth = overlay.ActualWidth;



            if (double.IsNaN(startWidth) || startWidth <= 0 || startWidth > 220)



            {



                startWidth = 0;



            }







            var anim = new DoubleAnimation



            {



                From = startWidth,



                To = 225,



                Duration = TimeSpan.FromMilliseconds(750),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }



            };







            anim.Completed += (s, e) =>



            {



                _hasWipeCompleted = true;







                // Collapse BaseBorder and SuccessContent so absolutely zero green remains underneath



                if (baseBorder != null)



                {



                    baseBorder.Opacity = 0;



                    baseBorder.Visibility = Visibility.Collapsed;



                }



                if (successContent != null)



                {



                    successContent.Visibility = Visibility.Collapsed;



                }







                // Cleanly normalize geometry to standard bounds (Margin=0, Width=220, CornerRadius=16, 16, 16, 24)



                overlay.BeginAnimation(FrameworkElement.WidthProperty, null);



                overlay.Width = 220;



                overlay.Margin = new Thickness(0);



                overlay.CornerRadius = new CornerRadius(16, 16, 16, 24);



                overlay.ClearValue(Border.BackgroundProperty);

                overlay.ClearValue(Border.BorderThicknessProperty);

                overlay.ClearValue(Border.BorderBrushProperty);



                if (installGrid != null)



                {



                    installGrid.Margin = new Thickness(0);



                }







                // Check if mouse cursor is currently over DriverNotchBtn

                var targetVm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;

                if (DriverNotchBtn != null && DriverNotchBtn.IsMouseOver)

                {

                    // 1st time manual trigger: cursor is STILL on the button!

                    // Keep IsHoverGreenEnabled false so it stays pure black until the mouse leaves!

                    if (targetVm != null)

                    {

                        targetVm.IsHoverGreenEnabled = false;

                    }

                }

                else

                {

                    // Auto-wipe completed without cursor on button. Arm hover green immediately!

                    if (targetVm != null)

                    {

                        targetVm.IsHoverGreenEnabled = true;

                    }

                }



            };







            overlay.BeginAnimation(FrameworkElement.WidthProperty, anim);



        }



        catch (Exception ex)



        {



            System.Diagnostics.Debug.WriteLine($"[DriverScanner] Wipe animation error: {ex.Message}");



        }



    }







    private void ShowRestingFrame()



    {



        try



        {



            _frameTimer?.Stop();



            _autoWipeTimer?.Stop();



            _isSuccessStateActive = false;



            _hasWipeStarted = true;



            _hasWipeCompleted = true;







            var targetVm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;

            if (targetVm != null)

            {

                targetVm.IsHoverGreenEnabled = true;

            }







            if (DriverScannerGifImage != null)



            {



                DriverScannerGifImage.Source = null;



            }







            if (DriverNotchBtn?.Template?.FindName("BaseBorder", DriverNotchBtn) is Border baseBorder)



            {



                baseBorder.Visibility = Visibility.Collapsed;



                baseBorder.Opacity = 0;



                baseBorder.ClearValue(Border.BackgroundProperty);



            }



            if (DriverNotchBtn?.Template?.FindName("SuccessContent", DriverNotchBtn) is FrameworkElement successContent)



            {



                successContent.Visibility = Visibility.Collapsed;



            }



            if (DriverNotchBtn?.Template?.FindName("BlackFillOverlay", DriverNotchBtn) is Border overlay)



            {



                overlay.BeginAnimation(FrameworkElement.WidthProperty, null);



                overlay.Width = 220;



                overlay.Margin = new Thickness(0);



                overlay.CornerRadius = new CornerRadius(16, 16, 16, 24);



                overlay.ClearValue(Border.BackgroundProperty);

                overlay.ClearValue(Border.BorderThicknessProperty);

                overlay.ClearValue(Border.BorderBrushProperty);



                overlay.Visibility = Visibility.Visible;



            }



            if (DriverNotchBtn?.Template?.FindName("InstallContentGrid", DriverNotchBtn) is Grid installGrid)



            {



                installGrid.Margin = new Thickness(0);



            }







            if (PostScanCenterText != null)



            {



                PostScanCenterText.Visibility = Visibility.Visible;



                PostScanCenterText.Opacity = 1;



                if (PostScanTextTrans != null) PostScanTextTrans.Y = 0;



                if (PostScanSubText != null && _subscribedVm != null)



                {



                    PostScanSubText.Text = _subscribedVm.PostScanSubtitleText;



                }



            }



            if (PreScanCenterText != null)



            {



                PreScanCenterText.Visibility = Visibility.Collapsed;



                PreScanCenterText.Opacity = 0;



            }







            StartShimmerAnimation();



        }



        catch { }



    }







    private void StopAnimation()



    {



        try



        {



            _startDelayTimer?.Stop();



            _startDelayTimer = null;



            _endTextDelayTimer?.Stop();



            _endTextDelayTimer = null;



            ResetPreScanTextEntrance();



            _frameTimer?.Stop();



            _autoWipeTimer?.Stop();



            _isSuccessStateActive = false;



            _currentFrameIndex = 0;



            if (DriverScannerGifImage != null)



            {



                DriverScannerGifImage.Source = null;



            }



        }



        catch { }



    }







    private void OnLockedButtonClicked(object? sender, EventArgs e)



    {



        Dispatcher.InvokeAsync(() =>



        {



            PlayLockedButtonAnimation();



        }, DispatcherPriority.Render);



    }



    private void OnLockedSlideRequested(object? sender, int slideIndex)

    {

        Dispatcher.InvokeAsync(() =>

        {

            PlayLockedSlideAnimation(slideIndex);

        }, DispatcherPriority.Render);

    }



    private void LockedPaginationDot_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)

    {

        if (DataContext is WindowsOptimizationViewModel vm && ((vm.SelectedSlideIndex == 0 && !vm.CanNavigateToNextSlide) || vm.IsScanningDrivers || vm.IsRescanningDrivers || vm.IsRescanCompleted))

        {

            int slide = 1;

            if (sender is FrameworkElement fe && fe.Tag is string tagStr && int.TryParse(tagStr, out int s))

            {

                slide = s;

            }

            PlayLockedSlideAnimation(slide);

            e.Handled = true;

        }

    }



    private DispatcherTimer? _lockedLockPhaseTimer;

    private DispatcherTimer? _lockedTextPhaseTimer;

    private DispatcherTimer? _lockedGapTimer;

    private bool _isLockedIndicatorActive = false;



    private void PlayLockedSlideAnimation(int slideIndex)

    {

        try

        {

            if (PaginationDotsHost == null || PaginationLockHost == null || PaginationHintBadge == null) return;

            if (PaginationHintText != null && DataContext is WindowsOptimizationViewModel vmText)
            {
                if (vmText.IsScanningDrivers || vmText.IsRescanningDrivers || vmText.IsRescanCompleted)
                {
                    PaginationHintText.Text = "Scanning in progress • Please wait...";
                }
                else
                {
                    PaginationHintText.Text = "Scan to unlock Windows optimizations";
                }
            }



            // Stop any running phase and gap timers

            _lockedLockPhaseTimer?.Stop();

            _lockedTextPhaseTimer?.Stop();

            _lockedGapTimer?.Stop();



            // Snappy Shake KeyFrames for the Lock

            var shakeAnim = new DoubleAnimationUsingKeyFrames

            {

                KeyFrames =

                {

                    new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),

                    new LinearDoubleKeyFrame(-4.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(35))),

                    new LinearDoubleKeyFrame(4.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(75))),

                    new LinearDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(115))),

                    new LinearDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(155))),

                    new LinearDoubleKeyFrame(-1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(195))),

                    new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(235)))

                }

            };



            // If lock is ALREADY showing (user clicked repeatedly during lock phase):

            if (_isLockedIndicatorActive && PaginationLockHost.Visibility == Visibility.Visible && PaginationLockHost.Opacity > 0.3)

            {

                // Re-shake the lock and extend lock phase

                PaginationLockShake?.BeginAnimation(TranslateTransform.XProperty, null);

                PaginationLockShake?.BeginAnimation(TranslateTransform.XProperty, shakeAnim);



                _lockedLockPhaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };

                _lockedLockPhaseTimer.Tick += OnLockPhaseCompleted;

                _lockedLockPhaseTimer.Start();

                return;

            }



            _isLockedIndicatorActive = true;



            // Make sure hint text is collapsed/reset

            PaginationHintBadge.BeginAnimation(UIElement.OpacityProperty, null);

            PaginationHintBadge.Opacity = 0.0;

            PaginationHintBadge.Visibility = Visibility.Collapsed;



            // 1. Hide Pagination Dots (Fade out + subtle scale down)

            PaginationDotsHost.BeginAnimation(UIElement.OpacityProperty, null);

            PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);

            PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);



            var fadeOutDots = new DoubleAnimation

            {

                From = PaginationDotsHost.Opacity,

                To = 0.0,

                Duration = TimeSpan.FromMilliseconds(130),

                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

            };



            var scaleDownDots = new DoubleAnimation

            {

                To = 0.75,

                Duration = TimeSpan.FromMilliseconds(130),

                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

            };



            fadeOutDots.Completed += (s, ev) =>

            {

                PaginationDotsHost.Visibility = Visibility.Collapsed;

            };



            PaginationDotsHost.BeginAnimation(UIElement.OpacityProperty, fadeOutDots);

            PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDownDots);

            PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDownDots);



            // 2. Show Lock Host in the center (Fade in + Bouncy Pop Scale + Snappy Shake)

            PaginationLockHost.Visibility = Visibility.Visible;

            PaginationLockHost.BeginAnimation(UIElement.OpacityProperty, null);

            PaginationLockScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);

            PaginationLockScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);

            PaginationLockShake?.BeginAnimation(TranslateTransform.XProperty, null);



            var fadeInLock = new DoubleAnimation

            {

                From = 0.0,

                To = 1.0,

                Duration = TimeSpan.FromMilliseconds(160),

                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

            };



            var popScaleLock = new DoubleAnimationUsingKeyFrames

            {

                KeyFrames =

                {

                    new DiscreteDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.Zero)),

                    new SplineDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160)), new KeySpline(0.1, 0.9, 0.2, 1.0)),

                    new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)), new KeySpline(0.1, 0.9, 0.2, 1.0))

                }

            };



            PaginationLockHost.BeginAnimation(UIElement.OpacityProperty, fadeInLock);

            PaginationLockScale?.BeginAnimation(ScaleTransform.ScaleXProperty, popScaleLock);

            PaginationLockScale?.BeginAnimation(ScaleTransform.ScaleYProperty, popScaleLock);

            PaginationLockShake?.BeginAnimation(TranslateTransform.XProperty, shakeAnim);



            // Step 1: Let the lock stay and shake for 750ms, then fade it out and show the text

            _lockedLockPhaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };

            _lockedLockPhaseTimer.Tick += OnLockPhaseCompleted;

            _lockedLockPhaseTimer.Start();

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[LockedSlide] Error: {ex.Message}");

        }

    }



    private void OnLockPhaseCompleted(object? sender, EventArgs e)

    {

        _lockedLockPhaseTimer?.Stop();



        try

        {

            if (PaginationLockHost == null || PaginationHintBadge == null) return;



            // 1. Fade out Lock completely (250ms)

            var fadeOutLock = new DoubleAnimation

            {

                From = PaginationLockHost.Opacity,

                To = 0.0,

                Duration = TimeSpan.FromMilliseconds(250),

                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

            };

            fadeOutLock.Completed += (s, ev) =>

            {

                PaginationLockHost.Visibility = Visibility.Collapsed;

                if (!_isLockedIndicatorActive) return;



                // Brief clean gap (100ms) after lock disappears before text appears

                _lockedGapTimer?.Stop();

                _lockedGapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };

                _lockedGapTimer.Tick += (gs, gev) =>

                {

                    _lockedGapTimer.Stop();

                    if (!_isLockedIndicatorActive) return;



                    // 2. Show Hint Text in that EXACT same spot (Smooth Fade in: 550ms)

                    PaginationHintBadge.Visibility = Visibility.Visible;

                    PaginationHintBadge.BeginAnimation(UIElement.OpacityProperty, null);



                    var fadeInText = new DoubleAnimation

                    {

                        From = 0.0,

                        To = 1.0,

                        Duration = TimeSpan.FromMilliseconds(420),

                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

                    };

                    PaginationHintBadge.BeginAnimation(UIElement.OpacityProperty, fadeInText);



                    // Keep text fully visible for 1350ms so user can comfortably read it

                    _lockedTextPhaseTimer?.Stop();

                    _lockedTextPhaseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1350) };

                    _lockedTextPhaseTimer.Tick += OnTextPhaseCompleted;

                    _lockedTextPhaseTimer.Start();

                };

                _lockedGapTimer.Start();

            };

            PaginationLockHost.BeginAnimation(UIElement.OpacityProperty, fadeOutLock);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[LockedSlide] LockPhase error: {ex.Message}");

        }

    }



    private void OnTextPhaseCompleted(object? sender, EventArgs e)

    {

        _lockedTextPhaseTimer?.Stop();



        try

        {

            if (PaginationHintBadge == null || PaginationDotsHost == null) return;



            // 1. Fade out Hint Text completely first (500ms)

            var fadeOutText = new DoubleAnimation

            {

                From = PaginationHintBadge.Opacity,

                To = 0.0,

                Duration = TimeSpan.FromMilliseconds(500),

                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

            };

            fadeOutText.Completed += (s, ev) =>

            {

                PaginationHintBadge.Visibility = Visibility.Collapsed;

                PaginationHintBadge.Opacity = 0.0;

                if (!_isLockedIndicatorActive) return;



                // 2. Snappy & smooth gap between text disappearing and scroll dots showing (150ms)

                _lockedGapTimer?.Stop();

                _lockedGapTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };

                _lockedGapTimer.Tick += (ds, dev) =>

                {

                    _lockedGapTimer.Stop();

                    if (!_isLockedIndicatorActive) return;



                    // 3. Smoothly restore Pagination Dots (400ms)

                    PaginationDotsHost.Visibility = Visibility.Visible;

                    PaginationDotsHost.BeginAnimation(UIElement.OpacityProperty, null);

                    PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleXProperty, null);

                    PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleYProperty, null);



                    var fadeInDots = new DoubleAnimation

                    {

                        From = 0.0,

                        To = 1.0,

                        Duration = TimeSpan.FromMilliseconds(400),

                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

                    };

                    var scaleInDots = new DoubleAnimation

                    {

                        From = 0.85,

                        To = 1.0,

                        Duration = TimeSpan.FromMilliseconds(400),

                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

                    };

                    fadeInDots.Completed += (fs, fev) =>

                    {

                        _isLockedIndicatorActive = false;

                    };



                    PaginationDotsHost.BeginAnimation(UIElement.OpacityProperty, fadeInDots);

                    PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleXProperty, scaleInDots);

                    PaginationDotsScale?.BeginAnimation(ScaleTransform.ScaleYProperty, scaleInDots);

                };

                _lockedGapTimer.Start();

            };

            PaginationHintBadge.BeginAnimation(UIElement.OpacityProperty, fadeOutText);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[LockedSlide] TextPhase error: {ex.Message}");

            _isLockedIndicatorActive = false;

        }

    }







    private DispatcherTimer? _lockSettleTimer;



    private DispatcherTimer? _lockBodyTimer;







    private void PlayLockedButtonAnimation()



    {



        try



        {



            if (DriverBoosterBottomBtn == null) return;



            DriverBoosterBottomBtn.ApplyTemplate();







            var shakeTrans = DriverBoosterBottomBtn.Template?.FindName("ButtonShakeTrans", DriverBoosterBottomBtn) as TranslateTransform;



            var chevron = DriverBoosterBottomBtn.Template?.FindName("ChevronGlyph", DriverBoosterBottomBtn) as TextBlock;



            var arrow = DriverBoosterBottomBtn.Template?.FindName("ArrowGlyph", DriverBoosterBottomBtn) as TextBlock;



            var lockContainer = DriverBoosterBottomBtn.Template?.FindName("LockIconContainer", DriverBoosterBottomBtn) as Grid;



            var shackleRot = DriverBoosterBottomBtn.Template?.FindName("ShackleRot", DriverBoosterBottomBtn) as RotateTransform;



            var shackleTrans = DriverBoosterBottomBtn.Template?.FindName("ShackleTrans", DriverBoosterBottomBtn) as TranslateTransform;



            var clickSparks = DriverBoosterBottomBtn.Template?.FindName("ClickSparks", DriverBoosterBottomBtn) as FrameworkElement;



            var sparksScale = DriverBoosterBottomBtn.Template?.FindName("SparksScale", DriverBoosterBottomBtn) as ScaleTransform;



            var capsule = DriverBoosterBottomBtn.Template?.FindName("Capsule", DriverBoosterBottomBtn) as Border;



            var lockBody = DriverBoosterBottomBtn.Template?.FindName("LockBody", DriverBoosterBottomBtn) as Border;



            var scanText = DriverBoosterBottomBtn.Template?.FindName("ScanToUnlockText", DriverBoosterBottomBtn) as TextBlock;







            if (shakeTrans == null || lockContainer == null || shackleRot == null || shackleTrans == null || clickSparks == null)



            {



                return;



            }







            // Stop any running timers from previous clicks



            _lockSettleTimer?.Stop();



            _lockBodyTimer?.Stop();







            // CRITICAL: Clear all previous animations so 2nd, 3rd, and subsequent clicks always re-trigger cleanly!



            lockContainer.BeginAnimation(UIElement.OpacityProperty, null);



            shackleTrans.BeginAnimation(TranslateTransform.YProperty, null);



            shackleRot.BeginAnimation(RotateTransform.AngleProperty, null);



            clickSparks.BeginAnimation(UIElement.OpacityProperty, null);



            if (sparksScale != null)



            {



                sparksScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);



                sparksScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);



            }



            shakeTrans.BeginAnimation(TranslateTransform.XProperty, null);



            shakeTrans.X = 0;







            if (scanText != null)



            {



                scanText.BeginAnimation(TextBlock.ForegroundProperty, null);



            }







            // Pulse red locked border outline on Capsule (without modifying WidthProperty so hover expansion is NEVER broken!)



            if (capsule != null)



            {



                capsule.BorderBrush = new SolidColorBrush(Color.FromArgb(0xDD, 0xEF, 0x44, 0x44));



                var borderAnim = new ColorAnimation



                {



                    From = Color.FromArgb(0xDD, 0xEF, 0x44, 0x44),



                    To = Colors.Transparent,



                    Duration = TimeSpan.FromMilliseconds(750),



                    BeginTime = TimeSpan.FromMilliseconds(100)



                };



                capsule.BorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, borderAnim);



            }







            // Subtle text warning tint



            if (scanText != null)



            {



                scanText.Foreground = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));



                var textColAnim = new ColorAnimation



                {



                    From = Color.FromRgb(0xEF, 0x44, 0x44),



                    To = Color.FromRgb(0x8E, 0x95, 0xA5),



                    Duration = TimeSpan.FromMilliseconds(700),



                    BeginTime = TimeSpan.FromMilliseconds(300)



                };



                scanText.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, textColAnim);



            }







            // Swap glyphs: fully hide and collapse chevron & arrow so they NEVER show under the lock!



            if (chevron != null)



            {



                chevron.BeginAnimation(UIElement.OpacityProperty, null);



                chevron.Opacity = 0;



                chevron.Visibility = Visibility.Collapsed;



            }



            if (arrow != null)



            {



                arrow.BeginAnimation(UIElement.OpacityProperty, null);



                arrow.Opacity = 0;



                arrow.Visibility = Visibility.Collapsed;



            }



            lockContainer.Opacity = 1.0;







            // Phase 1: Open / Tilted Padlock state (matching frames 0-40 of lock button.gif)



            shackleTrans.Y = -4;



            shackleRot.Angle = -20;







            if (lockBody != null)



            {



                lockBody.Background = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));



            }







            clickSparks.Opacity = 0;



            if (sparksScale != null)



            {



                sparksScale.ScaleX = 0.5;



                sparksScale.ScaleY = 0.5;



            }







            // Phase 2: Snap shut at 70ms with BounceEase



            var snapTrans = new DoubleAnimation



            {



                From = -4,



                To = 0,



                Duration = TimeSpan.FromMilliseconds(110),



                BeginTime = TimeSpan.FromMilliseconds(70),



                EasingFunction = new BounceEase { Bounces = 1, Bounciness = 2, EasingMode = EasingMode.EaseOut }



            };







            var snapRot = new DoubleAnimation



            {



                From = -20,



                To = 0,



                Duration = TimeSpan.FromMilliseconds(90),



                BeginTime = TimeSpan.FromMilliseconds(70),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };







            _lockBodyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };



            _lockBodyTimer.Tick += (s, ev) =>



            {



                _lockBodyTimer.Stop();



                if (lockBody != null)



                {



                    lockBody.Background = Brushes.White;



                }



            };



            _lockBodyTimer.Start();







            // Phase 3: Radiating Click Sparks burst (like frame 55 in lock button.gif)



            var sparksOpacityAnim = new DoubleAnimationUsingKeyFrames



            {



                BeginTime = TimeSpan.FromMilliseconds(150),



                KeyFrames =



                {



                    new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),



                    new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(30))),



                    new LinearDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))



                }



            };







            var sparksScaleAnim = new DoubleAnimation



            {



                From = 0.5,



                To = 1.3,



                Duration = TimeSpan.FromMilliseconds(180),



                BeginTime = TimeSpan.FromMilliseconds(150),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };







            // Phase 4: Horizontal locked shake (+/-5px within 10px buffer so absolutely zero clipping occurs)



            var shakeAnim = new DoubleAnimationUsingKeyFrames



            {



                BeginTime = TimeSpan.FromMilliseconds(160),



                KeyFrames =



                {



                    new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),



                    new LinearDoubleKeyFrame(-5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(50))),



                    new LinearDoubleKeyFrame(5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))),



                    new LinearDoubleKeyFrame(-3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150))),



                    new LinearDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))),



                    new LinearDoubleKeyFrame(-1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))),



                    new LinearDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))),



                    new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(350)))



                }



            };







            shackleTrans.BeginAnimation(TranslateTransform.YProperty, snapTrans);



            shackleRot.BeginAnimation(RotateTransform.AngleProperty, snapRot);



            clickSparks.BeginAnimation(UIElement.OpacityProperty, sparksOpacityAnim);



            if (sparksScale != null)



            {



                sparksScale.BeginAnimation(ScaleTransform.ScaleXProperty, sparksScaleAnim);



                sparksScale.BeginAnimation(ScaleTransform.ScaleYProperty, sparksScaleAnim);



            }



            shakeTrans.BeginAnimation(TranslateTransform.XProperty, shakeAnim);







            // Phase 5: Settle & return to resting or hovered state after 1800ms (increased as requested)



            _lockSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1800) };



            _lockSettleTimer.Tick += (s, ev) =>



            {



                _lockSettleTimer.Stop();







                var fadeOutLock = new DoubleAnimation



                {



                    From = 1.0,



                    To = 0.0,



                    Duration = TimeSpan.FromMilliseconds(350),



                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



                };







                fadeOutLock.Completed += (s2, ev2) =>



                {



                    // Cleanly clear animation clock so next click is 100% responsive



                    lockContainer.BeginAnimation(UIElement.OpacityProperty, null);



                    lockContainer.Opacity = 0;



                    shackleTrans.Y = 0;



                    shackleRot.Angle = 0;







                    // Revert to arrow if hovered, chevron if not (restore Visibility & Opacity cleanly)



                    if (DriverBoosterBottomBtn != null && DriverBoosterBottomBtn.IsMouseOver)



                    {



                        if (arrow != null)



                        {



                            arrow.Visibility = Visibility.Visible;



                            arrow.Opacity = 1;



                        }



                        if (chevron != null)



                        {



                            chevron.Visibility = Visibility.Visible;



                            chevron.Opacity = 0;



                        }



                    }



                    else



                    {



                        if (chevron != null)



                        {



                            chevron.Visibility = Visibility.Visible;



                            chevron.Opacity = 1;



                        }



                        if (arrow != null)



                        {



                            arrow.Visibility = Visibility.Visible;



                            arrow.Opacity = 0;



                        }



                    }



                };







                lockContainer.BeginAnimation(UIElement.OpacityProperty, fadeOutLock);



            };



            _lockSettleTimer.Start();



        }



        catch (Exception ex)



        {



            System.Diagnostics.Debug.WriteLine($"[LockedButton] Error: {ex.Message}");



        }



    }







    private async void TriggerPostScanSubtitleTypewriter()



    {



        try



        {



            if (PostScanSubText == null || _subscribedVm == null) return;



            string fullText = _subscribedVm.PostScanSubtitleText;



            if (string.IsNullOrEmpty(fullText)) return;







            // Wait 280ms so the headline floats up into position first



            PostScanSubText.Text = "";



            await Task.Delay(280);







            for (int i = 1; i <= fullText.Length; i++)



            {



                if (_subscribedVm?.HasCompletedDriverScan != true) break;



                PostScanSubText.Text = fullText.Substring(0, i);



                await Task.Delay(35);



            }



        }



        catch { }



    }











    private void AnimatePreScanTextExit()
    {
        try
        {
            if (PreScanCenterText == null) return;

            var curVm = DataContext as WindowsOptimizationViewModel ?? _subscribedVm;
            if (curVm?.IsScanningDrivers == true || curVm?.HasStartedScan == true || PreScanCenterText.Visibility == Visibility.Collapsed || PreScanCenterText.Opacity <= 0.05)
            {
                PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                PreScanCenterText.Visibility = Visibility.Collapsed;
                PreScanCenterText.Opacity = 0.0;
                if (PreScanTextTrans != null)
                {
                    PreScanTextTrans.BeginAnimation(TranslateTransform.XProperty, null);
                    PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);
                    PreScanTextTrans.X = 0;
                    PreScanTextTrans.Y = 0;
                }
                return;
            }

            PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
            if (PreScanTextTrans != null) PreScanTextTrans.BeginAnimation(TranslateTransform.YProperty, null);

            var trans = PreScanCenterText.RenderTransform as TranslateTransform;
            if (trans == null)
            {
                trans = new TranslateTransform(0, 0);
                PreScanCenterText.RenderTransform = trans;
            }

            var slideOut = new DoubleAnimation(0, -80, TimeSpan.FromMilliseconds(340))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var fadeOut = new DoubleAnimation(PreScanCenterText.Opacity > 0 ? PreScanCenterText.Opacity : 1.0, 0, TimeSpan.FromMilliseconds(290))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (s, e) =>
            {
                PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                PreScanCenterText.Visibility = Visibility.Collapsed;
                PreScanCenterText.Opacity = 0.0;
            };

            trans.BeginAnimation(TranslateTransform.XProperty, slideOut);
            PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }
        catch { }
    }







    private void ResetPreScanTextEntrance()



    {



        try
        {
            if (_subscribedVm?.HasCompletedDriverScan == true || _subscribedVm?.IsScanningDrivers == true || _subscribedVm?.HasStartedScan == true)
            {
                if (PreScanCenterText != null)
                {
                    PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, null);
                    PreScanCenterText.Visibility = Visibility.Collapsed;
                    PreScanCenterText.Opacity = 0.0;
                }
                return;
            }

            if (PreScanCenterText == null) return;

            if (PostScanCenterText != null)
            {
                PostScanCenterText.Visibility = Visibility.Collapsed;
                PostScanCenterText.Opacity = 0.0;
            }

            PreScanCenterText.Visibility = Visibility.Visible;







            var trans = PreScanCenterText.RenderTransform as TranslateTransform;



            if (trans == null)



            {



                trans = new TranslateTransform(0, 0);



                PreScanCenterText.RenderTransform = trans;



            }







            var slideIn = new DoubleAnimation(-45, 0, TimeSpan.FromMilliseconds(380))



            {



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };



            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(320))



            {



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };







            trans.BeginAnimation(TranslateTransform.XProperty, slideIn);



            PreScanCenterText.BeginAnimation(UIElement.OpacityProperty, fadeIn);







            if (PostScanCenterText != null)



            {



                PostScanCenterText.Visibility = Visibility.Collapsed;



                PostScanCenterText.Opacity = 0;



            }



        }



        catch { }



    }







    private void AnimatePostScanTextEntrance()



    {



        try
        {
            if (PreScanCenterText != null)
            {
                PreScanCenterText.Visibility = Visibility.Collapsed;
                PreScanCenterText.Opacity = 0.0;
            }

            if (PostScanCenterText == null) return;

            PostScanCenterText.Visibility = Visibility.Visible;







            var trans = PostScanTextTrans;



            if (trans == null)



            {



                trans = new TranslateTransform(0, 20);



                PostScanCenterText.RenderTransform = trans;



            }







            var floatUp = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(420))



            {



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };



            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))



            {



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };







            trans.BeginAnimation(TranslateTransform.YProperty, floatUp);



            PostScanCenterText.BeginAnimation(UIElement.OpacityProperty, fadeIn);







            TriggerPostScanSubtitleTypewriter();



        }



        catch { }



    }











    private void DriverInstallationScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)

    {

        if (sender is ScrollViewer sv)

        {

            sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.6));

            e.Handled = true;

        }

    }

    private void AdvanceSlideHost_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (AdvanceSubTabScrollViewer != null && AdvanceSubTabScrollViewer.ScrollableHeight > 0)
        {
            AdvanceSubTabScrollViewer.ScrollToVerticalOffset(AdvanceSubTabScrollViewer.VerticalOffset - (e.Delta * 0.6));
            e.Handled = true;
        }
    }

    private void AdvanceSubTabScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.ScrollableHeight > 0)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - (e.Delta * 0.6));
            e.Handled = true;
        }
    }

    private double _targetServicesScrollOffset = 0.0;

    private void ServicesItemsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.ScrollableHeight > 0)
        {
            e.Handled = true;

            // If user scrolled via scrollbar thumb or large difference, re-anchor
            if (Math.Abs(_targetServicesScrollOffset - sv.VerticalOffset) > 60)
            {
                _targetServicesScrollOffset = sv.VerticalOffset;
            }

            // Gentle slow step size per wheel notch (26px)
            double scrollStep = (e.Delta / 120.0) * 26.0;
            _targetServicesScrollOffset = Math.Clamp(_targetServicesScrollOffset - scrollStep, 0, sv.ScrollableHeight);

            var animation = new DoubleAnimation
            {
                From = sv.VerticalOffset,
                To = _targetServicesScrollOffset,
                Duration = TimeSpan.FromMilliseconds(240),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            sv.BeginAnimation(XenomOptimizer.Converters.ScrollBarAutoHideHelper.AnimatedVerticalOffsetProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }
    }



    private DateTime _lastSlideWheelTime = DateTime.MinValue;



    private void PaginationBar_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)

    {

        if (DataContext is WindowsOptimizationViewModel vm)

        {

            if (((vm.SelectedSlideIndex == 0 && !vm.CanNavigateToNextSlide) || vm.IsScanningDrivers || vm.IsRescanningDrivers || vm.IsRescanCompleted) && e.Delta < 0)

            {

                PlayLockedSlideAnimation(1);

                e.Handled = true;

                return;

            }

            if (vm.IsScanningDrivers || vm.IsRescanningDrivers || vm.IsRescanCompleted)

            {

                e.Handled = true;

                return;

            }



            if ((DateTime.Now - _lastSlideWheelTime).TotalMilliseconds < 200)

            {

                e.Handled = true;

                return;

            }



            if (e.Delta < 0)

            {

                _lastSlideWheelTime = DateTime.Now;

                vm.SelectNextSlide();

            }

            else if (e.Delta > 0)

            {

                _lastSlideWheelTime = DateTime.Now;

                vm.SelectPreviousSlide();

            }



            e.Handled = true;

        }

    }











    #region Collapsible Driver Hub Bar Transition (rescan.mp4 Fluid Animation)







    private Storyboard? _activeTransitionStoryboard;







    private void UpdateDriverInstallationVisualState(bool isActive, bool animate)



    {



        if (ExpandedScannerHost == null || CollapsedScannerHost == null ||



            DashboardCollapsedBar == null || DriverInstallationHost == null) return;







        if (!animate)



        {



            _activeTransitionStoryboard?.Stop();



            _activeTransitionStoryboard = null;







            if (isActive)

            {

                ExpandedScannerHost.Visibility = Visibility.Collapsed;

                ExpandedScannerHost.Opacity = 0;

                ExpandedScannerHost.IsHitTestVisible = false;



                CollapsedScannerHost.Visibility = Visibility.Visible;

                CollapsedScannerHost.Opacity = 1;

                DashboardCollapsedBar.IsHitTestVisible = true;

                StartInstallAllDriversBumpAnimation();

            }



            else



            {



                StopInstallAllDriversBumpAnimation();



                CollapsedScannerHost.Visibility = Visibility.Collapsed;



                CollapsedScannerHost.Opacity = 0;



                DashboardCollapsedBar.IsHitTestVisible = false;







                ExpandedScannerHost.Visibility = Visibility.Visible;



                ExpandedScannerHost.Opacity = 1;



                ExpandedScannerHost.IsHitTestVisible = true;



                if (DriverScannerCardGrid != null) DriverScannerCardGrid.Height = 582;



                if (DriverBoosterBottomCard != null) DriverBoosterBottomCard.Opacity = 1;



                if (_subscribedVm?.HasCompletedDriverScan == true)



                {



                    ShowRestingFrame();



                }



                UpdateCardClip();



            }



            return;



        }







        if (isActive)



        {



            AnimateCollapseToBar();



        }



        else



        {



            AnimateExpandToDashboard();



        }



    }







    private void AnimateCollapseToBar()



    {



        if (ExpandedScannerHost == null || CollapsedScannerHost == null ||



            DashboardCollapsedBar == null || DriverInstallationHost == null) return;







        _activeTransitionStoryboard?.Stop();







        CollapsedScannerHost.Visibility = Visibility.Visible;



        CollapsedScannerHost.Opacity = 1;



        DashboardCollapsedBar.Opacity = 0;



        DashboardCollapsedBar.IsHitTestVisible = false;







        ExpandedScannerHost.IsHitTestVisible = false;







        DriverInstallationHost.Opacity = 0;



        if (DriverInstallationHostTranslate != null)



        {



            DriverInstallationHostTranslate.Y = 24;



        }







        var sb = new Storyboard();







        // 1. Fade out bottom booster card quickly



        if (DriverBoosterBottomCard != null)



        {



            var fadeOutBooster = new DoubleAnimation



            {



                To = 0.0,



                Duration = TimeSpan.FromSeconds(0.12),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };



            Storyboard.SetTarget(fadeOutBooster, DriverBoosterBottomCard);



            Storyboard.SetTargetProperty(fadeOutBooster, new PropertyPath("Opacity"));



            sb.Children.Add(fadeOutBooster);



        }







        // 2. Collapse Driver Scanner Card height: 582 -> 52 (smooth cubic ease matching rescan.mp4)



        if (DriverScannerCardGrid != null)



        {



            double currentH = DriverScannerCardGrid.ActualHeight > 0 ? DriverScannerCardGrid.ActualHeight : DriverScannerCardGrid.Height;



            if (double.IsNaN(currentH) || currentH <= 0) currentH = 582;







            var collapseHeight = new DoubleAnimation



            {



                From = currentH,



                To = 58.0,



                Duration = TimeSpan.FromSeconds(0.36),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }



            };



            Storyboard.SetTarget(collapseHeight, DriverScannerCardGrid);



            Storyboard.SetTargetProperty(collapseHeight, new PropertyPath("Height"));



            sb.Children.Add(collapseHeight);



        }







        // 3. Fade out expanded scanner host



        var fadeOutExpanded = new DoubleAnimation



        {



            BeginTime = TimeSpan.FromSeconds(0.14),



            To = 0.0,



            Duration = TimeSpan.FromSeconds(0.18),



            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



        };



        Storyboard.SetTarget(fadeOutExpanded, ExpandedScannerHost);



        Storyboard.SetTargetProperty(fadeOutExpanded, new PropertyPath("Opacity"));



        sb.Children.Add(fadeOutExpanded);







        // 4. Fade in collapsed bar as height reaches compact size



        var fadeInCollapsed = new DoubleAnimation



        {



            BeginTime = TimeSpan.FromSeconds(0.16),



            To = 1.0,



            Duration = TimeSpan.FromSeconds(0.22),



            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



        };



        Storyboard.SetTarget(fadeInCollapsed, DashboardCollapsedBar);



        Storyboard.SetTargetProperty(fadeInCollapsed, new PropertyPath("Opacity"));



        sb.Children.Add(fadeInCollapsed);







        // 5. Fade in Driver Installation content



        var fadeInDriverInst = new DoubleAnimation



        {



            BeginTime = TimeSpan.FromSeconds(0.20),



            To = 1.0,



            Duration = TimeSpan.FromSeconds(0.26),



            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



        };



        Storyboard.SetTarget(fadeInDriverInst, DriverInstallationHost);



        Storyboard.SetTargetProperty(fadeInDriverInst, new PropertyPath("Opacity"));



        sb.Children.Add(fadeInDriverInst);







        // 6. Slide up Driver Installation content: 24 -> 0



        if (DriverInstallationHostTranslate != null)



        {



            var slideUpDriverInst = new DoubleAnimation



            {



                BeginTime = TimeSpan.FromSeconds(0.20),



                From = 24.0,



                To = 0.0,



                Duration = TimeSpan.FromSeconds(0.28),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };



            Storyboard.SetTarget(slideUpDriverInst, DriverInstallationHostTranslate);



            Storyboard.SetTargetProperty(slideUpDriverInst, new PropertyPath("Y"));



            sb.Children.Add(slideUpDriverInst);



        }







        sb.Completed += (s, e) =>



        {



            ExpandedScannerHost.Visibility = Visibility.Collapsed;



            DashboardCollapsedBar.IsHitTestVisible = true;



            _activeTransitionStoryboard = null;



            StartInstallAllDriversBumpAnimation();



        };







        _activeTransitionStoryboard = sb;



        sb.Begin();



    }







    private void AnimateExpandToDashboard()



    {



        StopInstallAllDriversBumpAnimation();



        if (ExpandedScannerHost == null || CollapsedScannerHost == null ||



            DashboardCollapsedBar == null || DriverInstallationHost == null) return;







        _activeTransitionStoryboard?.Stop();







        if (_subscribedVm?.HasCompletedDriverScan == true)



        {



            ShowRestingFrame();



        }







        ExpandedScannerHost.Visibility = Visibility.Visible;



        ExpandedScannerHost.Opacity = 0;



        ExpandedScannerHost.IsHitTestVisible = false;







        DashboardCollapsedBar.IsHitTestVisible = false;







        var sb = new Storyboard();







        // 1. Fade out Driver Installation content



        var fadeOutDriverInst = new DoubleAnimation



        {



            To = 0.0,



            Duration = TimeSpan.FromSeconds(0.14),



            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



        };



        Storyboard.SetTarget(fadeOutDriverInst, DriverInstallationHost);



        Storyboard.SetTargetProperty(fadeOutDriverInst, new PropertyPath("Opacity"));



        sb.Children.Add(fadeOutDriverInst);







        // 2. Fade out collapsed bar



        var fadeOutCollapsed = new DoubleAnimation



        {



            To = 0.0,



            Duration = TimeSpan.FromSeconds(0.14),



            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



        };



        Storyboard.SetTarget(fadeOutCollapsed, DashboardCollapsedBar);



        Storyboard.SetTargetProperty(fadeOutCollapsed, new PropertyPath("Opacity"));



        sb.Children.Add(fadeOutCollapsed);







        // 3. Expand Driver Scanner Card height: 52 -> 582 (smooth cubic ease matching rescan.mp4)



        if (DriverScannerCardGrid != null)



        {



            double currentH = DriverScannerCardGrid.ActualHeight > 0 ? DriverScannerCardGrid.ActualHeight : 52;



            if (double.IsNaN(currentH) || currentH <= 0) currentH = 58;







            var expandHeight = new DoubleAnimation



            {



                From = currentH,



                To = 582.0,



                Duration = TimeSpan.FromSeconds(0.40),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }



            };



            Storyboard.SetTarget(expandHeight, DriverScannerCardGrid);



            Storyboard.SetTargetProperty(expandHeight, new PropertyPath("Height"));



            sb.Children.Add(expandHeight);



        }







        // 4. Fade in expanded scanner host



        var fadeInExpanded = new DoubleAnimation



        {



            BeginTime = TimeSpan.FromSeconds(0.16),



            To = 1.0,



            Duration = TimeSpan.FromSeconds(0.24),



            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



        };



        Storyboard.SetTarget(fadeInExpanded, ExpandedScannerHost);



        Storyboard.SetTargetProperty(fadeInExpanded, new PropertyPath("Opacity"));



        sb.Children.Add(fadeInExpanded);







        // 5. Fade in bottom booster card



        if (DriverBoosterBottomCard != null)



        {



            DriverBoosterBottomCard.Opacity = 0;



            var fadeInBooster = new DoubleAnimation



            {



                BeginTime = TimeSpan.FromSeconds(0.22),



                To = 1.0,



                Duration = TimeSpan.FromSeconds(0.20),



                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }



            };



            Storyboard.SetTarget(fadeInBooster, DriverBoosterBottomCard);



            Storyboard.SetTargetProperty(fadeInBooster, new PropertyPath("Opacity"));



            sb.Children.Add(fadeInBooster);



        }







        sb.Completed += (s, e) =>



        {



            ExpandedScannerHost.IsHitTestVisible = true;



            CollapsedScannerHost.Visibility = Visibility.Collapsed;



            if (DriverScannerCardGrid != null) DriverScannerCardGrid.Height = 582;



            if (DriverBoosterBottomCard != null) DriverBoosterBottomCard.Opacity = 1;



            if (_subscribedVm?.HasCompletedDriverScan == true)



            {



                ShowRestingFrame();



            }



            UpdateCardClip();



            _activeTransitionStoryboard = null;



        };







        _activeTransitionStoryboard = sb;



        sb.Begin();



    }







    #endregion







    #region Collapsed Rescan Button Animations



    private DispatcherTimer? _rescanWipeTimer;



        private void StartRescanScanningVisual()
    {
        try
        {
            _rescanWipeTimer?.Stop();
            if (CollapsedRescanBtn == null) return;
            CollapsedRescanBtn.ApplyTemplate();

            var limeOverlay = CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) as Border;
            var dotsLoader = CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) as FrameworkElement;
            var doneContent = CollapsedRescanBtn.Template?.FindName("RescanDoneContent", CollapsedRescanBtn) as FrameworkElement;

            if (doneContent != null) doneContent.Visibility = Visibility.Collapsed;
            if (dotsLoader != null) dotsLoader.Visibility = Visibility.Visible;

            if (limeOverlay != null)
            {
                limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);
                limeOverlay.HorizontalAlignment = HorizontalAlignment.Left;
                limeOverlay.CornerRadius = new CornerRadius(10);
                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = 134,
                    Duration = TimeSpan.FromMilliseconds(320),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, anim);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RescanVisual] Scan error: {ex.Message}");
        }
    }

    private void StartRescanCompletedVisual()
    {
        try
        {
            _rescanWipeTimer?.Stop();
            if (CollapsedRescanBtn == null) return;
            CollapsedRescanBtn.ApplyTemplate();

            var limeOverlay = CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) as Border;
            var dotsLoader = CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) as FrameworkElement;
            var doneContent = CollapsedRescanBtn.Template?.FindName("RescanDoneContent", CollapsedRescanBtn) as FrameworkElement;

            if (limeOverlay != null)
            {
                limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);
                limeOverlay.HorizontalAlignment = HorizontalAlignment.Left;
                limeOverlay.CornerRadius = new CornerRadius(10);
                limeOverlay.Width = 134;
            }

            if (dotsLoader != null) dotsLoader.Visibility = Visibility.Collapsed;
            if (doneContent != null)
            {
                doneContent.Visibility = Visibility.Visible;
                doneContent.Opacity = 1.0;
            }

            // Hold on Lime Done for 700ms, then run the smooth unmask wipe animation
            _rescanWipeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _rescanWipeTimer.Tick += (s, ev) =>
            {
                _rescanWipeTimer.Stop();
                StartRescanBlackWipe();
            };
            _rescanWipeTimer.Start();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RescanVisual] Done error: {ex.Message}");
        }
    }

    private void StartRescanBlackWipe()
    {
        try
        {
            if (CollapsedRescanBtn == null) return;
            CollapsedRescanBtn.ApplyTemplate();

            var limeOverlay = CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) as Border;
            var dotsLoader = CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) as FrameworkElement;
            var doneContent = CollapsedRescanBtn.Template?.FindName("RescanDoneContent", CollapsedRescanBtn) as FrameworkElement;

            if (doneContent != null) doneContent.Visibility = Visibility.Collapsed;
            if (dotsLoader != null) dotsLoader.Visibility = Visibility.Collapsed;

            if (limeOverlay != null)
            {
                limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);
                limeOverlay.HorizontalAlignment = HorizontalAlignment.Right;
                limeOverlay.CornerRadius = new CornerRadius(0, 10, 10, 0);
                limeOverlay.Width = 134;

                var anim = new DoubleAnimation
                {
                    From = 134,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(420),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                };

                anim.Completed += (s, ev) =>
                {
                    if (limeOverlay != null)
                    {
                        limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);
                        limeOverlay.Width = 0;
                        limeOverlay.HorizontalAlignment = HorizontalAlignment.Left;
                        limeOverlay.CornerRadius = new CornerRadius(10);
                    }
                    if (doneContent != null)
                    {
                        doneContent.Visibility = Visibility.Collapsed;
                        doneContent.Opacity = 1.0;
                    }
                    if (dotsLoader != null) dotsLoader.Visibility = Visibility.Visible;

                    // Smoothly collapse and remove the Rescan button
                    var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    var widthAnim = new DoubleAnimation(134, 0, TimeSpan.FromMilliseconds(350))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };
                    var marginAnim = new ThicknessAnimation(new Thickness(0, 0, 10, 0), new Thickness(0), TimeSpan.FromMilliseconds(350))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };

                    widthAnim.Completed += (s2, ev2) =>
                    {
                        CollapsedRescanBtn.Visibility = Visibility.Collapsed;
                        CollapsedRescanBtn.BeginAnimation(FrameworkElement.WidthProperty, null);
                        CollapsedRescanBtn.BeginAnimation(UIElement.OpacityProperty, null);
                        CollapsedRescanBtn.BeginAnimation(FrameworkElement.MarginProperty, null);
                        CollapsedRescanBtn.Width = 134;
                        CollapsedRescanBtn.Opacity = 1.0;
                        CollapsedRescanBtn.Margin = new Thickness(0, 0, 10, 0);
                        if (_subscribedVm != null)
                        {
                            _subscribedVm.IsRescanButtonVisible = false;
                        }
                    };

                    CollapsedRescanBtn.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
                    CollapsedRescanBtn.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);
                    CollapsedRescanBtn.BeginAnimation(FrameworkElement.MarginProperty, marginAnim);
                };

                limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, anim);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[RescanVisual] Wipe error: {ex.Message}");
        }
    }

    private void ResetRescanVisual()
    {
        try
        {
            _rescanWipeTimer?.Stop();
            if (CollapsedRescanBtn == null) return;
            CollapsedRescanBtn.ApplyTemplate();

            var limeOverlay = CollapsedRescanBtn.Template?.FindName("RescanLimeOverlay", CollapsedRescanBtn) as Border;
            var dotsLoader = CollapsedRescanBtn.Template?.FindName("RescanDotsLoader", CollapsedRescanBtn) as FrameworkElement;
            var doneContent = CollapsedRescanBtn.Template?.FindName("RescanDoneContent", CollapsedRescanBtn) as FrameworkElement;

            if (limeOverlay != null)
            {
                limeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);
                limeOverlay.Width = 0;
                limeOverlay.HorizontalAlignment = HorizontalAlignment.Left;
                limeOverlay.CornerRadius = new CornerRadius(10);
            }
            if (doneContent != null)
            {
                doneContent.Visibility = Visibility.Collapsed;
                doneContent.Opacity = 1.0;
            }
            if (dotsLoader != null) dotsLoader.Visibility = Visibility.Visible;
            CollapsedRescanBtn.IsEnabled = true;
        }
        catch { }
    }

    #endregion

    #region Header Stats Cascade Animation (Driver Booster Style)



    private void ShowHeaderStatsImmediate()
    {
        if (HeaderStatsPanel == null) return;
        var vm = DataContext as WindowsOptimizationViewModel;
        if (vm?.IsDriversSlideActive != true || !vm.HasCompletedDriverScan)
        {
            HideHeaderStatsImmediate();
            return;
        }

        HeaderStatsPanel.BeginAnimation(UIElement.OpacityProperty, null);
        HeaderStatsPanel.Visibility = Visibility.Visible;
        HeaderStatsPanel.Opacity = 1.0;

        ResetStatItemImmediate(HeaderStatItem1, Stat1Trans);
        ResetStatItemImmediate(HeaderStatItem2, Stat2Trans);
        ResetStatItemImmediate(HeaderStatItem3, Stat3Trans);
    }



    private void ResetStatItemImmediate(FrameworkElement? item, TranslateTransform? trans)

    {

        if (item == null) return;

        item.BeginAnimation(UIElement.OpacityProperty, null);

        item.Opacity = 1.0;

        if (trans != null)

        {

            trans.BeginAnimation(TranslateTransform.YProperty, null);

            trans.Y = 0;

        }

    }



    private void HideHeaderStatsImmediate()

    {

        if (HeaderStatsPanel == null) return;

        HeaderStatsPanel.BeginAnimation(UIElement.OpacityProperty, null);

        HeaderStatsPanel.Visibility = Visibility.Collapsed;

        HeaderStatsPanel.Opacity = 0;

    }



    private void AnimateHeaderStatsEntrance()
    {
        if (HeaderStatsPanel == null) return;
        var vm = DataContext as WindowsOptimizationViewModel;
        if (vm?.IsDriversSlideActive != true || !vm.HasCompletedDriverScan)
        {
            HideHeaderStatsImmediate();
            return;
        }

        HeaderStatsPanel.BeginAnimation(UIElement.OpacityProperty, null);
        HeaderStatsPanel.Visibility = Visibility.Visible;
        HeaderStatsPanel.Opacity = 0.0;



        // Smooth fade-in of the stats container

        var panelFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260))

        {

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

        };

        HeaderStatsPanel.BeginAnimation(UIElement.OpacityProperty, panelFade);



        // Staggered slide up & fade entrance matching Driver Booster

        AnimateStatItemEntrance(HeaderStatItem1, Stat1Trans, delayMs: 0);

        AnimateStatItemEntrance(HeaderStatItem2, Stat2Trans, delayMs: 80);

        AnimateStatItemEntrance(HeaderStatItem3, Stat3Trans, delayMs: 160);

    }



    private void AnimateStatItemEntrance(FrameworkElement? item, TranslateTransform? trans, int delayMs)

    {

        if (item == null) return;



        if (trans == null)

        {

            trans = new TranslateTransform(0, 18);

            item.RenderTransform = trans;

        }



        trans.BeginAnimation(TranslateTransform.YProperty, null);

        item.BeginAnimation(UIElement.OpacityProperty, null);



        trans.Y = 18;

        item.Opacity = 0;



        var slideUp = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(420))

        {

            BeginTime = TimeSpan.FromMilliseconds(delayMs),

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

        };



        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350))

        {

            BeginTime = TimeSpan.FromMilliseconds(delayMs),

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

        };



        trans.BeginAnimation(TranslateTransform.YProperty, slideUp);

        item.BeginAnimation(UIElement.OpacityProperty, fadeIn);

    }



    private void AnimateHeaderStatsExit()

    {

        if (HeaderStatsPanel == null) return;



        AnimateStatItemExit(HeaderStatItem1, Stat1Trans, delayMs: 0);

        AnimateStatItemExit(HeaderStatItem2, Stat2Trans, delayMs: 30);

        AnimateStatItemExit(HeaderStatItem3, Stat3Trans, delayMs: 60);



        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(240))

        {

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }

        };

        fade.Completed += (s, ev) =>

        {

            HeaderStatsPanel.Visibility = Visibility.Collapsed;

        };

        HeaderStatsPanel.BeginAnimation(UIElement.OpacityProperty, fade);

    }



    private void AnimateStatItemExit(FrameworkElement? item, TranslateTransform? trans, int delayMs)

    {

        if (item == null || trans == null) return;



        var slideDown = new DoubleAnimation(0, 16, TimeSpan.FromMilliseconds(220))

        {

            BeginTime = TimeSpan.FromMilliseconds(delayMs),

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }

        };



        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))

        {

            BeginTime = TimeSpan.FromMilliseconds(delayMs),

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }

        };



        trans.BeginAnimation(TranslateTransform.YProperty, slideDown);

        item.BeginAnimation(UIElement.OpacityProperty, fadeOut);

    }



    #endregion







    #region Download Button Visual Transitions & Bump Animation



    private DispatcherTimer? _downloadWipeTimer;



    private void StartDownloadCompletedVisual()

    {

        try

        {

            if (StandaloneDownloadBtn == null) return;

            StandaloneDownloadBtn.ApplyTemplate();



            var successLayer = StandaloneDownloadBtn.Template?.FindName("DownloadSuccessLayer", StandaloneDownloadBtn) as FrameworkElement;

            var liquidFill = StandaloneDownloadBtn.Template?.FindName("DownloadLiquidFill", StandaloneDownloadBtn) as FrameworkElement;

            var wipeOverlay = StandaloneDownloadBtn.Template?.FindName("DownloadBlackWipeOverlay", StandaloneDownloadBtn) as Border;



            if (liquidFill != null) liquidFill.Visibility = Visibility.Collapsed;



            if (successLayer != null)

            {

                successLayer.Visibility = Visibility.Visible;

            }



            if (wipeOverlay != null)

            {

                wipeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);

                wipeOverlay.Width = 0;

                wipeOverlay.Visibility = Visibility.Collapsed;

            }



            // Hold on Lime Green Success (#D1FE29 with dark checkmark) for 1000ms, then wipe transition back to download icon

            _downloadWipeTimer?.Stop();

            _downloadWipeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };

            _downloadWipeTimer.Tick += (s, ev) =>

            {

                _downloadWipeTimer?.Stop();

                StartDownloadBlackWipe();

            };

            _downloadWipeTimer.Start();

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[DownloadVisual] Completed error: {ex.Message}");

        }

    }



    private void StartDownloadBlackWipe()

    {

        try

        {

            if (StandaloneDownloadBtn == null) return;

            StandaloneDownloadBtn.ApplyTemplate();



            var successLayer = StandaloneDownloadBtn.Template?.FindName("DownloadSuccessLayer", StandaloneDownloadBtn) as FrameworkElement;

            var wipeOverlay = StandaloneDownloadBtn.Template?.FindName("DownloadBlackWipeOverlay", StandaloneDownloadBtn) as Border;



            if (wipeOverlay != null)

            {

                double targetWidth = StandaloneDownloadBtn.ActualWidth > 0 ? StandaloneDownloadBtn.ActualWidth : 36;

                wipeOverlay.Width = 0;

                wipeOverlay.Visibility = Visibility.Visible;



                var wipeAnim = new DoubleAnimation

                {

                    From = 0,

                    To = targetWidth,

                    Duration = TimeSpan.FromMilliseconds(450),

                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }

                };



                wipeAnim.Completed += (s, ev) =>

                {

                    if (wipeOverlay != null)

                    {

                        wipeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);

                        wipeOverlay.Width = 0;

                        wipeOverlay.Visibility = Visibility.Collapsed;

                    }

                    if (successLayer != null)

                    {

                        successLayer.Visibility = Visibility.Collapsed;

                    }

                    // Transition back to download icon complete! Now start continuous bump animation.

                    StartDownloadBumpAnimation();

                };



                wipeOverlay.BeginAnimation(FrameworkElement.WidthProperty, wipeAnim);

            }

            else

            {

                if (successLayer != null) successLayer.Visibility = Visibility.Collapsed;

                StartDownloadBumpAnimation();

            }

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[DownloadVisual] Wipe error: {ex.Message}");

        }

    }



    private void StartDownloadBumpAnimation()

    {

        try

        {

            StopDownloadBumpAnimation();



            if (StandaloneDownloadBtn == null) return;

            StandaloneDownloadBtn.ApplyTemplate();



            var arrowIcon = StandaloneDownloadBtn.Template?.FindName("DownloadArrowIcon", StandaloneDownloadBtn) as TextBlock;

            if (arrowIcon == null) return;



            var trans = arrowIcon.RenderTransform as TranslateTransform;

            if (trans == null)

            {

                trans = new TranslateTransform();

                arrowIcon.RenderTransform = trans;

            }



            var yAnim = new DoubleAnimationUsingKeyFrames

            {

                Duration = TimeSpan.FromMilliseconds(1600),

                RepeatBehavior = RepeatBehavior.Forever

            };



            // Tactile bump animation keyframes:

            // 0ms: at base (0px)

            yAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

            // 240ms: snappy upward bump (-4.5px)

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-4.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

            // 480ms: return to base (0px)

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480)), new KeySpline(0.4, 0.0, 0.6, 1.0)));

            // 680ms: soft secondary hop (-2.0px)

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-2.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(680)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

            // 880ms: settle back to base (0px)

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(880)), new KeySpline(0.4, 0.0, 0.6, 1.0)));

            // 880ms - 1600ms: gentle pause before repeating

            yAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));



            trans.BeginAnimation(TranslateTransform.YProperty, yAnim);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[DownloadVisual] Bump error: {ex.Message}");

        }

    }



    private void StopDownloadBumpAnimation()

    {

        try

        {

            _downloadWipeTimer?.Stop();



            if (StandaloneDownloadBtn != null)

            {

                StandaloneDownloadBtn.ApplyTemplate();

                var arrowIcon = StandaloneDownloadBtn.Template?.FindName("DownloadArrowIcon", StandaloneDownloadBtn) as TextBlock;

                if (arrowIcon?.RenderTransform is TranslateTransform trans)

                {

                    trans.BeginAnimation(TranslateTransform.YProperty, null);

                    trans.Y = 0;

                }



                var successLayer = StandaloneDownloadBtn.Template?.FindName("DownloadSuccessLayer", StandaloneDownloadBtn) as FrameworkElement;

                if (successLayer != null) successLayer.Visibility = Visibility.Collapsed;



                var wipeOverlay = StandaloneDownloadBtn.Template?.FindName("DownloadBlackWipeOverlay", StandaloneDownloadBtn) as Border;

                if (wipeOverlay != null)

                {

                    wipeOverlay.BeginAnimation(FrameworkElement.WidthProperty, null);

                    wipeOverlay.Width = 0;

                    wipeOverlay.Visibility = Visibility.Collapsed;

                }

            }

        }

        catch { }

    }



    #endregion





    #region Install All Drivers Bump Animation



    private void StartInstallAllDriversBumpAnimation()

    {

        try

        {

            StopInstallAllDriversBumpAnimation();



            if (HeaderInstallAllDriversBtn == null) return;

            HeaderInstallAllDriversBtn.ApplyTemplate();



            var textBlock = HeaderInstallAllDriversBtn.Template?.FindName("InstallBtnText", HeaderInstallAllDriversBtn) as TextBlock;

            if (textBlock == null)

            {

                RoutedEventHandler? onLoaded = null;

                onLoaded = (s, e) =>

                {

                    HeaderInstallAllDriversBtn.Loaded -= onLoaded;

                    StartInstallAllDriversBumpAnimation();

                };

                HeaderInstallAllDriversBtn.Loaded += onLoaded;

                return;

            }



            var trans = textBlock.RenderTransform as TranslateTransform;

            if (trans == null)

            {

                trans = new TranslateTransform();

                textBlock.RenderTransform = trans;

            }



            var yAnim = new DoubleAnimationUsingKeyFrames

            {

                Duration = TimeSpan.FromMilliseconds(1600),

                RepeatBehavior = RepeatBehavior.Forever

            };



            // Tactile bump animation keyframes:

            yAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-3.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(240)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480)), new KeySpline(0.4, 0.0, 0.6, 1.0)));

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-1.8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(680)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

            yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(880)), new KeySpline(0.4, 0.0, 0.6, 1.0)));

            yAnim.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1600))));



            trans.BeginAnimation(TranslateTransform.YProperty, yAnim);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[InstallAllDrivers] Bump error: {ex.Message}");

        }

    }



    private void StopInstallAllDriversBumpAnimation()

    {

        try

        {

            if (HeaderInstallAllDriversBtn != null)

            {

                HeaderInstallAllDriversBtn.ApplyTemplate();

                var textBlock = HeaderInstallAllDriversBtn.Template?.FindName("InstallBtnText", HeaderInstallAllDriversBtn) as TextBlock;

                if (textBlock?.RenderTransform is TranslateTransform trans)

                {

                    trans.BeginAnimation(TranslateTransform.YProperty, null);

                    trans.Y = 0;

                }

            }

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[InstallAllDrivers] Stop bump error: {ex.Message}");

        }

    }



    #endregion



    #region Driver Booster Banner Bump Animation



    private void StartDriverBoosterBumpAnimation()

    {

        try

        {

            StopDriverBoosterBumpAnimation();



            if (DriverBoosterBannerBtn == null) return;

            DriverBoosterBannerBtn.ApplyTemplate();



            var border = DriverBoosterBannerBtn.Template?.FindName("BoosterBtnBorder", DriverBoosterBannerBtn) as Border;

            if (border == null) return;



            var scale = border.RenderTransform as ScaleTransform;

            if (scale == null)

            {

                scale = new ScaleTransform(1.0, 1.0);

                border.RenderTransformOrigin = new Point(0.5, 0.5);

                border.RenderTransform = scale;

            }



            var anim = new DoubleAnimationUsingKeyFrames

            {

                Duration = TimeSpan.FromMilliseconds(1800),

                RepeatBehavior = RepeatBehavior.Forever

            };

            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

            anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.06, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

            anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500)), new KeySpline(0.6, 0.0, 0.8, 0.2)));

            anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.03, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)), new KeySpline(0.2, 0.8, 0.4, 1.0)));

            anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(900)), new KeySpline(0.6, 0.0, 0.8, 0.2)));

            anim.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1800))));



            scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);

            scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[DriverBoosterBanner] Bump error: {ex.Message}");

        }

    }



    private void StopDriverBoosterBumpAnimation()

    {

        try

        {

            if (DriverBoosterBannerBtn != null)

            {

                DriverBoosterBannerBtn.ApplyTemplate();

                var border = DriverBoosterBannerBtn.Template?.FindName("BoosterBtnBorder", DriverBoosterBannerBtn) as Border;

                if (border?.RenderTransform is ScaleTransform scale)

                {

                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);

                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

                    scale.ScaleX = 1.0;

                    scale.ScaleY = 1.0;

                }

            }

        }

        catch { }

    }



    #endregion







    #region Download Button Morph & Guide Button Transition



    private void ApplyHeaderButtonsVisualState(bool isScanned, bool animate)
    {
        try
        {
            var vm = DataContext as WindowsOptimizationViewModel;
            if (vm?.IsDriversSlideActive != true)
            {
                HideHeaderStatsImmediate();
                return;
            }

            if (StandaloneDownloadBtn == null) return;

            StandaloneDownloadBtn.ApplyTemplate();



            var border = StandaloneDownloadBtn.Template?.FindName("DownloadBtnBorder", StandaloneDownloadBtn) as Border;

            var textClip = StandaloneDownloadBtn.Template?.FindName("DownloadTextClip", StandaloneDownloadBtn) as FrameworkElement;



            if (border == null) return;



            if (isScanned)

            {

                if (animate)

                {

                    // Ensure HeaderStatsPanel stays hidden while download button is shrinking

                    if (HeaderStatsPanel != null)

                    {

                        HeaderStatsPanel.BeginAnimation(UIElement.OpacityProperty, null);

                        HeaderStatsPanel.Visibility = Visibility.Collapsed;

                        HeaderStatsPanel.Opacity = 0;

                    }



                    // Only AFTER download button becomes small, show stats with animation!

                    AnimateDownloadButtonShrink(onCompleted: () =>

                    {

                        AnimateHeaderStatsEntrance();

                    });

                    AnimateGuideButtonEntrance();

                }

                else

                {

                    border.BeginAnimation(FrameworkElement.WidthProperty, null);

                    border.BeginAnimation(Border.PaddingProperty, null);

                    border.Width = 36;

                    border.Padding = new Thickness(0);



                    if (textClip != null)

                    {

                        textClip.BeginAnimation(FrameworkElement.WidthProperty, null);

                        textClip.BeginAnimation(UIElement.OpacityProperty, null);

                        textClip.Width = 0;

                        textClip.Opacity = 0;

                        textClip.Visibility = Visibility.Collapsed;

                    }



                    ShowHeaderStatsImmediate();

                }

            }

            else

            {

                if (animate)

                {

                    AnimateHeaderStatsExit();

                    AnimateDownloadButtonExpand();

                }

                else

                {

                    border.BeginAnimation(FrameworkElement.WidthProperty, null);

                    border.BeginAnimation(Border.PaddingProperty, null);

                    border.Width = 98;

                    border.Padding = new Thickness(12, 0, 14, 0);



                    if (textClip != null)

                    {

                        textClip.BeginAnimation(FrameworkElement.WidthProperty, null);

                        textClip.BeginAnimation(UIElement.OpacityProperty, null);

                        textClip.Width = double.NaN;

                        textClip.Opacity = 1.0;

                        textClip.Visibility = Visibility.Visible;

                    }



                    HideHeaderStatsImmediate();

                }

            }

        }

        catch { }

    }



    private void AnimateDownloadButtonShrink(Action? onCompleted = null)

    {

        try

        {

            if (StandaloneDownloadBtn == null)

            {

                onCompleted?.Invoke();

                return;

            }

            StandaloneDownloadBtn.ApplyTemplate();



            var border = StandaloneDownloadBtn.Template?.FindName("DownloadBtnBorder", StandaloneDownloadBtn) as Border;

            var textClip = StandaloneDownloadBtn.Template?.FindName("DownloadTextClip", StandaloneDownloadBtn) as FrameworkElement;



            if (border == null)

            {

                onCompleted?.Invoke();

                return;

            }



            double fromWidth = border.ActualWidth > 45 ? border.ActualWidth : 98;

            double toWidth = 36;



            var cubicEase = new CubicEase { EasingMode = EasingMode.EaseInOut };



            // 1. Smoothly shrink border width from ~98px to 36px

            var widthAnim = new DoubleAnimation

            {

                From = fromWidth,

                To = toWidth,

                Duration = TimeSpan.FromMilliseconds(420),

                EasingFunction = cubicEase

            };



            // 2. Smoothly fade out and collapse the "Download" text

            if (textClip != null)

            {

                textClip.Visibility = Visibility.Visible;

                double textFromWidth = textClip.ActualWidth > 0 ? textClip.ActualWidth : 58;

                var textWidthAnim = new DoubleAnimation

                {

                    From = textFromWidth,

                    To = 0,

                    Duration = TimeSpan.FromMilliseconds(380),

                    EasingFunction = cubicEase

                };



                var textFadeAnim = new DoubleAnimation

                {

                    From = textClip.Opacity > 0 ? textClip.Opacity : 1.0,

                    To = 0.0,

                    Duration = TimeSpan.FromMilliseconds(220),

                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }

                };



                textFadeAnim.Completed += (s, ev) =>

                {

                    textClip.Visibility = Visibility.Collapsed;

                };



                textClip.BeginAnimation(FrameworkElement.WidthProperty, textWidthAnim);

                textClip.BeginAnimation(UIElement.OpacityProperty, textFadeAnim);

            }



            // 3. Smoothly animate padding to 0

            var padAnim = new ThicknessAnimation

            {

                From = border.Padding,

                To = new Thickness(0),

                Duration = TimeSpan.FromMilliseconds(380),

                EasingFunction = cubicEase

            };



            widthAnim.Completed += (s, ev) =>

            {

                border.BeginAnimation(FrameworkElement.WidthProperty, null);

                border.Width = 36;

                border.BeginAnimation(Border.PaddingProperty, null);

                border.Padding = new Thickness(0);



                // Download button is now small; trigger entrance animation for verified count section

                onCompleted?.Invoke();

            };



            border.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);

            border.BeginAnimation(Border.PaddingProperty, padAnim);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[DownloadShrink] Error: {ex.Message}");

            onCompleted?.Invoke();

        }

    }



    private void AnimateDownloadButtonExpand()

    {

        try

        {

            if (StandaloneDownloadBtn == null) return;

            StandaloneDownloadBtn.ApplyTemplate();



            var border = StandaloneDownloadBtn.Template?.FindName("DownloadBtnBorder", StandaloneDownloadBtn) as Border;

            var textClip = StandaloneDownloadBtn.Template?.FindName("DownloadTextClip", StandaloneDownloadBtn) as FrameworkElement;



            if (border == null) return;



            double fromWidth = border.ActualWidth < 90 ? border.ActualWidth : 36;

            double toWidth = 98;



            var cubicEase = new CubicEase { EasingMode = EasingMode.EaseInOut };



            var widthAnim = new DoubleAnimation

            {

                From = fromWidth,

                To = toWidth,

                Duration = TimeSpan.FromMilliseconds(400),

                EasingFunction = cubicEase

            };



            if (textClip != null)

            {

                textClip.Visibility = Visibility.Visible;

                var textWidthAnim = new DoubleAnimation

                {

                    From = 0,

                    To = 58,

                    Duration = TimeSpan.FromMilliseconds(380),

                    EasingFunction = cubicEase

                };



                var textFadeAnim = new DoubleAnimation

                {

                    From = 0.0,

                    To = 1.0,

                    Duration = TimeSpan.FromMilliseconds(350),

                    BeginTime = TimeSpan.FromMilliseconds(80),

                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }

                };



                textClip.BeginAnimation(FrameworkElement.WidthProperty, textWidthAnim);

                textClip.BeginAnimation(UIElement.OpacityProperty, textFadeAnim);

            }



            var padAnim = new ThicknessAnimation

            {

                From = border.Padding,

                To = new Thickness(12, 0, 14, 0),

                Duration = TimeSpan.FromMilliseconds(380),

                EasingFunction = cubicEase

            };



            widthAnim.Completed += (s, ev) =>

            {

                border.BeginAnimation(FrameworkElement.WidthProperty, null);

                border.Width = 98;

                border.BeginAnimation(Border.PaddingProperty, null);

                border.Padding = new Thickness(12, 0, 14, 0);

            };



            border.BeginAnimation(FrameworkElement.WidthProperty, widthAnim);

            border.BeginAnimation(Border.PaddingProperty, padAnim);

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[DownloadExpand] Error: {ex.Message}");

        }

    }



    private void AnimateGuideButtonEntrance()

    {

        try

        {

            if (HeaderGuideBtn == null) return;

            if (GuideBtnTrans == null)

            {

                GuideBtnTrans = new TranslateTransform(0, 0);

                HeaderGuideBtn.RenderTransform = GuideBtnTrans;

            }



            GuideBtnTrans.BeginAnimation(TranslateTransform.XProperty, null);

            GuideBtnTrans.BeginAnimation(TranslateTransform.YProperty, null);

            HeaderGuideBtn.BeginAnimation(UIElement.OpacityProperty, null);



            GuideBtnTrans.X = 0;

            GuideBtnTrans.Y = 0;

            HeaderGuideBtn.Opacity = 1.0;

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[GuideEntrance] Error: {ex.Message}");

        }

    }



    #region Guide Modal Delegation
    // Full-window Guide Modal is hosted in MainWindow as InstructionGuideModalControl
    // covering the entire application window (TitleBar, Sidebar, and Page) with full dark dimming and blur.
    private void PlayGuideOpenAnimation() { }
    private void PlayGuideCloseAnimation() { }
    #endregion

    #endregion
}
