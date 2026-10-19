using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LazyPhotographer.Avalonia.Models;
using LazyPhotographer.Avalonia.ViewModels;

namespace LazyPhotographer.Avalonia.Views
{
    public partial class FilteringView : UserControl
    {
        public FilteringView()
        {
            InitializeComponent();
        }

        private void OnPhotoThumbnailPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Control control && control.DataContext is PhotoItem photo && DataContext is MainViewModel vm)
            {
                vm.SelectedPhoto = photo;
                if (e.ClickCount >= 2)
                {
                    vm.OpenPhotoInLoupe(photo);
                    e.Handled = true;
                }
            }
        }

        private void OnPhotoThumbnailDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is Control control && control.DataContext is PhotoItem photo && DataContext is MainViewModel vm)
            {
                vm.OpenPhotoInLoupe(photo);
                e.Handled = true;
            }
        }
    }
}
