using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using WpfApp3.Models;
using WpfApp3.ViewModels.Beneficiaries;

namespace WpfApp3.Views.Beneficiaries
{
    public partial class BeneficiariesView : UserControl
    {
        private static readonly Regex DigitsOnly = new(@"^\d+$");
        private static readonly Regex MoneyChars = new(@"^[0-9.,]+$");

        public BeneficiariesView()
        {
            InitializeComponent();
        }

        private void BlockSpace_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }

        private void Money_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !MoneyChars.IsMatch(e.Text);
        }

        private void BeneficiaryRow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source) return;

            // ignore clicks on buttons / checkboxes inside the row
            var current = source;
            while (current != null)
            {
                if (current is ButtonBase || current is CheckBox)
                    return;

                current = VisualTreeHelper.GetParent(current);
            }

            if (sender is not DataGridRow row) return;
            if (row.Item is not BeneficiaryRecord record) return;
            if (DataContext is not BeneficiariesViewModel vm) return;

            if (vm.OpenProfileCommand.CanExecute(record))
                vm.OpenProfileCommand.Execute(record);
        }
    }
}
