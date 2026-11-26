using System.Windows.Controls;
using System.Windows.Input;

namespace XenomOptimizer.Views;

public partial class GameLoopAppDrawerControl : UserControl
{
    public GameLoopAppDrawerControl()
    {
        InitializeComponent();
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
}
