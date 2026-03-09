using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp3.Views.Allotment
{
    public partial class AllotmentView : UserControl
    {
        private static readonly Regex MoneyRegex = new(@"^\d*(?:[.,]\d{0,2})?$"); // allow 2 decimals while typing

        public AllotmentView()
        {
            InitializeComponent();
            CommandManager.AddPreviewExecutedHandler(this, OnPreviewExecuted);
        }

        // DIGITS ONLY (No of beneficiaries, in-kind qty)
        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
        }

        // MONEY (digits + optional decimal + commas/dot)
        private void Money_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb) return;

            var proposed = GetProposedText(tb, e.Text);
            e.Handled = !MoneyRegex.IsMatch(proposed);
        }

        // Block space
        private void BlockSpace_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        // Block invalid paste
        private void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Command != ApplicationCommands.Paste) return;
            if (Keyboard.FocusedElement is not TextBox tb) return;

            var mode = (tb.Tag as string) ?? "";
            var text = Clipboard.GetText() ?? "";

            if (mode == "digits")
            {
                if (text.Any(ch => !char.IsDigit(ch)))
                    e.Handled = true;
            }
            else if (mode == "money")
            {
                var proposed = GetProposedText(tb, text);
                if (!MoneyRegex.IsMatch(proposed))
                    e.Handled = true;
            }
        }

        private static string GetProposedText(TextBox tb, string input)
        {
            var text = tb.Text ?? "";
            var start = tb.SelectionStart;
            var length = tb.SelectionLength;

            if (length > 0)
                text = text.Remove(start, length);

            text = text.Insert(start, input);
            return text;
        }
    }
}
