using System.IO;
using System.Windows;
using System.Windows.Input;
using PhotoFolderViewer.ViewModels;
using Forms = System.Windows.Forms;
using WpfMessageBox = System.Windows.MessageBox;
using WpfOpenFileDialog = Microsoft.Win32.OpenFileDialog;

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

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
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

    private async void OpenPhotoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WpfOpenFileDialog
        {
            Title = "Open Photo",
            CheckFileExists = true,
            CheckPathExists = true,
            Multiselect = false,
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _viewModel.OpenTargetAsync(dialog.FileName);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                this,
                $"Could not open photo. {ex.Message}",
                "Open Photo Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async void OpenFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Select a folder containing photos",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        try
        {
            await _viewModel.OpenTargetAsync(dialog.SelectedPath);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(
                this,
                $"Could not open folder. {ex.Message}",
                "Open Folder Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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
        var result = WpfMessageBox.Show(
            this,
            $"Delete this photo permanently?\n\n{fileName}",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes && !_viewModel.TryDeleteSelectedImage(out var errorMessage))
        {
            WpfMessageBox.Show(
                this,
                errorMessage ?? "Could not delete the selected photo.",
                "Delete Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
