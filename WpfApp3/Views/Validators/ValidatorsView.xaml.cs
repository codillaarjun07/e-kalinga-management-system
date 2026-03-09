using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Models;
using WpfApp3.ViewModels.Validators;

namespace WpfApp3.Views.Validators
{
    public partial class ValidatorsView : UserControl
    {
        public ValidatorsView()
        {
            InitializeComponent();
        }

        // Click outside modal to close (Border overlay, no hover states)
        private void Overlay_ClickToClose(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ValidatorsViewModel vm)
                vm.CloseAllModalsCommand.Execute(null);
        }

        // Row click selects the row item (since SelectionUnit=None)
        private void ValidatedRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridRow row) return;
            if (row.Item is not ValidatorRecord item) return;

            if (DataContext is ValidatorsViewModel vm)
                vm.SelectedPerson = item;
        }
    }
}
