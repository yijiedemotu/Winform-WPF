using System.Windows;
using WpfDemo.ViewModels;

namespace WpfDemo.Views;

/// <summary>
/// View 的 code-behind 只做两件事：设置 DataContext、把 View 生命周期转达给 VM。
/// 所有状态与命令都在 ViewModel 里，这里没有任何业务逻辑。
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Closed += (_, _) => _viewModel.Unsubscribe();
    }
}
