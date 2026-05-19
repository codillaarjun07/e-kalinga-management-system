using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Windows.Threading;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Dashboard
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DispatcherTimer _clockTimer;

        [ObservableProperty] private string currentUserFullName = "User";
        [ObservableProperty] private string greetingText = "Welcome back, User!";
        [ObservableProperty] private string dateTimeText = "";

        public DashboardViewModel()
        {
            LoadCurrentUserFullName();
            UpdateClock();

            _clockTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _clockTimer.Tick += (_, _) => UpdateClock();
            _clockTimer.Start();
        }

        partial void OnCurrentUserFullNameChanged(string value) => UpdateClock();

        private void LoadCurrentUserFullName()
        {
            try
            {
                var repo = new UsersRepository();
                var username = SessionService.Username;

                var user = repo.GetAll()
                    .FirstOrDefault(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));

                if (user is null)
                {
                    CurrentUserFullName = string.IsNullOrWhiteSpace(username) ? "User" : username.Trim();
                    return;
                }

                var fullName = $"{user.FirstName} {user.LastName}".Trim();
                CurrentUserFullName = string.IsNullOrWhiteSpace(fullName)
                    ? user.Username ?? "User"
                    : fullName;
            }
            catch
            {
                CurrentUserFullName = "User";
            }
        }

        private void UpdateClock()
        {
            var now = DateTime.Now;
            var name = string.IsNullOrWhiteSpace(CurrentUserFullName)
                ? "User"
                : CurrentUserFullName.Trim();

            var greeting = now.Hour switch
            {
                >= 5 and < 12 => "Good morning",
                >= 12 and < 18 => "Good afternoon",
                _ => "Good evening"
            };

            GreetingText = $"{greeting}, {name}!";
            DateTimeText = now.ToString("dddd, MMMM dd, yyyy • hh:mm:ss tt");
        }

        [RelayCommand]
        private void RefreshUser()
        {
            LoadCurrentUserFullName();
            UpdateClock();
        }
    }
}
