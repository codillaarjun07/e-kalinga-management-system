using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Users
{
    public partial class UsersViewModel : ObservableObject
    {
        private readonly UsersRepository _repo = new();
        private readonly List<UserRecord> _all = new();
        private CancellationTokenSource? _toastCts;

        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private int currentPage = 1;

        [ObservableProperty] private bool isToastVisible;
        [ObservableProperty] private string toastMessage = "";
        [ObservableProperty] private string toastBackground = "#2E3A59";

        public int PageSize { get; } = 8;

        public ObservableCollection<UserRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isDeleteOpen;
        [ObservableProperty] private string formTitle = "Add User";

        private UserRecord? _editingTarget;
        private UserRecord? _deleteTarget;

        [ObservableProperty] private string deleteMessage = "";

        [ObservableProperty] private string firstNameInput = "";
        [ObservableProperty] private string lastNameInput = "";
        [ObservableProperty] private string? officeInput;
        [ObservableProperty] private string? roleInput;
        [ObservableProperty] private string usernameInput = "";
        [ObservableProperty] private string passwordInput = "";

        [ObservableProperty] private byte[]? profilePictureInput;

        public bool HasProfileImage => ProfilePictureInput is { Length: > 0 };
        public ImageSource? ProfileImagePreview => CreateImage(ProfilePictureInput);

        public ObservableCollection<string> Offices { get; } = new();
        public ObservableCollection<string> Roles { get; } = new();

        public UsersViewModel()
        {
            LoadLookupOptions();
            LoadFromDb();
            Apply();
        }

        public bool IsSuperAdmin =>
            string.Equals(SessionService.Role, "superadmin", StringComparison.OrdinalIgnoreCase);

        partial void OnProfilePictureInputChanged(byte[]? value)
        {
            OnPropertyChanged(nameof(HasProfileImage));
            OnPropertyChanged(nameof(ProfileImagePreview));
        }

        private static ImageSource? CreateImage(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = ms;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }

        private bool IsSelf(UserRecord? row)
        {
            if (row is null) return false;

            return string.Equals(
                row.Username?.Trim(),
                SessionService.Username?.Trim(),
                StringComparison.OrdinalIgnoreCase);
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

        private bool EnsureSuperAdminOrToast(string actionText)
        {
            if (IsSuperAdmin) return true;

            ShowToast($"You cannot {actionText} because you are not superadmin.", "warning");
            return false;
        }

        private void LoadLookupOptions()
        {
            Offices.Clear();
            foreach (var office in _repo.GetDepartments())
                Offices.Add(office);

            Roles.Clear();
            foreach (var role in _repo.GetRoles())
                Roles.Add(role);
        }

        private void LoadFromDb()
        {
            _all.Clear();

            var rows = _repo.GetAll();
            foreach (var r in rows)
            {
                _all.Add(new UserRecord
                {
                    Id = r.Id,
                    FirstName = r.FirstName,
                    LastName = r.LastName,
                    Office = r.Office ?? "",
                    Role = r.Role,
                    Username = r.Username,
                    ProfilePicture = r.ProfilePicture,
                    IsCurrentSessionUser = string.Equals(
                        r.Username?.Trim(),
                        SessionService.Username?.Trim(),
                        StringComparison.OrdinalIgnoreCase)
                });
            }

            CurrentPage = 1;
        }

        partial void OnSearchTextChanged(string value)
        {
            CurrentPage = 1;
            Apply();
        }

        partial void OnCurrentPageChanged(int value)
        {
            Apply();
        }

        private List<UserRecord> Filtered()
        {
            var q = (SearchText ?? "").Trim().ToLowerInvariant();

            var query = _all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(q) ||
                    (x.FirstName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.LastName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Office ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Role ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Username ?? "").ToLowerInvariant().Contains(q));
            }

            return query
                .OrderByDescending(x => x.IsCurrentSessionUser)
                .ThenBy(x => x.Id)
                .ToList();
        }

        private void Apply()
        {
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            Items.Clear();
            foreach (var it in Filtered()
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize))
            {
                Items.Add(it);
            }

            PageNumbers.Clear();
            for (int i = 1; i <= TotalPages; i++)
                PageNumbers.Add(i);

            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(FoundText));
            OnPropertyChanged(nameof(IsSuperAdmin));
        }

        [RelayCommand]
        private void AddUser()
        {
            if (!EnsureSuperAdminOrToast("add a user"))
                return;

            _editingTarget = null;
            FormTitle = "Add User";

            FirstNameInput = "";
            LastNameInput = "";
            OfficeInput = Offices.FirstOrDefault();
            RoleInput = Roles.FirstOrDefault();
            UsernameInput = "";
            PasswordInput = "";
            ProfilePictureInput = null;

            IsFormOpen = true;
        }

        [RelayCommand]
        private void Edit(UserRecord? row)
        {
            if (!EnsureSuperAdminOrToast("edit a user"))
                return;

            if (row is null) return;

            _editingTarget = row;
            FormTitle = "Edit User";

            FirstNameInput = row.FirstName;
            LastNameInput = row.LastName;
            OfficeInput = row.Office;
            RoleInput = row.Role;
            UsernameInput = row.Username;
            PasswordInput = "";
            ProfilePictureInput = row.ProfilePicture is null ? null : row.ProfilePicture.ToArray();

            IsFormOpen = true;
        }

        [RelayCommand]
        private void UploadProfileImage()
        {
            if (!EnsureSuperAdminOrToast("upload or change a user photo"))
                return;

            var dialog = new OpenFileDialog
            {
                Title = "Select Profile Picture",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                ProfilePictureInput = File.ReadAllBytes(dialog.FileName);
            }
            catch
            {
                ShowToast("Failed to load the selected image.", "error");
            }
        }

        [RelayCommand]
        private void RemoveProfileImage()
        {
            if (!EnsureSuperAdminOrToast("remove a user photo"))
                return;

            ProfilePictureInput = null;
        }

        [RelayCommand]
        private void CloseForm() => IsFormOpen = false;

        [RelayCommand]
        private void SaveForm()
        {
            if (!EnsureSuperAdminOrToast("save user changes"))
                return;

            var first = (FirstNameInput ?? "").Trim();
            var last = (LastNameInput ?? "").Trim();
            var office = (OfficeInput ?? "").Trim();
            var role = (RoleInput ?? "").Trim();
            var user = (UsernameInput ?? "").Trim();
            var pass = (PasswordInput ?? "").Trim();

            if (string.IsNullOrWhiteSpace(first)) first = "First";
            if (string.IsNullOrWhiteSpace(last)) last = "Last";
            if (string.IsNullOrWhiteSpace(office)) office = Offices.FirstOrDefault() ?? "";
            if (string.IsNullOrWhiteSpace(role)) role = Roles.FirstOrDefault() ?? "";
            if (string.IsNullOrWhiteSpace(user)) user = "username";

            try
            {
                var ignoreId = _editingTarget?.Id;
                if (_repo.UsernameExists(user, ignoreId))
                {
                    ShowToast("Username already exists.", "warning");
                    return;
                }

                if (_editingTarget is null)
                {
                    if (string.IsNullOrWhiteSpace(pass))
                    {
                        ShowToast("Password is required for new users.", "warning");
                        return;
                    }

                    _repo.Create(first, last, office, role, user, pass, ProfilePictureInput);
                }
                else
                {
                    _repo.Update(
                        _editingTarget.Id,
                        first,
                        last,
                        office,
                        role,
                        user,
                        string.IsNullOrWhiteSpace(pass) ? null : pass,
                        ProfilePictureInput
                    );
                }

                LoadLookupOptions();
                LoadFromDb();
                Apply();
                IsFormOpen = false;
                ShowToast("User saved successfully.", "success");
            }
            catch
            {
                ShowToast("Failed to save user.", "error");
            }
        }

        [RelayCommand]
        private void Delete(UserRecord? row)
        {
            if (!EnsureSuperAdminOrToast("delete a user"))
                return;

            if (row is null) return;

            if (IsSelf(row))
            {
                ShowToast("You cannot delete your own logged-in account.", "warning");
                return;
            }

            _deleteTarget = row;
            DeleteMessage = $"Are you sure you want to delete user, {row.Username}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleteOpen = false;
            _deleteTarget = null;
        }

        [RelayCommand]
        private void ConfirmDelete()
        {
            if (!EnsureSuperAdminOrToast("delete a user"))
                return;

            if (_deleteTarget is not null && IsSelf(_deleteTarget))
            {
                ShowToast("You cannot delete your own logged-in account.", "warning");
                IsDeleteOpen = false;
                _deleteTarget = null;
                return;
            }

            try
            {
                if (_deleteTarget is not null)
                    _repo.Delete(_deleteTarget.Id);

                LoadLookupOptions();
                LoadFromDb();
                Apply();

                IsDeleteOpen = false;
                _deleteTarget = null;

                ShowToast("User deleted successfully.", "success");
            }
            catch
            {
                ShowToast("Failed to delete user.", "error");
            }
        }

        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }
        [RelayCommand] private void GoToPage(int page) { CurrentPage = page; }
    }
}