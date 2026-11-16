using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class DriverWarningModalControl : UserControl
{
    public DriverWarningModalControl()
    {
        InitializeComponent();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            PlayEntranceAnimation();
            if (DataContext is WindowsOptimizationViewModel vm && vm.AreAllDriversInstalled)
            {
                StartUnderstandBumpAnimation();
            }
            else
            {
                StopUnderstandBumpAnimation();
            }
        }
        else
        {
            StopUnderstandBumpAnimation();
        }
    }

    public void PlayEntranceAnimation()
    {
        try
        {
            // 1. Fade in backdrop & card
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // 2. Slide card up from bottom (+50 -> 0)
            if (ModalCardTranslate != null)
            {
                ModalCardTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, null);
                ModalCardTranslate.Y = 50.0;

                var slideUp = new DoubleAnimation(50.0, 0.0, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                ModalCardTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slideUp);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DriverWarningModal] Entrance error: {ex.Message}");
        }
    }

    public void StartUnderstandBumpAnimation()
    {
        try
        {
            StopUnderstandBumpAnimation();

            if (UnderstandButton == null) return;
            UnderstandButton.ApplyTemplate();

            var border = UnderstandButton.Template?.FindName("UnderstandBox", UnderstandButton) as Border;
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
            System.Diagnostics.Debug.WriteLine($"[DriverWarningModal] Understand bump error: {ex.Message}");
        }
    }

    public void StopUnderstandBumpAnimation()
    {
        try
        {
            if (UnderstandButton != null)
            {
                UnderstandButton.ApplyTemplate();
                var border = UnderstandButton.Template?.FindName("UnderstandBox", UnderstandButton) as Border;
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

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.CancelDriverWarningCommand.Execute(null);
        }
    }
}

