using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.Avalonia.Services;
using LazyPhotographer.Avalonia.ViewModels;
using LazyPhotographer.Models;

namespace LazyPhotographer.Avalonia.Controls
{
    public class FastThumbnailControl : Control
    {
        // Cached brushes and pens (zero GC allocation during render)
        private static readonly IBrush CardBgBrush = new SolidColorBrush(Color.Parse("#1E1E22"));
        private static readonly IPen SelectedPen = new Pen(new SolidColorBrush(Color.Parse("#007ACC")), 2);
        private static readonly IPen HoverPen = new Pen(new SolidColorBrush(Color.Parse("#3E3E48")), 1);
        private static readonly IPen NormalPen = new Pen(new SolidColorBrush(Color.Parse("#2A2A2E")), 1);

        private static readonly IBrush ThumbBgBrush = new SolidColorBrush(Color.Parse("#141418"));
        private static readonly IBrush PlaceholderIconBrush = new SolidColorBrush(Color.Parse("#2B2B32"));
        private static readonly IPen PlaceholderIconPen = new Pen(new SolidColorBrush(Color.Parse("#3A3A44")), 1.5);
        private static readonly IBrush BadgeBgBrush = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0));
        private static readonly IBrush ExtTextBrush = new SolidColorBrush(Color.Parse("#FFB300"));
        private static readonly IBrush DimTextBrush = new SolidColorBrush(Color.Parse("#888888"));
        private static readonly IBrush SubTextBrush = new SolidColorBrush(Color.Parse("#666666"));
        private static readonly IBrush WhiteTextBrush = Brushes.White;
        private static readonly IBrush GoldTextBrush = Brushes.Gold;
        private static readonly IBrush RedTextBrush = new SolidColorBrush(Color.Parse("#FF6B6B"));

        private static readonly IBrush BtnPickedBg = new SolidColorBrush(Color.Parse("#3D3522"));
        private static readonly IBrush BtnPickedHoverBg = new SolidColorBrush(Color.Parse("#4E432A"));
        private static readonly IPen BtnPickedPen = new Pen(Brushes.Gold, 1);

        private static readonly IBrush BtnUnpickedBg = new SolidColorBrush(Color.Parse("#2A2A2E"));
        private static readonly IBrush BtnUnpickedHoverBg = new SolidColorBrush(Color.Parse("#36363C"));
        private static readonly IPen BtnUnpickedPen = new Pen(new SolidColorBrush(Color.Parse("#3E3E44")), 1);

        private static readonly Typeface BoldTypeface = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);
        private static readonly Typeface NormalTypeface = Typeface.Default;

        // Interaction state
        public bool IsControlAttached { get; private set; }
        private bool _isCardHovered;
        private bool _isButtonHovered;
        private PhotoItem? _boundPhoto;

        // Cached text layouts to avoid font shaping allocations on scroll
        private FormattedText? _cachedNameFt;
        private FormattedText? _cachedCamFt;
        private FormattedText? _cachedExpFt;
        private FormattedText? _cachedBtnFt;
        private FormattedText? _cachedExtFt;
        private FormattedText? _cachedStatusFt;
        private FormattedText? _cachedPlaceholderFt;
        private bool _isTextDirty = true;
        private string? _lastName;
        private string? _lastCam;
        private string? _lastExp;
        private string? _lastExt;
        private bool _lastFlagState;
        private bool _lastBtnHoverState;

        public FastThumbnailControl()
        {
            ClipToBounds = true;
            Width = 270;
            Height = 280;
        }

        private Rect ThumbnailRect => new(8, 8, Bounds.Width - 16, 175);
        private Rect PickButtonRect => new(8, Bounds.Height - 36, Bounds.Width - 16, 28);

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(270, 280);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_boundPhoto != null)
            {
                _boundPhoto.PropertyChanged -= OnPhotoPropertyChanged;
                ThumbnailLoader.CancelLoad(_boundPhoto.FilePath);
            }

            _boundPhoto = DataContext as PhotoItem;
            _isTextDirty = true;

            if (_boundPhoto != null)
            {
                _boundPhoto.PropertyChanged += OnPhotoPropertyChanged;

                if (_boundPhoto.Thumbnail == null)
                {
                    ThumbnailLoader.RequestLoad(_boundPhoto, this);
                }
            }

            InvalidateVisual();
        }

        private void OnPhotoPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PhotoItem.Thumbnail) ||
                e.PropertyName == nameof(PhotoItem.IsSelected))
            {
                Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
            }
            else if (e.PropertyName == nameof(PhotoItem.Flag) ||
                     e.PropertyName == nameof(PhotoItem.IsFlagged) ||
                     e.PropertyName == nameof(PhotoItem.FileName) ||
                     e.PropertyName == nameof(PhotoItem.CameraModel) ||
                     e.PropertyName == nameof(PhotoItem.ExposureInfo))
            {
                _isTextDirty = true;
                Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Background);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            IsControlAttached = true;

            if (DataContext is PhotoItem photo && photo.Thumbnail == null)
            {
                ThumbnailLoader.RequestLoad(photo, this);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            IsControlAttached = false;

            if (_boundPhoto != null)
            {
                ThumbnailLoader.CancelLoad(_boundPhoto.FilePath);
                _boundPhoto.PropertyChanged -= OnPhotoPropertyChanged;
                _boundPhoto = null;
            }
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            _isCardHovered = true;
            UpdateHover(e);
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            UpdateHover(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _isCardHovered = false;
            _isButtonHovered = false;
            Cursor = Cursor.Default;
            _isTextDirty = true;
            InvalidateVisual();
        }

        private void UpdateHover(PointerEventArgs e)
        {
            var pos = e.GetPosition(this);
            bool overBtn = PickButtonRect.Contains(pos);
            if (overBtn != _isButtonHovered)
            {
                _isButtonHovered = overBtn;
                _isTextDirty = true;
                InvalidateVisual();
            }
            Cursor = (overBtn || ThumbnailRect.Contains(pos)) ? new Cursor(StandardCursorType.Hand) : Cursor.Default;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (DataContext is not PhotoItem photo) return;
            var pos = e.GetPosition(this);
            var ptr = e.GetCurrentPoint(this);
            if (!ptr.Properties.IsLeftButtonPressed) return;

            var vm = FindMainViewModel();

            if (PickButtonRect.Contains(pos))
            {
                // Single button action: Toggle pick state instantly
                photo.Flag = photo.IsFlagged ? FlagStatus.Unflagged : FlagStatus.Flagged;
                if (vm != null)
                {
                    vm.StatusMessage = photo.IsFlagged
                        ? $"Flagged (Picked): {photo.FileName}"
                        : $"Unflagged: {photo.FileName}";
                }
                _isTextDirty = true;
                InvalidateVisual();
                e.Handled = true;
                return;
            }

            // Clicked card body or thumbnail -> select photo
            if (vm != null)
            {
                vm.SelectedPhoto = photo;
                if (e.ClickCount >= 2)
                {
                    vm.OpenPhotoInLoupe(photo);
                }
            }
            InvalidateVisual();
            e.Handled = true;
        }

        private MainViewModel? FindMainViewModel()
        {
            var uc = this.FindAncestorOfType<UserControl>();
            return uc?.DataContext as MainViewModel;
        }

        private void EnsureTextLayouts(PhotoItem photo, Rect thumbRect, Rect btnRect)
        {
            bool flagged = photo.IsFlagged;
            if (!_isTextDirty &&
                _lastName == photo.FileName &&
                _lastCam == photo.CameraModel &&
                _lastExp == photo.ExposureInfo &&
                _lastExt == photo.FileExtension &&
                _lastFlagState == flagged &&
                _lastBtnHoverState == _isButtonHovered)
            {
                return;
            }

            _lastName = photo.FileName;
            _lastCam = photo.CameraModel;
            _lastExp = photo.ExposureInfo;
            _lastExt = photo.FileExtension;
            _lastFlagState = flagged;
            _lastBtnHoverState = _isButtonHovered;

            // 1. File Name Text
            _cachedNameFt = new FormattedText(
                _lastName,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BoldTypeface,
                12.5,
                WhiteTextBrush)
            {
                MaxTextWidth = Math.Max(10, thumbRect.Width)
            };

            // 2. Camera Model Text
            _cachedCamFt = new FormattedText(
                string.IsNullOrEmpty(_lastCam) ? "FUJIFILM" : _lastCam,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                NormalTypeface,
                11,
                DimTextBrush)
            {
                MaxTextWidth = Math.Max(10, thumbRect.Width)
            };

            // 3. Exposure Info Text
            _cachedExpFt = new FormattedText(
                string.IsNullOrEmpty(_lastExp) ? "Loading..." : _lastExp,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                NormalTypeface,
                11,
                SubTextBrush)
            {
                MaxTextWidth = Math.Max(10, thumbRect.Width)
            };

            // 4. Single Pick Button Label Text
            string btnLabel = flagged ? "★ Picked (P)" : "☆ Pick (P)";
            IBrush btnTextBrush = flagged ? GoldTextBrush : (_isButtonHovered ? WhiteTextBrush : DimTextBrush);
            _cachedBtnFt = new FormattedText(
                btnLabel,
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                BoldTypeface,
                11,
                btnTextBrush);

            // 5. Extension badge text
            if (!string.IsNullOrEmpty(_lastExt))
            {
                _cachedExtFt = new FormattedText(
                    _lastExt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    BoldTypeface,
                    10.5,
                    ExtTextBrush);
            }
            else
            {
                _cachedExtFt = null;
            }

            // 6. Status badge text (if flagged or rejected)
            if (photo.IsFlagged || photo.IsRejected)
            {
                string statusText = photo.IsFlagged ? "★ Picked" : "✕ Rejected";
                var statusBrush = photo.IsFlagged ? GoldTextBrush : RedTextBrush;
                _cachedStatusFt = new FormattedText(
                    statusText,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    BoldTypeface,
                    10.5,
                    statusBrush);
            }
            else
            {
                _cachedStatusFt = null;
            }

            // 7. Placeholder text
            _cachedPlaceholderFt = new FormattedText(
                "Loading...",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                NormalTypeface,
                11.5,
                SubTextBrush);

            _isTextDirty = false;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (DataContext is not PhotoItem photo) return;

            var cardRect = new Rect(0, 0, Bounds.Width, Bounds.Height);
            var thumbRect = ThumbnailRect;
            var btnRect = PickButtonRect;

            // Rebuild cached text objects only when dirty (zero shaping/allocations during smooth scrolling)
            EnsureTextLayouts(photo, thumbRect, btnRect);

            // 1. Draw Card Background & Selection / Hover Border
            var borderPen = photo.IsSelected ? SelectedPen : (_isCardHovered ? HoverPen : NormalPen);
            context.DrawRectangle(CardBgBrush, borderPen, new RoundedRect(cardRect, 8));

            // 2. Draw Thumbnail or Sleek Vector Placeholder
            context.DrawRectangle(ThumbBgBrush, null, new RoundedRect(thumbRect, 4));

            if (photo.Thumbnail != null)
            {
                using (context.PushClip(new RoundedRect(thumbRect, 4)))
                {
                    var imgSize = photo.Thumbnail.Size;
                    if (imgSize.Width > 0 && imgSize.Height > 0)
                    {
                        double scale = Math.Max(thumbRect.Width / imgSize.Width, thumbRect.Height / imgSize.Height);
                        double dw = imgSize.Width * scale;
                        double dh = imgSize.Height * scale;
                        double dx = thumbRect.X + (thumbRect.Width - dw) / 2.0;
                        double dy = thumbRect.Y + (thumbRect.Height - dh) / 2.0;
                        context.DrawImage(photo.Thumbnail, new Rect(dx, dy, dw, dh));
                    }
                }
            }
            else
            {
                // Smooth, high-performance placeholder:
                // Draw camera icon glyph using fast 2D geometry
                double cx = thumbRect.X + thumbRect.Width / 2.0;
                double cy = thumbRect.Y + thumbRect.Height / 2.0 - 10;

                // Camera body
                var camRect = new Rect(cx - 24, cy - 14, 48, 30);
                context.DrawRectangle(PlaceholderIconBrush, PlaceholderIconPen, new RoundedRect(camRect, 5));
                // Camera lens
                context.DrawEllipse(null, PlaceholderIconPen, new Point(cx, cy + 1), 9, 9);
                // Camera viewfinder bump
                var bumpRect = new Rect(cx - 8, cy - 20, 16, 6);
                context.DrawRectangle(PlaceholderIconBrush, PlaceholderIconPen, new RoundedRect(bumpRect, 2));

                if (_cachedPlaceholderFt != null)
                {
                    context.DrawText(_cachedPlaceholderFt, new Point(
                        cx - _cachedPlaceholderFt.Width / 2.0,
                        cy + 22));
                }

                // Request on-demand thumbnail load via non-blocking queue
                ThumbnailLoader.RequestLoad(photo, this);
            }

            // 3. Draw Badges
            // File extension badge (top-left)
            if (_cachedExtFt != null)
            {
                var badgeRect = new Rect(thumbRect.X + 6, thumbRect.Y + 6, _cachedExtFt.Width + 10, 18);
                context.DrawRectangle(BadgeBgBrush, null, new RoundedRect(badgeRect, 4));
                context.DrawText(_cachedExtFt, new Point(badgeRect.X + 5, badgeRect.Y + (18 - _cachedExtFt.Height) / 2));
            }

            // Status badge (top-right) if flagged or rejected
            if (_cachedStatusFt != null)
            {
                var statusBadgeRect = new Rect(thumbRect.Right - _cachedStatusFt.Width - 16, thumbRect.Y + 6, _cachedStatusFt.Width + 10, 18);
                context.DrawRectangle(BadgeBgBrush, null, new RoundedRect(statusBadgeRect, 4));
                context.DrawText(_cachedStatusFt, new Point(statusBadgeRect.X + 5, statusBadgeRect.Y + (18 - _cachedStatusFt.Height) / 2));
            }

            // 4. Draw Metadata Details Text (Cached)
            if (_cachedNameFt != null)
            {
                context.DrawText(_cachedNameFt, new Point(thumbRect.X, thumbRect.Bottom + 8));
            }
            if (_cachedCamFt != null)
            {
                context.DrawText(_cachedCamFt, new Point(thumbRect.X, thumbRect.Bottom + 27));
            }
            if (_cachedExpFt != null)
            {
                context.DrawText(_cachedExpFt, new Point(thumbRect.X, thumbRect.Bottom + 43));
            }

            // 5. Draw Single Pick Button using Fast 2D Graphics
            bool isFlagged = photo.IsFlagged;
            IBrush btnBg = isFlagged
                ? (_isButtonHovered ? BtnPickedHoverBg : BtnPickedBg)
                : (_isButtonHovered ? BtnUnpickedHoverBg : BtnUnpickedBg);

            IPen btnPen = isFlagged ? BtnPickedPen : BtnUnpickedPen;

            context.DrawRectangle(btnBg, btnPen, new RoundedRect(btnRect, 4));

            if (_cachedBtnFt != null)
            {
                double btnTextX = btnRect.X + (btnRect.Width - _cachedBtnFt.Width) / 2.0;
                double btnTextY = btnRect.Y + (btnRect.Height - _cachedBtnFt.Height) / 2.0;
                context.DrawText(_cachedBtnFt, new Point(btnTextX, btnTextY));
            }
        }
    }
}
