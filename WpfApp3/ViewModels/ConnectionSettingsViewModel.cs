using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels
{
    public partial class ConnectionSettingsViewModel : ObservableObject
    {
        public event Action? ReLoginRequested;

        [ObservableProperty] private string mode = "Server";
        [ObservableProperty] private string host = "";
        [ObservableProperty] private string port = "3306";
        [ObservableProperty] private string database = "";
        [ObservableProperty] private string username = "";
        [ObservableProperty] private string password = "";
        [ObservableProperty] private bool useSsl = true;

        [ObservableProperty] private bool isTesting;
        [ObservableProperty] private string testResultMessage = "";
        [ObservableProperty] private string testResultBrush = "#64748B";

        public ConnectionSettingsViewModel()
        {
            LoadSavedSettings();
        }

        public bool IsLocal => Mode == "Local";
        public bool IsServer => Mode == "Server";
        public string ConnectionTypeLabel => IsServer ? "Server Connection" : "Local Connection";

        partial void OnModeChanged(string value)
        {
            OnPropertyChanged(nameof(IsLocal));
            OnPropertyChanged(nameof(IsServer));
            OnPropertyChanged(nameof(ConnectionTypeLabel));

            if (value == "Local")
                ApplyLocalDefaults();
            else
                ApplyServerDefaults();
        }

        private void ApplyLocalDefaults()
        {
            Host = "127.0.0.1";
            Port = "3306";
            Database = "ekalinga_db";
            Username = "root";
            Password = "password123";
            UseSsl = false;
        }

        private void ApplyServerDefaults()
        {
            Host = "srv1237.hstgr.io";
            Port = "3306";
            Database = "u621755393_CDestributions";
            Username = "u621755393_destribution";
            Password = "Dssc@2026";
            UseSsl = true;
        }

        private void LoadSavedSettings()
        {
            try
            {
                var s = ConnectionSettingsService.Load();

                if (string.IsNullOrWhiteSpace(s.Host) ||
                    string.IsNullOrWhiteSpace(s.Database) ||
                    string.IsNullOrWhiteSpace(s.Username))
                {
                    Mode = "Server";
                    ApplyServerDefaults();
                    return;
                }

                Mode = string.IsNullOrWhiteSpace(s.Mode) ? "Server" : s.Mode;
                Host = s.Host;
                Port = string.IsNullOrWhiteSpace(s.Port) ? "3306" : s.Port;
                Database = s.Database;
                Username = s.Username;
                Password = s.Password;
                UseSsl = s.UseSsl;

                OnPropertyChanged(nameof(IsLocal));
                OnPropertyChanged(nameof(IsServer));
                OnPropertyChanged(nameof(ConnectionTypeLabel));
            }
            catch
            {
                Mode = "Server";
                ApplyServerDefaults();
                OnPropertyChanged(nameof(IsLocal));
                OnPropertyChanged(nameof(IsServer));
                OnPropertyChanged(nameof(ConnectionTypeLabel));
            }
        }

        [RelayCommand]
        private async Task TestConnectionAsync()
        {
            IsTesting = true;
            TestResultMessage = "Testing connection...";
            TestResultBrush = "#2E3A59";

            var settings = new ConnectionSettings
            {
                Mode = Mode,
                Host = Host?.Trim() ?? "",
                Port = Port?.Trim() ?? "3306",
                Database = Database?.Trim() ?? "",
                Username = Username?.Trim() ?? "",
                Password = Password ?? "",
                UseSsl = UseSsl
            };

            var result = await DbConnectionTester.TestAsync(settings);

            TestResultMessage = result.message;
            TestResultBrush = result.ok ? "#16A34A" : "#DC2626";
            IsTesting = false;
        }

        [RelayCommand]
        private void Save()
        {
            var settings = new ConnectionSettings
            {
                Mode = Mode,
                Host = Host?.Trim() ?? "",
                Port = Port?.Trim() ?? "3306",
                Database = Database?.Trim() ?? "",
                Username = Username?.Trim() ?? "",
                Password = Password ?? "",
                UseSsl = UseSsl
            };

            ConnectionSettingsService.Save(settings);

            TestResultMessage = "Connection settings saved successfully.";
            TestResultBrush = "#16A34A";

            var result = MessageBox.Show(
                "Connection settings were saved successfully.\n\nYou need to log in again to use the selected connection.\n\nLog out now?",
                "Connection Updated",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                ReLoginRequested?.Invoke();
            }
        }
    }
}