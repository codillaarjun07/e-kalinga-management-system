using System.Windows;
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

        private void Vm_LogoutRequested()
        {
            var login = new LoginWindow();
            login.Show();
            Close();
        }
    }
}
