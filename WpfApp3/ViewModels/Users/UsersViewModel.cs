using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Users
{
    public partial class UsersViewModel : ObservableObject
    {
        private readonly UsersRepository _repo = new();
        private readonly List<UserRecord> _all = new();

        // table/search/paging
        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private int currentPage = 1;

        public int PageSize { get; } = 8;

        public ObservableCollection<UserRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        // ===== MODALS =====
        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isDeleteOpen;
        [ObservableProperty] private string formTitle = "Add User";

        private UserRecord? _editingTarget;
        private UserRecord? _deleteTarget;

        [ObservableProperty] private string deleteMessage = "";

        // ===== FORM FIELDS =====
        [ObservableProperty] private string firstNameInput = "";
        [ObservableProperty] private string lastNameInput = "";
        [ObservableProperty] private string? officeInput;
        [ObservableProperty] private string? roleInput;
        [ObservableProperty] private string usernameInput = "";
        [ObservableProperty] private string passwordInput = ""; // only used for create OR reset on edit

        public ObservableCollection<string> Offices { get; } = new()
        {
            "Admin", "Finance", "Accounting", "Registrar"
        };

        public ObservableCollection<string> Roles { get; } = new()
        {
            "Administrator", "Admin", "User"
        };

        public UsersViewModel()
        {
            LoadFromDb();
            Apply();
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
                    Password = "********", // never load real password
                    IsPasswordRevealed = false
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
            if (string.IsNullOrWhiteSpace(q))
                return _all.ToList();

            return _all.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(q) ||
                    (x.FirstName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.LastName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Office ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Role ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Username ?? "").ToLowerInvariant().Contains(q))
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
            for (int i = 1; i <= TotalPages; i++) PageNumbers.Add(i);

            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(FoundText));
        }

        // ===== COMMANDS =====

        [RelayCommand]
        private void AddUser()
        {
            _editingTarget = null;
            FormTitle = "Add User";

            FirstNameInput = "";
            LastNameInput = "";
            OfficeInput = null;
            RoleInput = null;
            UsernameInput = "";
            PasswordInput = "";

            IsFormOpen = true;
        }

        [RelayCommand]
        private void Edit(UserRecord? row)
        {
            if (row is null) return;

            _editingTarget = row;
            FormTitle = "Edit User";

            FirstNameInput = row.FirstName;
            LastNameInput = row.LastName;
            OfficeInput = row.Office;
            RoleInput = row.Role;
            UsernameInput = row.Username;

            // IMPORTANT: don't fill password from DB (we don't have it)
            PasswordInput = "";

            IsFormOpen = true;
        }

        [RelayCommand] private void CloseForm() => IsFormOpen = false;

        [RelayCommand]
        private void SaveForm()
        {
            var first = (FirstNameInput ?? "").Trim();
            var last = (LastNameInput ?? "").Trim();
            var office = (OfficeInput ?? "").Trim();
            var role = (RoleInput ?? "").Trim();
            var user = (UsernameInput ?? "").Trim();
            var pass = (PasswordInput ?? "").Trim();

            if (string.IsNullOrWhiteSpace(first)) first = "First";
            if (string.IsNullOrWhiteSpace(last)) last = "Last";
            if (string.IsNullOrWhiteSpace(office)) office = "Admin";
            if (string.IsNullOrWhiteSpace(role)) role = "User";
            if (string.IsNullOrWhiteSpace(user)) user = "username";

            try
            {
                // Username uniqueness
                var ignoreId = _editingTarget?.Id;
                if (_repo.UsernameExists(user, ignoreId))
                {
                    // minimal handling: you can wire this to your UI error label later
                    return;
                }

                if (_editingTarget is null)
                {
                    // Creating requires a password
                    if (string.IsNullOrWhiteSpace(pass))
                    {
                        // minimal handling
                        return;
                    }

                    var newId = _repo.Create(first, last, office, role, user, pass);

                    // reload from db (keeps UI consistent)
                    LoadFromDb();
                }
                else
                {
                    // Updating: password optional (only if typed -> reset)
                    _repo.Update(_editingTarget.Id, first, last, office, role, user,
                        string.IsNullOrWhiteSpace(pass) ? null : pass);

                    LoadFromDb();
                }

                IsFormOpen = false;
                Apply();
            }
            catch
            {
                // You can set a VM error message property here if you already have one in the UI
                // For now we keep it silent to match your current implementation
            }
        }

        [RelayCommand]
        private void Delete(UserRecord? row)
        {
            if (row is null) return;

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
            try
            {
                if (_deleteTarget is not null)
                {
                    _repo.Delete(_deleteTarget.Id);
                    LoadFromDb();
                }
            }
            catch
            {
                // optional: show UI error
            }

            IsDeleteOpen = false;
            _deleteTarget = null;
            Apply();
        }

        [RelayCommand]
        private void ToggleReveal(UserRecord? row)
        {
            if (row is null) return;
            row.IsPasswordRevealed = !row.IsPasswordRevealed;
        }

        // paging
        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }
        [RelayCommand] private void GoToPage(int page) { CurrentPage = page; }
    }
}
