using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using WpfApp3.ViewModels;
using WpfApp3.Views.Login;

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            if (DataContext is MainViewModel vm)
            {
                vm.LogoutRequested += Vm_LogoutRequested;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Vm_LogoutRequested()
        {
            var login = new LoginWindow();
            login.Show();
            Close();
        }

        private void Window_Deactivated(object? sender, System.EventArgs e)
        {
            CloseNotificationsPanel();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel vm)
                return;

            if (!vm.IsNotificationsOpen)
                return;

            var source = e.OriginalSource as DependencyObject;

            // Keep the bell clickable. Its command handles the open/close toggle.
            if (IsClickInside(NotificationBellButton, source))
                return;

            // Keep the notification panel clickable. Buttons inside it should still work.
            if (IsClickInside(NotificationPanelRoot, source))
                return;

            CloseNotificationsPanel();
        }

        private void CloseNotificationsPanel()
        {
            if (DataContext is MainViewModel vm && vm.IsNotificationsOpen)
                vm.IsNotificationsOpen = false;
        }

        private static bool IsClickInside(DependencyObject parent, DependencyObject? source)
        {
            while (source is not null)
            {
                if (ReferenceEquals(source, parent))
                    return true;

                source = GetParentObject(source);
            }

            return false;
        }

        private static DependencyObject? GetParentObject(DependencyObject child)
        {
            if (child is null)
                return null;

            if (child is Visual || child is Visual3D)
                return VisualTreeHelper.GetParent(child);

            if (child is FrameworkElement fe)
                return fe.Parent;

            if (child is FrameworkContentElement fce)
                return fce.Parent;

            return null;
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }
    }
}
