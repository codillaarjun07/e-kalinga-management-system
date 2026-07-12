using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;using System.Globalization;
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
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private bool? isAllSelected = false;

        // USERS REGISTRY REVAMP STATE
        [ObservableProperty] private string selectedOfficeFilter = "All";
        [ObservableProperty] private string selectedRoleFilter = "All";

        public ObservableCollection<string> OfficeFilterOptions { get; } = new()
        {
            "All"
        };

        public ObservableCollection<string> RoleFilterOptions { get; } = new()
        {
            "All"
        };

        public int TotalUsers => _all.Count;

        public int SuperAdminCount =>
            _all.Count(x => string.Equals(
                x.Role,
                "Superadmin",
                StringComparison.OrdinalIgnoreCase));

        public int AdminCount =>
            _all.Count(x => string.Equals(
                x.Role,
                "Admin",
                StringComparison.OrdinalIgnoreCase));

        public int OfficeCount =>
            _all
                .Select(x => (x.Office ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
        // END USERS REGISTRY REVAMP STATE
        [ObservableProperty] private bool isToastVisible;
        [ObservableProperty] private string toastMessage = "";
        [ObservableProperty] private string toastBackground = "#2E3A59";

        public int PageSize { get; } = 8;

        public ObservableCollection<UserRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        public int SelectedCount =>
            _all.Count(x => x.IsSelected && !x.IsCurrentSessionUser);

        public bool HasSelectedUsers => SelectedCount > 0;

        public string DeleteSelectedText =>
            $"Delete Selected ({SelectedCount})";

        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isDeleteOpen;
        [ObservableProperty] private string formTitle = "Add User";

        private UserRecord? _editingTarget;
        private UserRecord? _deleteTarget;
        private readonly List<UserRecord> _deleteTargets = new();
        private bool _syncingSelection;

        [ObservableProperty] private string deleteTitle = "Delete User";
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
            _ = InitializeAsync();
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

        private async Task InitializeAsync()
        {
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;

                var departments = await Task.Run(() => _repo.GetDepartments().ToList());
                var roles = await Task.Run(() => _repo.GetRoles().ToList());
                var rows = await Task.Run(() => _repo.GetAll().ToList());

                Offices.Clear();
                foreach (var office in departments)
                    Offices.Add(office);

                Roles.Clear();
                foreach (var role in roles)
                    Roles.Add(role);

                foreach (var existing in _all)
                    existing.PropertyChanged -= UserRecord_PropertyChanged;

                _all.Clear();

                foreach (var r in rows)
                {
                    var record = new UserRecord
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
                    };

                    record.PropertyChanged += UserRecord_PropertyChanged;
                    _all.Add(record);
                }

                RefreshUserFilterOptions();
                NotifyUserRegistrySummary();
                CurrentPage = 1;
                Apply();
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnIsAllSelectedChanged(bool? value)
        {
            if (_syncingSelection || value is null)
                return;

            var eligibleUsers = Filtered()
                .Where(x => !x.IsCurrentSessionUser)
                .ToList();

            _syncingSelection = true;

            try
            {
                foreach (var user in eligibleUsers)
                    user.IsSelected = value.Value;
            }
            finally
            {
                _syncingSelection = false;
            }

            UpdateSelectionState();
        }

        private void UserRecord_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(UserRecord.IsSelected))
                return;

            if (sender is UserRecord user &&
                user.IsCurrentSessionUser &&
                user.IsSelected)
            {
                _syncingSelection = true;

                try
                {
                    user.IsSelected = false;
                }
                finally
                {
                    _syncingSelection = false;
                }
            }

            if (!_syncingSelection)
                UpdateSelectionState();
        }

        private void UpdateSelectionState()
        {
            var eligibleUsers = Filtered()
                .Where(x => !x.IsCurrentSessionUser)
                .ToList();

            bool? nextValue;

            if (eligibleUsers.Count == 0)
            {
                nextValue = false;
            }
            else if (eligibleUsers.All(x => x.IsSelected))
            {
                nextValue = true;
            }
            else if (eligibleUsers.Any(x => x.IsSelected))
            {
                nextValue = null;
            }
            else
            {
                nextValue = false;
            }

            _syncingSelection = true;

            try
            {
                IsAllSelected = nextValue;
            }
            finally
            {
                _syncingSelection = false;
            }

            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelectedUsers));
            OnPropertyChanged(nameof(DeleteSelectedText));
        }

        partial void OnSearchTextChanged(string value)
        {
            CurrentPage = 1;
            Apply();
        }

        // USERS REGISTRY REVAMP FILTER HANDLERS
        partial void OnSelectedOfficeFilterChanged(string value)
        {
            CurrentPage = 1;
            Apply();
        }

        partial void OnSelectedRoleFilterChanged(string value)
        {
            CurrentPage = 1;
            Apply();
        }
        // END USERS REGISTRY REVAMP FILTER HANDLERS
        partial void OnCurrentPageChanged(int value)
        {
            Apply();
        }

        // USERS REGISTRY REVAMP HELPERS
        private void RefreshUserFilterOptions()
        {
            var offices = _all
                .Select(x => (x.Office ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            OfficeFilterOptions.Clear();
            OfficeFilterOptions.Add("All");

            foreach (var office in offices)
                OfficeFilterOptions.Add(office);

            var roles = _all
                .Select(x => (x.Role ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            RoleFilterOptions.Clear();
            RoleFilterOptions.Add("All");

            foreach (var role in roles)
                RoleFilterOptions.Add(role);

            if (!OfficeFilterOptions.Contains(SelectedOfficeFilter))
                SelectedOfficeFilter = "All";

            if (!RoleFilterOptions.Contains(SelectedRoleFilter))
                SelectedRoleFilter = "All";
        }

        private void NotifyUserRegistrySummary()
        {
            OnPropertyChanged(nameof(TotalUsers));
            OnPropertyChanged(nameof(SuperAdminCount));
            OnPropertyChanged(nameof(AdminCount));
            OnPropertyChanged(nameof(OfficeCount));
        }
        // END USERS REGISTRY REVAMP HELPERS
        private List<UserRecord> Filtered()
        {
            var queryText = (SearchText ?? "").Trim();

            IEnumerable<UserRecord> query = _all;

            if (!string.IsNullOrWhiteSpace(queryText))
            {
                query = query.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(
                        queryText,
                        StringComparison.OrdinalIgnoreCase) ||
                    (x.FirstName ?? "").Contains(
                        queryText,
                        StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(
                        queryText,
                        StringComparison.OrdinalIgnoreCase) ||
                    (x.Office ?? "").Contains(
                        queryText,
                        StringComparison.OrdinalIgnoreCase) ||
                    (x.Role ?? "").Contains(
                        queryText,
                        StringComparison.OrdinalIgnoreCase) ||
                    (x.Username ?? "").Contains(
                        queryText,
                        StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(
                SelectedOfficeFilter,
                "All",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(
                    x.Office,
                    SelectedOfficeFilter,
                    StringComparison.OrdinalIgnoreCase));
            }

            if (!string.Equals(
                SelectedRoleFilter,
                "All",
                StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => string.Equals(
                    x.Role,
                    SelectedRoleFilter,
                    StringComparison.OrdinalIgnoreCase));
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
            NotifyUserRegistrySummary();
            UpdateSelectionState();
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
        private async Task SaveForm()
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
                IsLoading = true;

                var ignoreId = _editingTarget?.Id;
                var usernameExists = await Task.Run(() => _repo.UsernameExists(user, ignoreId));
                if (usernameExists)
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

                    await Task.Run(() => _repo.Create(first, last, office, role, user, pass, ProfilePictureInput));
                }
                else
                {
                    var editingId = _editingTarget.Id;
                    await Task.Run(() => _repo.Update(
                        editingId,
                        first,
                        last,
                        office,
                        role,
                        user,
                        string.IsNullOrWhiteSpace(pass) ? null : pass,
                        ProfilePictureInput
                    ));
                }

                await RefreshDataAsync();
                IsFormOpen = false;
                ShowToast("User saved successfully.", "success");
            }
            catch
            {
                ShowToast("Failed to save user.", "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void DeleteSelected()
        {
            if (!EnsureSuperAdminOrToast("delete selected users"))
                return;

            var selectedUsers = _all
                .Where(x => x.IsSelected && !x.IsCurrentSessionUser)
                .ToList();

            if (selectedUsers.Count == 0)
            {
                ShowToast("Select at least one user to delete.", "warning");
                return;
            }

            _deleteTarget = null;
            _deleteTargets.Clear();
            _deleteTargets.AddRange(selectedUsers);

            DeleteTitle = "Delete Selected Users";
            DeleteMessage =
                $"Are you sure you want to delete {selectedUsers.Count} selected users? This action cannot be undone.";

            IsDeleteOpen = true;
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

            _deleteTargets.Clear();
            _deleteTarget = row;
            DeleteTitle = "Delete User";
            DeleteMessage = $"Are you sure you want to delete user, {row.Username}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleteOpen = false;
            _deleteTarget = null;
            _deleteTargets.Clear();
            DeleteTitle = "Delete User";
        }

        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (!EnsureSuperAdminOrToast("delete a user"))
                return;

            var targets = _deleteTargets.Count > 0
                ? _deleteTargets.ToList()
                : _deleteTarget is null
                    ? new List<UserRecord>()
                    : new List<UserRecord> { _deleteTarget };

            targets = targets
                .Where(x => !IsSelf(x))
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToList();

            if (targets.Count == 0)
            {
                ShowToast("Your logged-in account cannot be deleted.", "warning");
                IsDeleteOpen = false;
                _deleteTarget = null;
                _deleteTargets.Clear();
                DeleteTitle = "Delete User";
                return;
            }

            var deletedCount = targets.Count;

            try
            {
                IsLoading = true;

                var ids = targets
                    .Select(x => x.Id)
                    .ToList();

                await Task.Run(() =>
                {
                    foreach (var id in ids)
                        _repo.Delete(id);
                });

                await RefreshDataAsync();

                IsDeleteOpen = false;
                _deleteTarget = null;
                _deleteTargets.Clear();
                DeleteTitle = "Delete User";

                ShowToast(
                    deletedCount == 1
                        ? "User deleted successfully."
                        : $"{deletedCount} users deleted successfully.",
                    "success");
            }
            catch
            {
                ShowToast(
                    deletedCount == 1
                        ? "Failed to delete user."
                        : "Failed to delete all selected users.",
                    "error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await RefreshDataAsync();
        }

        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }
        [RelayCommand] private void GoToPage(int page) { CurrentPage = page; }

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
    }
}
