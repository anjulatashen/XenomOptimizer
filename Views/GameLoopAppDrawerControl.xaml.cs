using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace XenomOptimizer.Views;

public partial class GameLoopAppDrawerControl : UserControl
{
    public GameLoopAppDrawerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitVideoSource();
    }

    private void InitVideoSource()
    {
        try
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string videoPath = Path.Combine(appDir, "Assets", "Gradiant.mp4");
            if (!File.Exists(videoPath))
            {
                videoPath = @"C:\Users\anjul\Downloads\Gradiant.mp4";
            }
            if (File.Exists(videoPath))
            {
                BackgroundVideo.Source = new Uri(videoPath, UriKind.Absolute);
                if (IsVisible)
                {
                    BackgroundVideo.SpeedRatio = 0.45;
                    BackgroundVideo.Position = TimeSpan.Zero;
                    BackgroundVideo.Play();
                }
            }
        }
        catch { }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            try
            {
                if (BackgroundVideo.Source == null)
                {
                    InitVideoSource();
                }
                BackgroundVideo.SpeedRatio = 0.45; // Slow motion as requested
                BackgroundVideo.Position = TimeSpan.Zero;
                BackgroundVideo.Play();
            }
            catch { }
        }
        else
        {
            try
            {
                // Pause/stop playback when hidden to save 100% CPU and RAM
                BackgroundVideo.Stop();
            }
            catch { }
        }
    }

    private void BackgroundVideo_MediaEnded(object sender, RoutedEventArgs e)
    {
        try
        {
            BackgroundVideo.Position = TimeSpan.Zero;
            BackgroundVideo.Play();
        }
        catch { }
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.CloseAppDrawerCommand.Execute(null);
        }
    }

    private void DrawerPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void PullHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.MainViewModel vm)
        {
            vm.CloseAppDrawerCommand.Execute(null);
        }
        e.Handled = true;
    }
}
