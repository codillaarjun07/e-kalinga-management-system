using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Login
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly MySqlAuthService _auth = new();

        [ObservableProperty] private string username = "";
        [ObservableProperty] private string password = "";

        [ObservableProperty] private string errorMessage = "";
        [ObservableProperty] private bool hasError = false;

        public event Action? LoginSucceeded;

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand(Login);
        }

        private void Login()
        {
            HasError = false;
            ErrorMessage = "";

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                HasError = true;
                ErrorMessage = "Please enter your username and password.";
                return;
            }

            var result = _auth.TryLogin(Username.Trim(), Password);

            if (!result.Success)
            {
                HasError = true;
                ErrorMessage = result.Message;
                return;
            }

            SessionService.Start(Username.Trim(), result.Role);
            LoginSucceeded?.Invoke();
        }
    }
}
