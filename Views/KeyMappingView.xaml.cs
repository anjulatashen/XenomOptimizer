using System.Windows.Controls;
using System.Windows.Input;
using XenomOptimizer.ViewModels;

namespace XenomOptimizer.Views;

public partial class KeyMappingView : UserControl
{
    public KeyMappingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var window = System.Windows.Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown += OnWindowPreviewKeyDown;
        }
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        var window = System.Windows.Window.GetWindow(this);
        if (window != null)
        {
            window.PreviewKeyDown -= OnWindowPreviewKeyDown;
        }
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is KeyMappingViewModel vm && vm.ListeningKeyItem != null)
        {
            if (e.Key == Key.Escape)
            {
                vm.CancelRebindKeyCommand.Execute(null);
                e.Handled = true;
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key != Key.None)
            {
                vm.ApplyRebind(key);
                e.Handled = true;
            }
        }
    }
}
