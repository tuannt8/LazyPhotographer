using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using LazyPhotographer.Common.Metadata;
using LazyPhotographer.Common.Models;
using LazyPhotographer.Common.Services;
using LazyPhotographer.Interop;
using LazyPhotographer.Models;

namespace LazyPhotographer.ViewModels
{
    public abstract class MainViewModelBase<TPhotoItem, TImageSource> : INotifyPropertyChanged
        where TPhotoItem : PhotoItemBase
        where TImageSource : class
    {
        private int _selectedTabIndex = 0;
        private string _currentDirectory = string.Empty;
        private TPhotoItem? _selectedPhoto;
        private bool _isLoading;
        private string _statusMessage = "Ready";
        private CancellationTokenSource? _folderLoadingCts;

        // Tab 1 state
        private ViewMode _filterViewMode = ViewMode.Grid;
        private FilterSelection _filterSelection = FilterSelection.All;

        // Tab 2 state
        private TImageSource? _previewImageSource;
        private bool _isRenderingAdjustment;
        private bool _showBeforeAfter;
        private NativeImageBridge? _activeBridge;
        private string _activeLoadedPath = string.Empty;

        // Tab 3 state
        private ExportPresetModel? _selectedExportPreset;
        private string _exportDestinationFolder = string.Empty;
        private bool _isExporting;
        private double _exportProgress;
        private string _exportStatus = string.Empty;

        public ObservableCollection<TPhotoItem> Photos { get; } = new();
        public ObservableCollection<TPhotoItem> FilteredPhotos { get; } = new();
        public ObservableCollection<TPhotoItem> FlaggedPhotos { get; } = new();
        public ObservableCollection<ExportPresetModel> ExportPresets { get; } = new();

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (SetField(ref _selectedTabIndex, value))
                {
                    OnPropertyChanged(nameof(IsTab1Selected));
                    OnPropertyChanged(nameof(IsTab2Selected));
                    OnPropertyChanged(nameof(IsTab3Selected));
                    OnPropertyChanged(nameof(IsTab4Selected));

                    if (_selectedTabIndex == 1 && SelectedPhoto != null)
                    {
                        _ = RenderCurrentAdjustmentsAsync();
                    }
                    else if (_selectedTabIndex == 2)
                    {
                        UpdateFlaggedPhotosList();
                    }
                    else if (_selectedTabIndex == 3)
                    {
                        RefreshCacheStats();
                    }
                }
            }
        }

        public bool IsTab1Selected => _selectedTabIndex == 0;
        public bool IsTab2Selected => _selectedTabIndex == 1;
        public bool IsTab3Selected => _selectedTabIndex == 2;
        public bool IsTab4Selected => _selectedTabIndex == 3;

        public string CurrentDirectory
        {
            get => _currentDirectory;
            set => SetField(ref _currentDirectory, value);
        }

        public TPhotoItem? SelectedPhoto
        {
            get => _selectedPhoto;
            set
            {
                var old = _selectedPhoto;
                if (SetField(ref _selectedPhoto, value))
                {
                    foreach (var p in Photos)
                    {
                        p.IsSelected = (p == value);
                    }

                    OnSelectedPhotoChanged(old, value);

                    if (value != null && IsTab2Selected)
                    {
                        _ = LoadAndRenderSelectedPhotoAsync();
                    }
                }
            }
        }

        protected virtual void OnSelectedPhotoChanged(TPhotoItem? oldPhoto, TPhotoItem? newPhoto)
        {
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        // Tab 1 View properties
        public ViewMode FilterViewMode
        {
            get => _filterViewMode;
            set
            {
                if (SetField(ref _filterViewMode, value))
                {
                    OnPropertyChanged(nameof(IsGridView));
                    OnPropertyChanged(nameof(IsLoupeView));
                    OnPropertyChanged(nameof(ViewModeButtonText));
                }
            }
        }

        public bool IsGridView => _filterViewMode == ViewMode.Grid;
        public bool IsLoupeView => _filterViewMode == ViewMode.LoupeWithFilmstrip;
        public string ViewModeButtonText => IsGridView ? "🖼️ Loupe Mode" : "🔲 Grid Mode";

        public FilterSelection FilterSelectionMode
        {
            get => _filterSelection;
            set
            {
                if (SetField(ref _filterSelection, value))
                {
                    ApplyFilter();
                }
            }
        }

        // Tab 2 Properties
        public TImageSource? PreviewImageSource
        {
            get => _previewImageSource;
            set => SetField(ref _previewImageSource, value);
        }

        public bool ShowBeforeAfter
        {
            get => _showBeforeAfter;
            set
            {
                if (SetField(ref _showBeforeAfter, value))
                {
                    OnPropertyChanged(nameof(BeforeAfterButtonText));
                    _ = RenderCurrentAdjustmentsAsync();
                }
            }
        }

        public string BeforeAfterButtonText => _showBeforeAfter ? "Viewing Original" : "Original (Before)";

        public bool IsRenderingAdjustment
        {
            get => _isRenderingAdjustment;
            set => SetField(ref _isRenderingAdjustment, value);
        }

        // Tab 3 Properties
        public ExportPresetModel? SelectedExportPreset
        {
            get => _selectedExportPreset;
            set => SetField(ref _selectedExportPreset, value);
        }

        public string ExportDestinationFolder
        {
            get => _exportDestinationFolder;
            set => SetField(ref _exportDestinationFolder, value);
        }

        public bool IsExporting
        {
            get => _isExporting;
            set => SetField(ref _isExporting, value);
        }

        public double ExportProgress
        {
            get => _exportProgress;
            set => SetField(ref _exportProgress, value);
        }

        public string ExportStatus
        {
            get => _exportStatus;
            set => SetField(ref _exportStatus, value);
        }

        // Loupe Mode Progressive Display State
        private TImageSource? _loupeImage;
        private bool _isHighResLoaded;
        private string _loupeResolutionText = string.Empty;

        public TImageSource? LoupeImage
        {
            get => _loupeImage;
            set => SetField(ref _loupeImage, value);
        }

        public bool IsHighResLoaded
        {
            get => _isHighResLoaded;
            set => SetField(ref _isHighResLoaded, value);
        }

        public string LoupeResolutionText
        {
            get => _loupeResolutionText;
            set => SetField(ref _loupeResolutionText, value);
        }

        // Tab 4 (Settings) State & Persistent Config
        private int _thumbnailCacheSizeSetting = SettingsService.Current.ThumbnailCacheCapacity;
        private int _highResCacheSizeSetting = SettingsService.Current.HighResCacheCapacity;
        private int _highResMaxDimensionSetting = SettingsService.Current.HighResMaxDimension;
        private string _cacheStatsText = string.Empty;
        private string _settingsFeedback = string.Empty;

        public int ThumbnailCacheSizeSetting
        {
            get => _thumbnailCacheSizeSetting;
            set
            {
                if (SetField(ref _thumbnailCacheSizeSetting, value))
                {
                    SaveCurrentSettings();
                }
            }
        }

        public int HighResCacheSizeSetting
        {
            get => _highResCacheSizeSetting;
            set
            {
                if (SetField(ref _highResCacheSizeSetting, value))
                {
                    SaveCurrentSettings();
                }
            }
        }

        public int HighResMaxDimensionSetting
        {
            get => _highResMaxDimensionSetting;
            set
            {
                if (SetField(ref _highResMaxDimensionSetting, value))
                {
                    SaveCurrentSettings();
                }
            }
        }

        public string CacheStatsText
        {
            get => _cacheStatsText;
            set => SetField(ref _cacheStatsText, value);
        }

        public string SettingsFeedback
        {
            get => _settingsFeedback;
            set => SetField(ref _settingsFeedback, value);
        }

        // Commands
        public ICommand SelectTabCommand { get; }
        public ICommand OpenFolderCommand { get; }
        public ICommand CreateSampleRafFilesCommand { get; }
        public ICommand ToggleViewModeCommand { get; }
        public ICommand SetFilterCommand { get; }
        public ICommand TogglePickPhotoCommand { get; }
        public ICommand FlagPhotoCommand { get; }
        public ICommand RejectPhotoCommand { get; }
        public ICommand UnflagPhotoCommand { get; }
        public ICommand SelectPhotoCommand { get; }
        public ICommand OpenInLoupeCommand { get; }
        public ICommand SelectPreviousPhotoCommand { get; }
        public ICommand SelectNextPhotoCommand { get; }
        public ICommand ApplyAiAutoToneCommand { get; }
        public ICommand ResetAdjustmentsCommand { get; }
        public ICommand ToggleBeforeAfterCommand { get; }
        public ICommand SelectPresetCommand { get; }
        public ICommand BrowseExportFolderCommand { get; }
        public ICommand StartExportCommand { get; }
        public ICommand OpenExportFolderCommand { get; }
        public ICommand ClearCacheCommand { get; }
        public ICommand ResetSettingsCommand { get; }

        protected MainViewModelBase()
        {
            SelectTabCommand = new RelayCommand<string>(OnSelectTab);
            OpenFolderCommand = new RelayCommand(async () => await OnOpenFolderAsync());
            CreateSampleRafFilesCommand = new RelayCommand(async () => await OnCreateSampleRafFilesAsync());
            ToggleViewModeCommand = new RelayCommand(OnToggleViewMode);
            SetFilterCommand = new RelayCommand<string>(OnSetFilter);
            TogglePickPhotoCommand = new RelayCommand<TPhotoItem>(TogglePickPhoto);
            FlagPhotoCommand = new RelayCommand<TPhotoItem>(OnFlagPhoto);
            RejectPhotoCommand = new RelayCommand<TPhotoItem>(OnRejectPhoto);
            UnflagPhotoCommand = new RelayCommand<TPhotoItem>(OnUnflagPhoto);
            SelectPhotoCommand = new RelayCommand<TPhotoItem>(OnSelectPhoto);
            OpenInLoupeCommand = new RelayCommand<TPhotoItem>(OpenPhotoInLoupe);
            SelectPreviousPhotoCommand = new RelayCommand(SelectPreviousPhoto);
            SelectNextPhotoCommand = new RelayCommand(SelectNextPhoto);
            ApplyAiAutoToneCommand = new RelayCommand(async () => await OnApplyAiAutoToneAsync());
            ResetAdjustmentsCommand = new RelayCommand(OnResetAdjustments);
            ToggleBeforeAfterCommand = new RelayCommand(() => ShowBeforeAfter = !ShowBeforeAfter);
            SelectPresetCommand = new RelayCommand<ExportPresetModel>(preset =>
            {
                if (preset != null) SelectedExportPreset = preset;
            });
            BrowseExportFolderCommand = new RelayCommand(async () => await OnBrowseExportFolderAsync());
            StartExportCommand = new RelayCommand(async () => await OnStartExportAsync());
            OpenExportFolderCommand = new RelayCommand(OnOpenExportFolder);
            ClearCacheCommand = new RelayCommand(ClearAllCaches);
            ResetSettingsCommand = new RelayCommand(ResetSettingsToDefaults);

            foreach (var preset in ExportPresetModel.GetDefaultPresets())
            {
                ExportPresets.Add(preset);
            }
            SelectedExportPreset = ExportPresets.FirstOrDefault();

            string defaultExport = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "LazyPhotographer_Exports");
            ExportDestinationFolder = defaultExport;
        }

        public void OpenPhotoInLoupe(TPhotoItem? photo)
        {
            if (photo != null)
            {
                SelectedPhoto = photo;
            }
            if (SelectedPhoto != null)
            {
                FilterViewMode = ViewMode.LoupeWithFilmstrip;
                StatusMessage = $"Loupe Mode: {SelectedPhoto.FileName}";
            }
        }

        public void FlagSelectedPhoto() => OnFlagPhoto(SelectedPhoto);
        public void RejectSelectedPhoto() => OnRejectPhoto(SelectedPhoto);
        public void UnflagSelectedPhoto() => OnUnflagPhoto(SelectedPhoto);

        public void SelectPreviousPhoto()
        {
            if (FilteredPhotos.Count == 0) return;
            int idx = SelectedPhoto != null ? FilteredPhotos.IndexOf(SelectedPhoto) : -1;
            if (idx > 0)
            {
                SelectedPhoto = FilteredPhotos[idx - 1];
            }
            else
            {
                SelectedPhoto = FilteredPhotos[0];
            }
        }

        public void SelectNextPhoto()
        {
            if (FilteredPhotos.Count == 0) return;
            int idx = SelectedPhoto != null ? FilteredPhotos.IndexOf(SelectedPhoto) : -1;
            if (idx >= 0 && idx + 1 < FilteredPhotos.Count)
            {
                SelectedPhoto = FilteredPhotos[idx + 1];
            }
            else if (idx < 0)
            {
                SelectedPhoto = FilteredPhotos[0];
            }
        }

        public void InitializeSamplePhotosInBackground()
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(250);
                string sampleDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePhotos");
                if (!Directory.Exists(sampleDir) || !Directory.EnumerateFiles(sampleDir, "*.RAF").Any())
                {
                    Directory.CreateDirectory(sampleDir);
                    NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "FUJI_XT5_0001.RAF"), "FUJIFILM X-T5", 1400, 933);
                    NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "FUJI_XT5_0002.RAF"), "FUJIFILM X-T5", 1400, 933);
                    NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "FUJI_XH2_0003.RAF"), "FUJIFILM X-H2", 1400, 933);
                }
                await LoadFolderPhotosAsync(sampleDir);
            });
        }

        // UI-Framework specific hooks implemented by MAUI and Avalonia subclasses
        protected abstract TPhotoItem CreatePhotoItem(string filePath, byte[]? thumbnailBytes);
        protected abstract TImageSource? CreateImageSourceFromBytes(byte[]? imageBytes);
        protected abstract void RunOnUiThread(Action action);
        protected abstract Task<string?> PickFolderAsync();

        private void OnSelectTab(string? tabIndexStr)
        {
            if (int.TryParse(tabIndexStr, out int idx))
            {
                SelectedTabIndex = idx;
            }
        }

        public bool MatchesFilter(TPhotoItem photo)
        {
            return FilterSelectionMode switch
            {
                FilterSelection.FlaggedOnly => photo.Flag == FlagStatus.Flagged,
                FilterSelection.UnflaggedOnly => photo.Flag == FlagStatus.Unflagged,
                FilterSelection.RejectedOnly => photo.Flag == FlagStatus.Rejected,
                _ => true
            };
        }

        public async Task LoadFolderPhotosAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            _folderLoadingCts?.Cancel();
            _folderLoadingCts?.Dispose();
            var cts = new CancellationTokenSource();
            _folderLoadingCts = cts;
            var ct = cts.Token;

            IsLoading = true;
            CurrentDirectory = folderPath;
            StatusMessage = $"Finding files in {Path.GetFileName(folderPath)}...";

            await Task.Run(async () =>
            {
                try
                {
                    // 1. FAST FILE DISCOVERY: Find all files and file count first
                    var files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f =>
                        {
                            string ext = Path.GetExtension(f).ToLowerInvariant();
                            return ext == ".raf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".tif" || ext == ".tiff";
                        })
                        .OrderBy(f => f)
                        .ToList();

                    if (ct.IsCancellationRequested) return;

                    int total = files.Count;

                    // 2. CREATE ALL PLACEHOLDER ITEMS IMMEDIATELY
                    var placeholders = new List<TPhotoItem>(total);
                    foreach (var file in files)
                    {
                        var item = CreatePhotoItem(file, null);
                        item.Flag = FlagStatus.Unflagged;
                        item.CameraModel = "FUJIFILM";
                        item.ExposureInfo = "Loading...";
                        item.IsMetadataLoaded = false;

                        item.Adjustments.PropertyChanged += (s, e) =>
                        {
                            if (SelectedPhoto == item && IsTab2Selected)
                            {
                                _ = RenderCurrentAdjustmentsAsync();
                            }
                        };

                        placeholders.Add(item);
                    }

                    // Populate GUI with placeholders immediately so scrolling & viewing starts with zero lag
                    RunOnUiThread(() =>
                    {
                        if (ct.IsCancellationRequested) return;

                        Photos.Clear();
                        FilteredPhotos.Clear();

                        foreach (var p in placeholders)
                        {
                            Photos.Add(p);
                            if (MatchesFilter(p))
                            {
                                FilteredPhotos.Add(p);
                            }
                        }

                        SelectedPhoto = FilteredPhotos.FirstOrDefault() ?? Photos.FirstOrDefault();
                        IsLoading = false;
                        StatusMessage = $"Found {total} photos. Ready.";
                    });

                    // 3. READ METADATA PROGRESSIVELY IN BACKGROUND WITHOUT BLOCKING GUI
                    await Task.Delay(40, ct); // Yield to let UI render the initial view

                    int processed = 0;
                    foreach (var item in placeholders)
                    {
                        if (ct.IsCancellationRequested) return;

                        var meta = FastMetadataReader.ReadMetadata(item.FilePath);
                        item.CameraModel = meta.CameraModel;
                        item.ExposureInfo = meta.ExposureInfo;
                        item.Width = meta.Width;
                        item.Height = meta.Height;
                        item.OrientationDegrees = meta.OrientationDegrees;
                        item.OrientationTag = meta.OrientationTag;
                        item.IsMetadataLoaded = true;

                        processed++;
                        if (processed % 20 == 0)
                        {
                            await Task.Delay(10, ct); // Yield CPU to keep GUI and render thread completely responsive
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Operation was cancelled
                }
                catch (Exception ex)
                {
                    RunOnUiThread(() =>
                    {
                        IsLoading = false;
                        StatusMessage = $"Folder error: {ex.Message}";
                    });
                }
            }, ct);
        }

        private async Task OnOpenFolderAsync()
        {
            try
            {
                string? folder = await PickFolderAsync();
                if (!string.IsNullOrEmpty(folder))
                {
                    await LoadFolderPhotosAsync(folder);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Folder select error: {ex.Message}";
            }
        }

        private async Task OnBrowseExportFolderAsync()
        {
            try
            {
                string? folder = await PickFolderAsync();
                if (!string.IsNullOrEmpty(folder))
                {
                    ExportDestinationFolder = folder;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Folder select error: {ex.Message}";
            }
        }

        private async Task OnCreateSampleRafFilesAsync()
        {
            IsLoading = true;
            StatusMessage = "Generating synthetic Fujifilm RAF files with 16-bit calibration charts...";
            string sampleDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SamplePhotos");
            Directory.CreateDirectory(sampleDir);

            await Task.Run(() =>
            {
                NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "DSCF1001_Sunset.RAF"), "FUJIFILM X-T5", 1600, 1067);
                NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "DSCF1002_StudioPortrait.RAF"), "FUJIFILM X-H2S", 1600, 1067);
                NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "DSCF1003_Landscape.RAF"), "FUJIFILM X-T4", 1600, 1067);
                NativeImageBridge.CreateSampleRaf(Path.Combine(sampleDir, "DSCF1004_StreetNight.RAF"), "FUJIFILM X100VI", 1600, 1067);
            });

            await LoadFolderPhotosAsync(sampleDir);
            StatusMessage = "Created and loaded sample Fujifilm RAF files.";
            IsLoading = false;
        }

        private void OnToggleViewMode()
        {
            FilterViewMode = FilterViewMode == ViewMode.Grid ? ViewMode.LoupeWithFilmstrip : ViewMode.Grid;
        }

        private void OnSetFilter(string? mode)
        {
            if (mode == "Flagged") FilterSelectionMode = FilterSelection.FlaggedOnly;
            else if (mode == "Unflagged") FilterSelectionMode = FilterSelection.UnflaggedOnly;
            else if (mode == "Rejected") FilterSelectionMode = FilterSelection.RejectedOnly;
            else FilterSelectionMode = FilterSelection.All;
        }

        public void ApplyFilter()
        {
            FilteredPhotos.Clear();
            var matches = Photos.Where(p =>
            {
                return FilterSelectionMode switch
                {
                    FilterSelection.FlaggedOnly => p.Flag == FlagStatus.Flagged,
                    FilterSelection.UnflaggedOnly => p.Flag == FlagStatus.Unflagged,
                    FilterSelection.RejectedOnly => p.Flag == FlagStatus.Rejected,
                    _ => true
                };
            }).ToList();

            foreach (var match in matches)
            {
                FilteredPhotos.Add(match);
            }

            UpdateFlaggedPhotosList();
        }

        protected void UpdateFlaggedPhotosList()
        {
            FlaggedPhotos.Clear();
            foreach (var p in Photos.Where(p => p.Flag == FlagStatus.Flagged))
            {
                FlaggedPhotos.Add(p);
            }
            OnPropertyChanged(nameof(FlaggedPhotosCountText));
        }

        public string FlaggedPhotosCountText => $"{FlaggedPhotos.Count} Flagged of {Photos.Count} Photos";

        public void TogglePickPhoto(TPhotoItem? photo)
        {
            photo ??= SelectedPhoto;
            if (photo != null)
            {
                photo.Flag = photo.IsFlagged ? FlagStatus.Unflagged : FlagStatus.Flagged;
                UpdateFlaggedPhotosList();
                if (FilterSelectionMode != FilterSelection.All)
                {
                    ApplyFilter();
                }
                StatusMessage = photo.IsFlagged
                    ? $"Flagged (Picked): {photo.FileName}"
                    : $"Unflagged: {photo.FileName}";
            }
        }

        private void OnFlagPhoto(TPhotoItem? photo)
        {
            photo ??= SelectedPhoto;
            if (photo != null)
            {
                photo.Flag = FlagStatus.Flagged;
                UpdateFlaggedPhotosList();
                StatusMessage = $"Flagged (Picked): {photo.FileName}";
            }
        }

        private void OnRejectPhoto(TPhotoItem? photo)
        {
            photo ??= SelectedPhoto;
            if (photo != null)
            {
                photo.Flag = FlagStatus.Rejected;
                UpdateFlaggedPhotosList();
                StatusMessage = $"Rejected: {photo.FileName}";
            }
        }

        private void OnUnflagPhoto(TPhotoItem? photo)
        {
            photo ??= SelectedPhoto;
            if (photo != null)
            {
                photo.Flag = FlagStatus.Unflagged;
                UpdateFlaggedPhotosList();
                StatusMessage = $"Unflagged: {photo.FileName}";
            }
        }

        private void OnSelectPhoto(TPhotoItem? photo)
        {
            if (photo != null)
            {
                SelectedPhoto = photo;
            }
        }

        public async Task LoadAndRenderSelectedPhotoAsync()
        {
            if (SelectedPhoto == null) return;

            IsRenderingAdjustment = true;
            try
            {
                await Task.Run(() =>
                {
                    if (_activeBridge == null || _activeLoadedPath != SelectedPhoto.FilePath)
                    {
                        _activeBridge?.Dispose();
                        _activeBridge = new NativeImageBridge();
                        _activeBridge.LoadRaw(SelectedPhoto.FilePath, out var rawInfo, false);
                        _activeLoadedPath = SelectedPhoto.FilePath;
                    }

                    var adjToUse = ShowBeforeAfter ? new ImageAdjustmentsModel().ToNative() : SelectedPhoto.Adjustments.ToNative();
                    byte[]? bmpBytes = _activeBridge.RenderPreviewBmpBytes(adjToUse, 1200, 800);

                    RunOnUiThread(() =>
                    {
                        PreviewImageSource = CreateImageSourceFromBytes(bmpBytes);
                        IsRenderingAdjustment = false;
                    });
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Rendering error: {ex.Message}";
                IsRenderingAdjustment = false;
            }
        }

        public async Task RenderCurrentAdjustmentsAsync()
        {
            if (SelectedPhoto == null) return;
            if (IsRenderingAdjustment) return;

            IsRenderingAdjustment = true;
            await Task.Run(() =>
            {
                if (_activeBridge == null || _activeLoadedPath != SelectedPhoto.FilePath)
                {
                    _activeBridge?.Dispose();
                    _activeBridge = new NativeImageBridge();
                    _activeBridge.LoadRaw(SelectedPhoto.FilePath, out var rawInfo, false);
                    _activeLoadedPath = SelectedPhoto.FilePath;
                }

                var adjToUse = ShowBeforeAfter ? new ImageAdjustmentsModel().ToNative() : SelectedPhoto.Adjustments.ToNative();
                byte[]? bmpBytes = _activeBridge.RenderPreviewBmpBytes(adjToUse, 1200, 800);

                RunOnUiThread(() =>
                {
                    PreviewImageSource = CreateImageSourceFromBytes(bmpBytes);
                    IsRenderingAdjustment = false;
                });
            });
        }

        private async Task OnApplyAiAutoToneAsync()
        {
            if (SelectedPhoto == null) return;

            StatusMessage = "Analyzing image with AI Engine for optimal tone and color balance...";
            await Task.Run(() =>
            {
                if (_activeBridge == null || _activeLoadedPath != SelectedPhoto.FilePath)
                {
                    _activeBridge?.Dispose();
                    _activeBridge = new NativeImageBridge();
                    _activeBridge.LoadRaw(SelectedPhoto.FilePath, out var rawInfo, false);
                    _activeLoadedPath = SelectedPhoto.FilePath;
                }

                var nativeAdj = SelectedPhoto.Adjustments.ToNative();
                if (_activeBridge.AnalyzeAutoTone(ref nativeAdj))
                {
                    RunOnUiThread(() =>
                    {
                        SelectedPhoto.Adjustments.FromNative(nativeAdj);
                        StatusMessage = "AI Auto-Tone applied successfully!";
                    });
                }
            });

            await RenderCurrentAdjustmentsAsync();
        }

        private void OnResetAdjustments()
        {
            if (SelectedPhoto != null)
            {
                SelectedPhoto.Adjustments.Reset();
                StatusMessage = "Adjustments reset to defaults.";
                _ = RenderCurrentAdjustmentsAsync();
            }
        }

        private async Task OnStartExportAsync()
        {
            var exportList = FlaggedPhotos.Count > 0 ? FlaggedPhotos.ToList() : (SelectedPhoto != null ? new List<TPhotoItem> { SelectedPhoto } : Photos.ToList());

            if (exportList.Count == 0)
            {
                StatusMessage = "No images selected or flagged for export.";
                return;
            }

            if (string.IsNullOrEmpty(ExportDestinationFolder))
            {
                StatusMessage = "Please choose a valid destination export folder.";
                return;
            }

            Directory.CreateDirectory(ExportDestinationFolder);

            IsExporting = true;
            ExportProgress = 0.0;
            var preset = SelectedExportPreset ?? ExportPresets.First();

            StatusMessage = $"Starting export of {exportList.Count} images using {preset.Name}...";

            await Task.Run(() =>
            {
                using var exportBridge = new NativeImageBridge();

                for (int i = 0; i < exportList.Count; ++i)
                {
                    var photo = exportList[i];
                    string ext = preset.Format switch
                    {
                        ExportFileFormat.Tiff16 => ".tif",
                        ExportFileFormat.Png => ".png",
                        _ => ".jpg"
                    };

                    string baseName = Path.GetFileNameWithoutExtension(photo.FilePath);
                    string outFileName = $"{baseName}_Export{ext}";
                    string fullOutPath = Path.Combine(ExportDestinationFolder, outFileName);

                    exportBridge.LoadRaw(photo.FilePath, out _, false);

                    var nativeAdj = photo.Adjustments.ToNative();
                    var nativeOpts = new NativeExportOptions
                    {
                        Format = (int)preset.Format,
                        JpegQuality = preset.JpegQuality,
                        MaxDimension = preset.MaxDimension,
                        BitDepth16 = preset.BitDepth16 ? 1 : 0
                    };

                    exportBridge.ExportImage(fullOutPath, nativeAdj, nativeOpts);

                    double progress = (double)(i + 1) / exportList.Count;
                    int currentIdx = i + 1;
                    RunOnUiThread(() =>
                    {
                        ExportProgress = progress;
                        ExportStatus = $"Exporting {currentIdx} of {exportList.Count}: {outFileName}...";
                    });
                }

                RunOnUiThread(() =>
                {
                    IsExporting = false;
                    ExportStatus = $"Export Complete! Successfully exported {exportList.Count} images.";
                    StatusMessage = ExportStatus;
                });
            });
        }

        public virtual void RefreshCacheStats()
        {
        }

        public virtual void ClearAllCaches()
        {
        }

        public void ResetSettingsToDefaults()
        {
            ThumbnailCacheSizeSetting = 200;
            HighResCacheSizeSetting = 20;
            HighResMaxDimensionSetting = 2560;
            SettingsFeedback = "✓ Reset to Defaults";
        }

        protected void SaveCurrentSettings()
        {
            var s = new AppSettings
            {
                ThumbnailCacheCapacity = _thumbnailCacheSizeSetting,
                HighResCacheCapacity = _highResCacheSizeSetting,
                HighResMaxDimension = _highResMaxDimensionSetting
            };
            SettingsService.Save(s);
            SettingsFeedback = "✓ Saved & Remembered";
            RefreshCacheStats();
        }

        private void OnOpenExportFolder()
        {
            if (Directory.Exists(ExportDestinationFolder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ExportDestinationFolder,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
