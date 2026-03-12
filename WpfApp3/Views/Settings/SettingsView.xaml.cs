using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels.Settings;

namespace WpfApp3.Views.Settings
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void OverlayBackdrop_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm) return;

            if (vm.IsDeleteOpen)
                vm.CancelDeleteCommand.Execute(null);
            else if (vm.IsFormOpen)
                vm.CloseFormCommand.Execute(null);
        }
    }
}