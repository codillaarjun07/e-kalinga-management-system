using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;

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
    }
}
