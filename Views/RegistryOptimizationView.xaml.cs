using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class RegistryOptimizationView : UserControl
{
    private DateTime _lastSlideWheelTime = DateTime.MinValue;

    public RegistryOptimizationView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is RegistryOptimizationViewModel oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;
        }
        if (e.NewValue is RegistryOptimizationViewModel newVm)
        {
            newVm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RegistryOptimizationViewModel.SelectedSlideIndex))
        {
            MainContentScrollViewer?.ScrollToTop();
        }
    }

    /// <summary>
    /// Handles mouse wheel scrolling directly over the bottom pagination dots bar.
    /// Matches WindowsOptimizationView's Advance tab exact interaction law.
    /// </summary>
    private void PaginationBar_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is RegistryOptimizationViewModel vm)
        {
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
}
