using Avalonia.Controls;
using Avalonia.Input;
using LazyPhotographer.Avalonia.ViewModels;
using LazyPhotographer.Models;

namespace LazyPhotographer.Avalonia.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized
                        ? WindowState.Normal
                        : WindowState.Maximized;
                }
                else
                {
                    BeginMoveDrag(e);
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;

            // Avoid intercepting keys if user is typing in a TextBox
            if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox)
            {
                return;
            }

            if (DataContext is MainViewModel vm)
            {
                switch (e.Key)
                {
                    case Key.P:
                        vm.FlagSelectedPhoto();
                        e.Handled = true;
                        break;
                    case Key.X:
                        vm.RejectSelectedPhoto();
                        e.Handled = true;
                        break;
                    case Key.U:
                        vm.UnflagSelectedPhoto();
                        e.Handled = true;
                        break;
                    case Key.Left:
                        vm.SelectPreviousPhoto();
                        e.Handled = true;
                        break;
                    case Key.Right:
                        vm.SelectNextPhoto();
                        e.Handled = true;
                        break;
                    case Key.G:
                        if (vm.IsTab1Selected)
                        {
                            vm.FilterViewMode = ViewMode.Grid;
                            e.Handled = true;
                        }
                        break;
                    case Key.E:
                        if (vm.IsTab1Selected)
                        {
                            vm.OpenPhotoInLoupe(vm.SelectedPhoto);
                            e.Handled = true;
                        }
                        break;
                }
            }
        }
    }
}
