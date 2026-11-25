using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace XenomOptimizer.Views;

/// <summary>
/// Pure transparent floating animated logo splash screen.
/// Zero card, zero background box - only the white animated logo floating in center.
/// </summary>
public partial class SplashScreenWindow : Window
{
    private readonly Window _mainWindow;
    private readonly List<BitmapSource> _frames = new();
    private DispatcherTimer? _animTimer;
    private int _frameIndex = 0;
    private bool _isCompleted = false;

    public SplashScreenWindow(Window mainWindow)
    {
        InitializeComponent();
        _mainWindow = mainWindow;
        XenomOptimizer.Services.WindowPlacementService.PositionSplashScreen(this);
        Loaded += SplashScreenWindow_Loaded;
    }

    private void SplashScreenWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 1. Smooth Fade-in & Scale-up
        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        SplashRootGrid.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        var scaleIn = new DoubleAnimation
        {
            From = 0.92,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
        };
        SplashScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scaleIn);
        SplashScaleTransform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scaleIn);

        // 2. Load Transparent Animation Frames
        LoadAnimationFrames();

        // 3. Play Logo Animation; immediately when it completes, launch MainWindow without delay or fade
        StartAnimationLoop();
    }

    private void LoadAnimationFrames()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string framesDir = Path.Combine(baseDir, "Assets", "LogoFrames");
            if (!Directory.Exists(framesDir))
            {
                framesDir = @"d:\Shop\Xenom Lag Fix\Assets\LogoFrames";
            }

            if (Directory.Exists(framesDir))
            {
                var files = Directory.GetFiles(framesDir, "frame_*.png");
                Array.Sort(files);

                foreach (var file in files)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(file, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    _frames.Add(bmp);
                }
            }

            if (_frames.Count > 0)
            {
                LogoImage.Source = _frames[0];
            }
        }
        catch { }
    }

    private void StartAnimationLoop()
    {
        if (_frames.Count == 0)
        {
            FinishAndOpenMainWindow();
            return;
        }

        _animTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(40) // 25 FPS
        };
        _animTimer.Tick += (s, ev) =>
        {
            if (_frameIndex < _frames.Count)
            {
                LogoImage.Source = _frames[_frameIndex];
                _frameIndex++;
            }
            else
            {
                // Animation reached the end! Instantly launch MainWindow with zero lag/fade
                FinishAndOpenMainWindow();
            }
        };
        _animTimer.Start();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Click to skip immediately
        FinishAndOpenMainWindow();
    }

    private void FinishAndOpenMainWindow()
    {
        if (_isCompleted) return;
        _isCompleted = true;

        _animTimer?.Stop();

        // Instantly launch MainWindow and close splash screen
        Application.Current.MainWindow = _mainWindow;
        _mainWindow.Show();
        Close();
    }
}
