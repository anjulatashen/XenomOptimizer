using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class InstructionGuideModalControl : UserControl
{
    private bool _isOpenAnimation = true;
    private WindowsOptimizationViewModel? _boundVm;

    public InstructionGuideModalControl()
    {
        InitializeComponent();
        DataContextChanged += (s, e) =>
        {
            WireViewModelEvents();
            UpdateNotchAndProgress(animate: false);
        };
    }

    public static void EnsureGuideFramesPreloaded()
    {
    }

    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        WireViewModelEvents();
        UpdateNotchAndProgress(animate: false);
    }

    private void OnUserControlSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateNotchAndProgress(animate: false);
    }

    private void WireViewModelEvents()
    {
        if (_boundVm != null)
        {
            _boundVm.PropertyChanged -= OnViewModelPropertyChanged;
            _boundVm = null;
        }

        if (DataContext is WindowsOptimizationViewModel vm)
        {
            _boundVm = vm;
            _boundVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WindowsOptimizationViewModel.ActiveGuideStep) ||
            e.PropertyName == nameof(WindowsOptimizationViewModel.GuideCompletedCount) ||
            e.PropertyName == nameof(WindowsOptimizationViewModel.IsAllGuideStepsDone))
        {
            Dispatcher.InvokeAsync(() => UpdateNotchAndProgress(animate: true));
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            PlayGuideOpenAnimation();
            UpdateNotchAndProgress(animate: false);
        }
    }

    public void UpdateNotchAndProgress(bool animate = true)
    {
        try
        {
            if (DataContext is not WindowsOptimizationViewModel vm) return;

            double trackWidth = StepperTrackBorder?.ActualWidth ?? 0;
            if (trackWidth <= 10)
            {
                // Fallback before initial measure (modal width 660 - padding 64 = ~596)
                trackWidth = 596.0;
            }

            double colWidth = trackWidth / 6.0;
            int activeStep = Math.Clamp(vm.ActiveGuideStep, 1, 6);
            int completedCount = Math.Clamp(vm.GuideCompletedCount, 0, 6);

            // 1. Position Downward Notch to point at active step node center
            double targetCenter = (activeStep - 1) * colWidth + (colWidth / 2.0);
            double targetNotchX = targetCenter - 8.0; // 8px = half of notch width (16px)

            if (BubbleNotchTranslate != null)
            {
                BubbleNotchTranslate.BeginAnimation(TranslateTransform.XProperty, null);
                if (animate)
                {
                    var notchAnim = new DoubleAnimation(BubbleNotchTranslate.X, targetNotchX, TimeSpan.FromMilliseconds(240))
                    {
                        EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                    };
                    BubbleNotchTranslate.BeginAnimation(TranslateTransform.XProperty, notchAnim);
                }
                else
                {
                    BubbleNotchTranslate.X = targetNotchX;
                }
            }

            // 2. Position and size Continuous Progress Fill Bar
            double targetFillWidth = 0.0;
            if (completedCount == 6)
            {
                targetFillWidth = trackWidth;
            }
            else if (completedCount > 0)
            {
                targetFillWidth = (completedCount - 1) * colWidth + (colWidth / 2.0) + 18.0;
                targetFillWidth = Math.Min(targetFillWidth, trackWidth);
            }
            else
            {
                targetFillWidth = 0.0;
            }

            if (ProgressFillBar != null)
            {
                ProgressFillBar.BeginAnimation(FrameworkElement.WidthProperty, null);
                if (animate)
                {
                    double currentWidth = double.IsNaN(ProgressFillBar.Width) ? 0.0 : ProgressFillBar.Width;
                    var fillAnim = new DoubleAnimation(currentWidth, targetFillWidth, TimeSpan.FromMilliseconds(280))
                    {
                        EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                    };
                    ProgressFillBar.BeginAnimation(FrameworkElement.WidthProperty, fillAnim);
                }
                else
                {
                    ProgressFillBar.Width = targetFillWidth;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideModal] UpdateNotchAndProgress error: {ex.Message}");
        }
    }

    public void PlayGuideOpenAnimation()
    {
        try
        {
            _isOpenAnimation = true;

            // 1. Softly fade in deep dark translucent backdrop
            if (BackdropBorder != null)
            {
                BackdropBorder.BeginAnimation(UIElement.OpacityProperty, null);
                BackdropBorder.Opacity = 0.0;
                var backdropAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(240))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                BackdropBorder.BeginAnimation(UIElement.OpacityProperty, backdropAnim);
            }

            // 2. Fade in card & slide up from Y=30 to Y=0
            if (InstructionModalCard != null)
            {
                InstructionModalCard.BeginAnimation(UIElement.OpacityProperty, null);
                InstructionModalCard.Opacity = 0.0;
                var cardFade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                InstructionModalCard.BeginAnimation(UIElement.OpacityProperty, cardFade);
            }

            if (ModalCardTranslate != null)
            {
                ModalCardTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                ModalCardTranslate.Y = 30.0;
                var cardSlide = new DoubleAnimation(30.0, 0.0, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                };
                ModalCardTranslate.BeginAnimation(TranslateTransform.YProperty, cardSlide);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideModal] Open error: {ex.Message}");
        }
    }

    public void CloseWithAnimation()
    {
        CloseWithAction(() =>
        {
            if (DataContext is WindowsOptimizationViewModel vm)
            {
                vm.CloseInstructionModalCommand.Execute(null);
            }
        });
    }

    public void CloseWithAction(Action onClosed)
    {
        try
        {
            if (!_isOpenAnimation)
            {
                onClosed?.Invoke();
                return;
            }
            _isOpenAnimation = false;

            // Fade out backdrop
            if (BackdropBorder != null)
            {
                BackdropBorder.BeginAnimation(UIElement.OpacityProperty, null);
                var backdropAnim = new DoubleAnimation(BackdropBorder.Opacity, 0.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                BackdropBorder.BeginAnimation(UIElement.OpacityProperty, backdropAnim);
            }

            // Fade out card & slide down Y=0 -> Y=20
            if (InstructionModalCard != null)
            {
                InstructionModalCard.BeginAnimation(UIElement.OpacityProperty, null);
                var cardFade = new DoubleAnimation(InstructionModalCard.Opacity, 0.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                InstructionModalCard.BeginAnimation(UIElement.OpacityProperty, cardFade);
            }

            if (ModalCardTranslate != null)
            {
                ModalCardTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                var cardSlide = new DoubleAnimation(ModalCardTranslate.Y, 20.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                cardSlide.Completed += (s, e) =>
                {
                    onClosed?.Invoke();
                };
                ModalCardTranslate.BeginAnimation(TranslateTransform.YProperty, cardSlide);
            }
            else
            {
                onClosed?.Invoke();
            }
        }
        catch
        {
            onClosed?.Invoke();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseWithAnimation();
    }

    private void Backdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        CloseWithAnimation();
    }

    private void BubbleActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            int targetStep = vm.ActiveGuideStep;
            CloseWithAction(() =>
            {
                vm.NavigateToGuideStep(targetStep);
            });
        }
        else
        {
            CloseWithAnimation();
        }
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            int targetStep = vm.ActiveGuideStep;
            CloseWithAction(() =>
            {
                vm.NavigateToGuideStep(targetStep);
            });
        }
        else
        {
            CloseWithAnimation();
        }
    }

    private void Step1_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.SelectGuideStep(1);
        }
    }

    private void Step2_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.SelectGuideStep(2);
        }
    }

    private void Step3_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.SelectGuideStep(3);
        }
    }

    private void Step4_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.SelectGuideStep(4);
        }
    }

    private void Step5_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.SelectGuideStep(5);
        }
    }

    private void Step6_Click(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is WindowsOptimizationViewModel vm)
        {
            vm.SelectGuideStep(6);
        }
    }
}
