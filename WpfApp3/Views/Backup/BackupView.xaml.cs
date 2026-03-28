using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels.Backup;

namespace WpfApp3.Views.Backup
{
    public partial class BackupView : UserControl
    {
        public BackupView()
        {
            InitializeComponent();
        }

        private void OverlayBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not BackupViewModel vm) return;

            if (vm.IsDeleteOpen)
                vm.CancelDeleteBackupCommand.Execute(null);
        }
    }
}