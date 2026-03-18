using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Backup
{
    public partial class BackupViewModel : ObservableObject
    {
        private readonly DatabaseBackupService _backupService = new();

        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string statusMessage = "";
        [ObservableProperty] private string statusBrush = "#64748B";

        [ObservableProperty] private string databaseName = "";
        [ObservableProperty] private string serverName = "";
        [ObservableProperty] private string currentUserRole = "";

        public BackupViewModel()
        {
            LoadConnectionInfo();
            CurrentUserRole = SessionService.Role ?? "Unknown";
        }

        private void LoadConnectionInfo()
        {
            try
            {
                var builder = new MySqlConnector.MySqlConnectionStringBuilder(MySqlDb.ConnectionString);
                DatabaseName = builder.Database ?? "";
                ServerName = builder.Server ?? "";
            }
            catch
            {
                DatabaseName = "";
                ServerName = "";
            }
        }

        public bool IsSuperadmin =>
            string.Equals(SessionService.Role, "Superadmin", StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        private async Task CreateBackupAsync()
        {
            if (IsLoading)
                return;

            if (!IsSuperadmin)
            {
                StatusMessage = "Access denied. Only Superadmin can create a backup.";
                StatusBrush = "#E11D48";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Save Database Backup",
                Filter = "SQL Backup (*.sql)|*.sql",
                FileName = $"ekalinga_backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql",
                DefaultExt = ".sql",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true)
                return;

            IsLoading = true;
            StatusMessage = "Creating backup...";
            StatusBrush = "#2E3A59";

            try
            {
                await Task.Run(() => _backupService.CreateBackup(dialog.FileName));

                var fileName = Path.GetFileName(dialog.FileName);
                StatusMessage = $"Backup created successfully: {fileName}";
                StatusBrush = "#16A34A";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Backup failed: {ex.Message}";
                StatusBrush = "#E11D48";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}