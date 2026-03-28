using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels;
using WpfApp3.ViewModels.Settings;
using WpfApp3.Views.Login;

namespace WpfApp3.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += SettingsView_Loaded;
            DataContextChanged += SettingsView_DataContextChanged;
            Unloaded += SettingsView_Unloaded;
        }

        private void SettingsView_Loaded(object sender, RoutedEventArgs e)
        {
            SyncConnectionPassword();
            HookConnectionEvents();
        }

        private void SettingsView_Unloaded(object sender, RoutedEventArgs e)
        {
            UnhookConnectionEvents();
        }

        private void SettingsView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is SettingsViewModel oldVm)
                oldVm.Connection.ReLoginRequested -= OnReLoginRequested;

            SyncConnectionPassword();
            HookConnectionEvents();
        }

        private void HookConnectionEvents()
        {
            if (DataContext is SettingsViewModel vm)
            {
                vm.Connection.ReLoginRequested -= OnReLoginRequested;
                vm.Connection.ReLoginRequested += OnReLoginRequested;
            }
        }

        private void UnhookConnectionEvents()
        {
            if (DataContext is SettingsViewModel vm)
                vm.Connection.ReLoginRequested -= OnReLoginRequested;
        }

        private void OnReLoginRequested()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var login = new LoginWindow();
                login.Show();

                var hostWindow = Window.GetWindow(this);
                hostWindow?.Close();
            });
        }

        private void SyncConnectionPassword()
        {
            if (DataContext is SettingsViewModel vm && ConnectionPasswordInput != null)
            {
                ConnectionPasswordInput.Password = vm.Connection.Password ?? string.Empty;
            }
        }

        private void OverlayBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;

            if (vm.IsDeleteOpen)
                vm.CancelDeleteCommand.Execute(null);
            else if (vm.IsFormOpen)
                vm.CloseFormCommand.Execute(null);
        }

        private void ConnectionPasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.Connection.Password = ConnectionPasswordInput.Password;
        }

        private void LocalMode_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.Connection.Mode = "Local";
        }

        private void ServerMode_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.Connection.Mode = "Server";
        }
    }
}