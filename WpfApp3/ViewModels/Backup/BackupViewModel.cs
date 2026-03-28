using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
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

        [ObservableProperty] private string historySearchText = "";
        [ObservableProperty] private int currentPage = 1;

        [ObservableProperty] private bool isDeleteOpen;
        [ObservableProperty] private string deleteMessage = "";

        private DatabaseBackupRecord? _deleteTarget;

        public int PageSize { get; } = 6;

        public ObservableCollection<DatabaseBackupRecord> HistoryItems { get; } = new();
        public ObservableCollection<DatabaseBackupRecord> PagedHistoryItems { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public BackupViewModel()
        {
            LoadConnectionInfo();
            CurrentUserRole = SessionService.Role ?? "Unknown";

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = InitializeAsync();
        }

        public bool IsSuperadmin =>
            string.Equals(SessionService.Role, "Superadmin", StringComparison.OrdinalIgnoreCase);

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} backup records";

        private async Task InitializeAsync()
        {
            await LoadHistoryAsync();
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

        partial void OnHistorySearchTextChanged(string value)
        {
            CurrentPage = 1;
            ApplyHistory();
        }

        partial void OnCurrentPageChanged(int value)
        {
            ApplyHistory();
        }

        private async Task LoadHistoryAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                var records = await Task.Run(() =>
                {
                    _backupService.EnsureBackupTable();
                    return _backupService.GetBackupHistory();
                });

                HistoryItems.Clear();
                foreach (var item in records)
                    HistoryItems.Add(item);

                CurrentPage = 1;
                ApplyHistory();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load backup history: {ex.Message}";
                StatusBrush = "#E11D48";

                HistoryItems.Clear();
                ApplyHistory();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private System.Collections.Generic.List<DatabaseBackupRecord> Filtered()
        {
            var q = (HistorySearchText ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(q))
                return HistoryItems.ToList();

            return HistoryItems.Where(x =>
                    (x.FileName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.DatabaseName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.ServerName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.CreatedBy ?? "").ToLowerInvariant().Contains(q) ||
                    x.CreatedAtText.ToLowerInvariant().Contains(q) ||
                    x.Id.ToString().Contains(q))
                .ToList();
        }

        private void ApplyHistory()
        {
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            PagedHistoryItems.Clear();
            foreach (var item in Filtered()
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize))
            {
                PagedHistoryItems.Add(item);
            }

            PageNumbers.Clear();
            for (int i = 1; i <= TotalPages; i++)
                PageNumbers.Add(i);

            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(FoundText));
        }

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

            var createdSuccessfully = false;

            IsLoading = true;
            StatusMessage = "Creating backup...";
            StatusBrush = "#2E3A59";

            try
            {
                var record = await Task.Run(() =>
                    _backupService.CreateAndStoreBackup(SessionService.Username ?? "Unknown"));

                StatusMessage = $"Backup created successfully: {record.FileName}";
                StatusBrush = "#16A34A";
                createdSuccessfully = true;
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

            if (createdSuccessfully)
                await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task DownloadBackupAsync(DatabaseBackupRecord? row)
        {
            if (row is null || IsLoading)
                return;

            var suggestedFileName = row.FileName.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
    ? row.FileName.Substring(0, row.FileName.Length - 3)
    : row.FileName;

            var dialog = new SaveFileDialog
            {
                Title = "Download Backup File",
                Filter = "SQL Backup (*.sql)|*.sql",
                FileName = suggestedFileName,
                DefaultExt = ".sql",
                AddExtension = true
            };

            if (dialog.ShowDialog() != true)
                return;

            IsLoading = true;
            StatusMessage = "Downloading backup...";
            StatusBrush = "#2E3A59";

            try
            {
                await Task.Run(() => _backupService.DownloadBackup(row.Id, dialog.FileName));
                StatusMessage = $"Backup downloaded successfully: {row.FileName}";
                StatusBrush = "#16A34A";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download failed: {ex.Message}";
                StatusBrush = "#E11D48";
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void OpenDeleteBackup(DatabaseBackupRecord? row)
        {
            if (row is null)
                return;

            _deleteTarget = row;
            DeleteMessage = $"Delete backup file {row.FileName}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void CancelDeleteBackup()
        {
            IsDeleteOpen = false;
            _deleteTarget = null;
            DeleteMessage = "";
        }

        [RelayCommand]
        private async Task ConfirmDeleteBackupAsync()
        {
            if (_deleteTarget is null || IsLoading)
                return;

            IsLoading = true;
            try
            {
                var fileName = _deleteTarget.FileName;
                await Task.Run(() => _backupService.DeleteBackup(_deleteTarget.Id));

                IsDeleteOpen = false;
                _deleteTarget = null;
                DeleteMessage = "";

                StatusMessage = $"Backup deleted successfully: {fileName}";
                StatusBrush = "#16A34A";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
                StatusBrush = "#E11D48";
            }
            finally
            {
                IsLoading = false;
            }

            await LoadHistoryAsync();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadHistoryAsync();
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
                CurrentPage--;
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
                CurrentPage++;
        }

        [RelayCommand]
        private void GoToPage(int page)
        {
            CurrentPage = page;
        }
    }
}