using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Login
{
    public partial class LoginViewModel : ObservableObject
    {
        private readonly MySqlAuthService _auth = new();
        private CancellationTokenSource? _toastCts;

        [ObservableProperty] private string username = "";
        [ObservableProperty] private string password = "";

        [ObservableProperty] private string errorMessage = "";
        [ObservableProperty] private bool hasError = false;

        [ObservableProperty] private bool isLoading = false;
        [ObservableProperty] private bool isToastVisible = false;
        [ObservableProperty] private string toastMessage = "";
        [ObservableProperty] private string toastBackground = "#2E3A59";

        public event Action? LoginSucceeded;

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            LoginCommand = new AsyncRelayCommand(LoginAsync, CanLogin);
        }

        private bool CanLogin() => !IsLoading;

        partial void OnIsLoadingChanged(bool value)
        {
            if (LoginCommand is AsyncRelayCommand asyncCommand)
                asyncCommand.NotifyCanExecuteChanged();
        }

        private async Task LoginAsync()
        {
            if (IsLoading)
                return;

            HasError = false;
            ErrorMessage = "";

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                HasError = true;
                ErrorMessage = "Please enter your username and password.";
                ShowToast(ErrorMessage, "warning");
                return;
            }

            IsLoading = true;

            try
            {
                var result = await Task.Run(() => _auth.TryLogin(Username.Trim(), Password));

                if (!result.Success)
                {
                    HasError = true;
                    ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                        ? "Login failed. Please try again."
                        : result.Message;

                    ShowToast(ErrorMessage, "error");
                    return;
                }

                SessionService.Start(Username.Trim(), result.Role);
                LoginSucceeded?.Invoke();
            }
            catch
            {
                HasError = true;
                ErrorMessage = "Unable to connect to the server. Please check your connection and try again.";
                ShowToast(ErrorMessage, "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void ShowToast(string msg, string kind)
        {
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;

            ToastMessage = msg;
            ToastBackground = kind switch
            {
                "success" => "#16A34A",
                "error" => "#E11D48",
                "warning" => "#F59E0B",
                _ => "#2E3A59"
            };

            IsToastVisible = true;

            try
            {
                await Task.Delay(2200, token);
                IsToastVisible = false;
            }
            catch
            {
            }
        }
    }
}
