using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels.Users;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp3.Views.Users
{
    public partial class UsersView : UserControl
    {
        public UsersView()
        {
            InitializeComponent();
        }

        private void OverlayBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not UsersViewModel vm) return;

            if (vm.IsDeleteOpen)
                vm.CancelDeleteCommand.Execute(null);
            else if (vm.IsFormOpen)
                vm.CloseFormCommand.Execute(null);
        }

    }
}
