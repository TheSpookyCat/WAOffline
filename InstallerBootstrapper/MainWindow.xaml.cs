using System.Collections.Specialized;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Input;
using InstallerBootstrapper.Infrastructure.State;

namespace InstallerBootstrapper;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly InstallerViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new InstallerViewModel(new InstallStateStore());
        DataContext = _viewModel;

        _viewModel.StatusLog.CollectionChanged += OnStatusLogCollectionChanged;
    }

    private void DragRegion_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Minimize_OnClick(object sender, RoutedEventArgs e)
    {
        SystemCommands.MinimizeWindow(this);
    }

    private void Close_OnClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnStatusLogCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() => LogScrollViewer?.ScrollToEnd());
    }
}
