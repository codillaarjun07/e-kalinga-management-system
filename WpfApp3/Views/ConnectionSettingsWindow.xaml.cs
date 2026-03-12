using System.Windows;
using WpfApp3.ViewModels;

namespace WpfApp3.Views
{
    public partial class ConnectionSettingsWindow : Window
    {
        public ConnectionSettingsWindow()
        {
            InitializeComponent();

            if (DataContext is ConnectionSettingsViewModel vm)
            {
                PasswordInput.Password = vm.Password;
            }
        }

        private void PasswordInput_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionSettingsViewModel vm)
                vm.Password = PasswordInput.Password;
        }

        private void LocalMode_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionSettingsViewModel vm)
                vm.Mode = "Local";
        }

        private void ServerMode_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is ConnectionSettingsViewModel vm)
                vm.Mode = "Server";
        }
    }
}