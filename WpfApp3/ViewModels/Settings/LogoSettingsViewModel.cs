using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Settings
{
    public partial class LogoSettingsViewModel : ObservableObject
    {
        private readonly LogosRepository _repo = new();
        private readonly System.Collections.Generic.List<LogoRecord> _all = new();

        private const long MaxFileSizeBytes = 10 * 1024 * 1024;

        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string errorMessage = "";
        [ObservableProperty] private string successMessage = "";

        [ObservableProperty] private LogoRecord? selectedLogo;
        [ObservableProperty] private string currentLogoName = "No active logo selected.";

        public ObservableCollection<LogoRecord> Items { get; } = new();

        public string FoundText => $"Found {Items.Count} logos";

        public LogoSettingsViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                return;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadAsync();
        }

        partial void OnSearchTextChanged(string value) => Apply();

        private async Task LoadAsync()
        {
            ErrorMessage = "";
            SuccessMessage = "";

            var list = await Task.Run(() =>
            {
                _repo.EnsureTable();
                return _repo.GetAll();
            });

            _all.Clear();
            _all.AddRange(list);

            var active = _all.FirstOrDefault(x => x.IsActive);
            CurrentLogoName = active is null ? "No active logo selected." : active.Name;

            Apply();
        }

        private void Apply()
        {
            Items.Clear();

            var q = (SearchText ?? "").Trim().ToLowerInvariant();

            var filtered = string.IsNullOrWhiteSpace(q)
                ? _all
                : _all.Where(x =>
                    (x.Name ?? "").ToLowerInvariant().Contains(q) ||
                    (x.FileName ?? "").ToLowerInvariant().Contains(q));

            foreach (var item in filtered.OrderByDescending(x => x.IsActive).ThenByDescending(x => x.CreatedAt))
                Items.Add(item);

            OnPropertyChanged(nameof(FoundText));
        }

        [RelayCommand]
        private async Task UploadLogo()
        {
            if (IsLoading) return;

            ErrorMessage = "";
            SuccessMessage = "";

            var dialog = new OpenFileDialog
            {
                Title = "Select logo",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
                return;

            var ext = Path.GetExtension(dialog.FileName)?.ToLowerInvariant() ?? "";
            var allowed = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };

            if (!allowed.Contains(ext))
            {
                ErrorMessage = "Only image files are allowed.";
                return;
            }

            var fileInfo = new FileInfo(dialog.FileName);
            if (!fileInfo.Exists)
            {
                ErrorMessage = "Selected file does not exist.";
                return;
            }

            if (fileInfo.Length > MaxFileSizeBytes)
            {
                ErrorMessage = "Image file must not be more than 10 MB.";
                return;
            }

            var bytes = await File.ReadAllBytesAsync(dialog.FileName);

            var record = new LogoRecord
            {
                Name = Path.GetFileNameWithoutExtension(dialog.FileName),
                FileName = Path.GetFileName(dialog.FileName),
                ContentType = GetContentType(ext),
                FileSizeBytes = fileInfo.Length,
                ImageData = bytes,
                IsActive = !_all.Any(x => x.IsActive)
            };

            IsLoading = true;
            try
            {
                await Task.Run(() =>
                {
                    _repo.EnsureTable();
                    _repo.Insert(record);
                });

                await LoadAsync();
                SuccessMessage = "Logo uploaded successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task SetAsActive(LogoRecord? item)
        {
            if (item is null || IsLoading) return;

            ErrorMessage = "";
            SuccessMessage = "";

            if (item.IsActive)
            {
                MessageBox.Show(
                    "This logo is already the one currently in use.",
                    "Use Logo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            var confirm = MessageBox.Show(
                $"Use '{item.Name}' as the current logo?",
                "Use Logo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsLoading = true;

            try
            {
                await Task.Run(() => _repo.SetActive(item.Id));
                await LoadAsync();

                SuccessMessage = $"'{item.Name}' is now the active logo.";

                MessageBox.Show(
                    $"'{item.Name}' is now the active logo.\n\nPlease logout and log back in to see the logo replaced across the app.",
                    "Logo Updated",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Use Logo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task DeleteLogo(LogoRecord? item)
        {
            if (item is null || IsLoading) return;

            ErrorMessage = "";
            SuccessMessage = "";

            if (_all.Count <= 1)
            {
                var message = "You cannot delete the only logo left.";
                ErrorMessage = message;

                MessageBox.Show(
                    message,
                    "Delete Logo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            if (item.IsActive)
            {
                var message = "You cannot delete the logo that is currently in use.";
                ErrorMessage = message;

                MessageBox.Show(
                    message,
                    "Delete Logo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            var confirm = MessageBox.Show(
                $"Delete logo '{item.Name}'?",
                "Delete Logo",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            IsLoading = true;

            try
            {
                await Task.Run(() => _repo.Delete(item.Id));
                await LoadAsync();
                SuccessMessage = "Logo deleted successfully.";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;

                MessageBox.Show(
                    ex.Message,
                    "Delete Logo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static string GetContentType(string ext) => ext switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }
}