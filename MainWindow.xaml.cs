using System.IO;
using System.Windows;
using System.Windows.Input;
using PhotoFolderViewer.ViewModels;

namespace PhotoFolderViewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;
    private WindowState _previousWindowState;
    private bool _isTopmost;

    public MainWindow(string? startupTarget)
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.InitializeAsync(startupTarget);
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsFullScreen))
            {
                ApplyFullScreenState(_viewModel.IsFullScreen);
            }
        };
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Delete:
                ConfirmAndDeleteSelectedPhoto();
                e.Handled = true;
                break;
            case Key.Right:
            case Key.Down:
                _viewModel.SelectNextImage();
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Up:
                _viewModel.SelectPreviousImage();
                e.Handled = true;
                break;
            case Key.Escape when _viewModel.IsFullScreen:
                _viewModel.SetFullScreen(false);
                e.Handled = true;
                break;
        }
    }

    private void MainImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        _viewModel.ZoomByMouseWheel(e.Delta);
        e.Handled = true;
    }

    private void DeletePhotoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ConfirmAndDeleteSelectedPhoto();
    }

    private void ApplyFullScreenState(bool fullScreen)
    {
        if (fullScreen)
        {
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            _previousWindowState = WindowState;
            _isTopmost = Topmost;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Topmost = true;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousWindowState;
            Topmost = _isTopmost;
        }
    }

    private void ConfirmAndDeleteSelectedPhoto()
    {
        if (_viewModel.SelectedImage is null)
        {
            return;
        }

        var fileName = Path.GetFileName(_viewModel.SelectedImage.FilePath);
        var result = MessageBox.Show(
            this,
            $"Delete this photo permanently?\n\n{fileName}",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes && !_viewModel.TryDeleteSelectedImage(out var errorMessage))
        {
            MessageBox.Show(
                this,
                errorMessage ?? "Could not delete the selected photo.",
                "Delete Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
