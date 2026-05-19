using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PhotoFolderViewer.Infrastructure;
using PhotoFolderViewer.Models;
using PhotoFolderViewer.Services;

namespace PhotoFolderViewer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".webp"
    };

    private readonly ImageCacheService _cacheService = new();
    private readonly DispatcherTimer _slideshowTimer;
    private CancellationTokenSource? _currentImageLoadCts;
    private string? _activeFolder;

    private ImageItem? _selectedImage;
    private BitmapSource? _currentImage;
    private double _zoomFactor = 1.0;
    private Stretch _viewerStretch = Stretch.Uniform;
    private bool _isSlideshowRunning;
    private bool _isFullScreen;
    private GridLength _thumbnailPaneWidth = new(270);
    private string _windowTitle = "Photo Folder Viewer";

    public MainViewModel()
    {
        Images = new ObservableCollection<ImageItem>();

        NextImageCommand = new RelayCommand(_ => SelectNextImage(), _ => Images.Count > 1);
        PreviousImageCommand = new RelayCommand(_ => SelectPreviousImage(), _ => Images.Count > 1);
        ZoomInCommand = new RelayCommand(_ => ZoomIn());
        ZoomOutCommand = new RelayCommand(_ => ZoomOut());
        ActualSizeCommand = new RelayCommand(_ => SetActualSize());
        FitToScreenCommand = new RelayCommand(_ => FitToScreen());
        ToggleSlideshowCommand = new RelayCommand(_ => ToggleSlideshow(), _ => Images.Count > 1);
        ToggleFullScreenCommand = new RelayCommand(_ => SetFullScreen(!IsFullScreen));

        _slideshowTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _slideshowTimer.Tick += (_, _) => SelectNextImage();
    }

    public ObservableCollection<ImageItem> Images { get; }

    public ImageItem? SelectedImage
    {
        get => _selectedImage;
        set
        {
            if (ReferenceEquals(_selectedImage, value))
            {
                return;
            }

            _selectedImage = value;
            OnPropertyChanged();
            _ = OnSelectedImageChangedAsync();
        }
    }

    public BitmapSource? CurrentImage
    {
        get => _currentImage;
        private set
        {
            if (ReferenceEquals(_currentImage, value))
            {
                return;
            }

            _currentImage = value;
            OnPropertyChanged();
        }
    }

    public double ZoomFactor
    {
        get => _zoomFactor;
        private set
        {
            var clamped = Math.Clamp(value, 0.05, 20.0);
            if (Math.Abs(_zoomFactor - clamped) < 0.0001)
            {
                return;
            }

            _zoomFactor = clamped;
            OnPropertyChanged();
        }
    }

    public Stretch ViewerStretch
    {
        get => _viewerStretch;
        private set
        {
            if (_viewerStretch == value)
            {
                return;
            }

            _viewerStretch = value;
            OnPropertyChanged();
        }
    }

    public bool IsSlideshowRunning
    {
        get => _isSlideshowRunning;
        private set
        {
            if (_isSlideshowRunning == value)
            {
                return;
            }

            _isSlideshowRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SlideshowMenuText));
        }
    }

    public bool IsFullScreen
    {
        get => _isFullScreen;
        private set
        {
            if (_isFullScreen == value)
            {
                return;
            }

            _isFullScreen = value;
            ThumbnailPaneWidth = _isFullScreen ? new GridLength(0) : new GridLength(270);
            OnPropertyChanged();
        }
    }

    public GridLength ThumbnailPaneWidth
    {
        get => _thumbnailPaneWidth;
        private set
        {
            if (_thumbnailPaneWidth == value)
            {
                return;
            }

            _thumbnailPaneWidth = value;
            OnPropertyChanged();
        }
    }

    public string SlideshowMenuText => IsSlideshowRunning ? "Stop Slideshow" : "Start Slideshow";

    public string WindowTitle
    {
        get => _windowTitle;
        private set
        {
            if (_windowTitle == value)
            {
                return;
            }

            _windowTitle = value;
            OnPropertyChanged();
        }
    }

    public RelayCommand NextImageCommand { get; }
    public RelayCommand PreviousImageCommand { get; }
    public RelayCommand ZoomInCommand { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ActualSizeCommand { get; }
    public RelayCommand FitToScreenCommand { get; }
    public RelayCommand ToggleSlideshowCommand { get; }
    public RelayCommand ToggleFullScreenCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync(string? startupTarget)
    {
        var fallback = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var folder = fallback;
        var selectedPath = string.Empty;

        if (!string.IsNullOrWhiteSpace(startupTarget))
        {
            if (File.Exists(startupTarget))
            {
                selectedPath = startupTarget;
                folder = Path.GetDirectoryName(startupTarget) ?? fallback;
            }
            else if (Directory.Exists(startupTarget))
            {
                folder = startupTarget;
            }
        }

        if (!Directory.Exists(folder))
        {
            folder = fallback;
        }

        _activeFolder = folder;
        WindowTitle = $"Photo Folder Viewer - {folder}";

        var files = Directory
            .EnumerateFiles(folder)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Images.Clear();
        foreach (var file in files)
        {
            Images.Add(new ImageItem
            {
                FilePath = file,
                FileName = Path.GetFileName(file)
            });
        }

        RaiseCanExecute();

        if (Images.Count == 0)
        {
            CurrentImage = null;
            return;
        }

        var initial = string.IsNullOrWhiteSpace(selectedPath)
            ? Images[0]
            : Images.FirstOrDefault(item => string.Equals(item.FilePath, selectedPath, StringComparison.OrdinalIgnoreCase)) ?? Images[0];

        SelectedImage = initial;
        await WarmVisibleThumbnailsAsync(GetSelectedIndex(), 40, CancellationToken.None).ConfigureAwait(false);
    }

    public void SelectNextImage()
    {
        if (Images.Count == 0)
        {
            return;
        }

        var index = GetSelectedIndex();
        var next = (index + 1) % Images.Count;
        SelectedImage = Images[next];
    }

    public void SelectPreviousImage()
    {
        if (Images.Count == 0)
        {
            return;
        }

        var index = GetSelectedIndex();
        var previous = (index - 1 + Images.Count) % Images.Count;
        SelectedImage = Images[previous];
    }

    public void ZoomIn()
    {
        ViewerStretch = Stretch.None;
        ZoomFactor += 0.12;
    }

    public void ZoomOut()
    {
        ViewerStretch = Stretch.None;
        ZoomFactor -= 0.12;
    }

    public void ZoomByMouseWheel(int wheelDelta)
    {
        if (wheelDelta > 0)
        {
            ZoomIn();
            return;
        }

        ZoomOut();
    }

    public void SetActualSize()
    {
        ViewerStretch = Stretch.None;
        ZoomFactor = 1.0;
    }

    public void FitToScreen()
    {
        ViewerStretch = Stretch.Uniform;
        ZoomFactor = 1.0;
    }

    public void ToggleSlideshow()
    {
        if (IsSlideshowRunning)
        {
            _slideshowTimer.Stop();
            IsSlideshowRunning = false;
            return;
        }

        if (Images.Count <= 1)
        {
            return;
        }

        _slideshowTimer.Start();
        IsSlideshowRunning = true;
    }

    public void SetFullScreen(bool isEnabled)
    {
        IsFullScreen = isEnabled;
    }

    public void StopSlideshow()
    {
        _slideshowTimer.Stop();
        IsSlideshowRunning = false;
    }

    public bool TryDeleteSelectedImage(out string? errorMessage)
    {
        errorMessage = null;

        var target = SelectedImage;
        if (target is null)
        {
            errorMessage = "No photo is selected.";
            return false;
        }

        var index = Images.IndexOf(target);
        if (index < 0)
        {
            errorMessage = "The selected photo is not available in the current list.";
            return false;
        }

        try
        {
            File.Delete(target.FilePath);
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to delete '{target.FileName}'. {ex.Message}";
            return false;
        }

        Images.RemoveAt(index);

        if (Images.Count == 0)
        {
            StopSlideshow();
            SelectedImage = null;
            CurrentImage = null;
            FitToScreen();
            WindowTitle = string.IsNullOrWhiteSpace(_activeFolder)
                ? "Photo Folder Viewer"
                : $"Photo Folder Viewer - {_activeFolder}";
            RaiseCanExecute();
            return true;
        }

        if (Images.Count <= 1)
        {
            StopSlideshow();
        }

        var nextIndex = Math.Min(index, Images.Count - 1);
        SelectedImage = Images[nextIndex];
        RaiseCanExecute();
        return true;
    }

    private async Task OnSelectedImageChangedAsync()
    {
        var selected = SelectedImage;
        if (selected is null)
        {
            return;
        }

        _currentImageLoadCts?.Cancel();
        _currentImageLoadCts?.Dispose();
        _currentImageLoadCts = new CancellationTokenSource();
        var token = _currentImageLoadCts.Token;

        try
        {
            var image = await _cacheService.GetOrLoadFullAsync(selected.FilePath, token).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CurrentImage = image;
                WindowTitle = $"Photo Folder Viewer - {selected.FileName}";
            });

            _ = PreloadUpcomingAsync(GetSelectedIndex(), 3);
            _ = WarmVisibleThumbnailsAsync(GetSelectedIndex(), 40, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            await Application.Current.Dispatcher.InvokeAsync(() => CurrentImage = null);
        }
    }

    private async Task PreloadUpcomingAsync(int centerIndex, int count)
    {
        var tasks = new List<Task>(count);
        for (var offset = 1; offset <= count; offset++)
        {
            var idx = centerIndex + offset;
            if (idx >= Images.Count)
            {
                break;
            }

            var path = Images[idx].FilePath;
            tasks.Add(_cacheService.GetOrLoadFullAsync(path, CancellationToken.None));
        }

        if (tasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task WarmVisibleThumbnailsAsync(int centerIndex, int radius, CancellationToken cancellationToken)
    {
        if (Images.Count == 0)
        {
            return;
        }

        var min = Math.Max(0, centerIndex - radius);
        var max = Math.Min(Images.Count - 1, centerIndex + radius);

        var tasks = new List<Task>(max - min + 1);
        for (var i = min; i <= max; i++)
        {
            var item = Images[i];
            if (item.Thumbnail is not null)
            {
                continue;
            }

            tasks.Add(LoadThumbnailForItemAsync(item, cancellationToken));
        }

        if (tasks.Count == 0)
        {
            return;
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task LoadThumbnailForItemAsync(ImageItem item, CancellationToken cancellationToken)
    {
        try
        {
            var thumb = await _cacheService.GetOrLoadThumbnailAsync(item.FilePath, 320, cancellationToken).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() => item.Thumbnail = thumb);
        }
        catch
        {
        }
    }

    private int GetSelectedIndex()
    {
        if (SelectedImage is null)
        {
            return 0;
        }

        var index = Images.IndexOf(SelectedImage);
        return index < 0 ? 0 : index;
    }

    private void RaiseCanExecute()
    {
        NextImageCommand.RaiseCanExecuteChanged();
        PreviousImageCommand.RaiseCanExecuteChanged();
        ToggleSlideshowCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
