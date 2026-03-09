using System.Windows;
using System.Windows.Controls;
using WpfApp3.ViewModels.Login;

namespace WpfApp3.Views.Login
{
    public partial class LoginWindow : Window
    {
        private bool _syncing;

        public LoginWindow()
        {
            InitializeComponent();

            if (DataContext is LoginViewModel vm)
            {
                vm.LoginSucceeded += OnLoginSucceeded;
            }
        }

        private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;

            if (sender is PasswordBox pb)
            {
                // ✅ for placeholder triggers
                pb.Tag = pb.Password.Length;

                _syncing = true;

                // If we're in "show" mode, keep the TextBox updated
                if (PwdToggle?.IsChecked == true && PwdText != null && PwdText.Text != pb.Password)
                {
                    PwdText.Text = pb.Password;
                    PwdText.CaretIndex = PwdText.Text.Length;
                }

                if (DataContext is LoginViewModel vm)
                    vm.Password = pb.Password;

                _syncing = false;
            }
        }

        private void PasswordText_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_syncing) return;

            if (sender is TextBox tb)
            {
                _syncing = true;

                // Keep PasswordBox updated too
                if (PwdBox != null && PwdBox.Password != tb.Text)
                {
                    PwdBox.Password = tb.Text;
                    PwdBox.Tag = tb.Text.Length; // ✅ keep placeholder logic consistent
                }

                if (DataContext is LoginViewModel vm)
                    vm.Password = tb.Text;

                _syncing = false;
            }
        }

        private void PwdToggle_OnChecked(object sender, RoutedEventArgs e)
        {
            // Show password: copy from PasswordBox to TextBox and focus it
            _syncing = true;
            PwdText.Text = PwdBox.Password;
            PwdText.CaretIndex = PwdText.Text.Length;
            _syncing = false;

            PwdText.Focus();
        }

        private void PwdToggle_OnUnchecked(object sender, RoutedEventArgs e)
        {
            // Hide password: copy back to PasswordBox and focus it
            _syncing = true;
            PwdBox.Password = PwdText.Text;
            PwdBox.Tag = PwdBox.Password.Length;
            _syncing = false;

            PwdBox.Focus();
        }

        private void OnLoginSucceeded()
        {
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}