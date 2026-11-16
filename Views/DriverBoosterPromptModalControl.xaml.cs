using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class DriverBoosterPromptModalControl : UserControl
{
    public DriverBoosterPromptModalControl()
    {
        InitializeComponent();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            PlayEntranceAnimation();
        }
    }

    public void PlayEntranceAnimation()
    {
        try
        {
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(UIElement.OpacityProperty, fadeIn);

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
            System.Diagnostics.Debug.WriteLine($"[DriverBoosterPrompt] Entrance error: {ex.Message}");
        }
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.DismissDriverBoosterPromptCommand.Execute(null);
        }
    }
}
