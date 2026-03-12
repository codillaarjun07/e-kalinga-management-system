using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Settings
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsRepository _repo = new();

        private readonly List<SettingOptionRecord> _allDepartments = new();
        private readonly List<SettingOptionRecord> _allFunds = new();
        private readonly List<SettingOptionRecord> _allClassifications = new();
        private readonly List<SettingOptionRecord> _allRoles = new();

        [ObservableProperty] private string selectedTab = "Departments";

        [ObservableProperty] private string departmentSearchText = "";
        [ObservableProperty] private string fundSearchText = "";
        [ObservableProperty] private string classificationSearchText = "";
        [ObservableProperty] private string roleSearchText = "";
        [ObservableProperty] private bool isLoading;

        public ObservableCollection<SettingOptionRecord> DepartmentItems { get; } = new();
        public ObservableCollection<SettingOptionRecord> FundItems { get; } = new();
        public ObservableCollection<SettingOptionRecord> ClassificationItems { get; } = new();
        public ObservableCollection<SettingOptionRecord> RoleItems { get; } = new();

        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isDeleteOpen;
        [ObservableProperty] private string formTitle = "Add Setting";
        [ObservableProperty] private string deleteMessage = "";

        [ObservableProperty] private string nameInput = "";
        [ObservableProperty] private bool isActiveInput = true;

        private SettingOptionRecord? _editingTarget;
        private SettingOptionRecord? _deleteTarget;

        private string _mode = "Departments";

        public string DepartmentFoundText => $"Found {FilteredDepartments().Count} records";
        public string FundFoundText => $"Found {FilteredFunds().Count} records";
        public string ClassificationFoundText => $"Found {FilteredClassifications().Count} records";
        public string RoleFoundText => $"Found {FilteredRoles().Count} records";

        public SettingsViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                ApplyAll();
                return;
            }

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                var data = await Task.Run(() => new
                {
                    Departments = _repo.GetAll("departments"),
                    Funds = _repo.GetAll("source_of_funds"),
                    Classifications = _repo.GetAll("classifications"),
                    Roles = _repo.GetAll("roles")
                });

                _allDepartments.Clear();
                _allFunds.Clear();
                _allClassifications.Clear();
                _allRoles.Clear();

                _allDepartments.AddRange(data.Departments);
                _allFunds.AddRange(data.Funds);
                _allClassifications.AddRange(data.Classifications);
                _allRoles.AddRange(data.Roles);

                ApplyAll();
            }
            finally
            {
                IsLoading = false;
            }
        }

        partial void OnDepartmentSearchTextChanged(string value) => ApplyDepartments();
        partial void OnFundSearchTextChanged(string value) => ApplyFunds();
        partial void OnClassificationSearchTextChanged(string value) => ApplyClassifications();
        partial void OnRoleSearchTextChanged(string value) => ApplyRoles();

        private List<SettingOptionRecord> FilteredDepartments()
        {
            return Filter(_allDepartments, DepartmentSearchText);
        }

        private List<SettingOptionRecord> FilteredFunds()
        {
            return Filter(_allFunds, FundSearchText);
        }

        private List<SettingOptionRecord> FilteredClassifications()
        {
            return Filter(_allClassifications, ClassificationSearchText);
        }

        private List<SettingOptionRecord> FilteredRoles()
        {
            return Filter(_allRoles, RoleSearchText);
        }

        private List<SettingOptionRecord> Filter(List<SettingOptionRecord> src, string q)
        {
            q = (q ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(q))
                return src.ToList();

            return src.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(q) ||
                    (x.Name ?? "").ToLowerInvariant().Contains(q))
                .ToList();
        }

        private void ApplyAll()
        {
            ApplyDepartments();
            ApplyFunds();
            ApplyClassifications();
            ApplyRoles();
        }

        private void ApplyDepartments()
        {
            DepartmentItems.Clear();
            foreach (var item in FilteredDepartments())
                DepartmentItems.Add(item);

            OnPropertyChanged(nameof(DepartmentFoundText));
        }

        private void ApplyFunds()
        {
            FundItems.Clear();
            foreach (var item in FilteredFunds())
                FundItems.Add(item);

            OnPropertyChanged(nameof(FundFoundText));
        }

        private void ApplyClassifications()
        {
            ClassificationItems.Clear();
            foreach (var item in FilteredClassifications())
                ClassificationItems.Add(item);

            OnPropertyChanged(nameof(ClassificationFoundText));
        }

        private void ApplyRoles()
        {
            RoleItems.Clear();
            foreach (var item in FilteredRoles())
                RoleItems.Add(item);

            OnPropertyChanged(nameof(RoleFoundText));
        }

        [RelayCommand]
        private void SelectTab(string? tab)
        {
            if (!string.IsNullOrWhiteSpace(tab))
                SelectedTab = tab;
        }

        [RelayCommand]
        private void AddDepartment()
        {
            _mode = "Departments";
            _editingTarget = null;
            FormTitle = "Add Department";
            NameInput = "";
            IsActiveInput = true;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void AddFund()
        {
            _mode = "Source of Fund";
            _editingTarget = null;
            FormTitle = "Add Source of Fund";
            NameInput = "";
            IsActiveInput = true;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void AddClassification()
        {
            _mode = "Classifications";
            _editingTarget = null;
            FormTitle = "Add Classification";
            NameInput = "";
            IsActiveInput = true;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void AddRole()
        {
            _mode = "Roles";
            _editingTarget = null;
            FormTitle = "Add Role";
            NameInput = "";
            IsActiveInput = true;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditDepartment(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Departments";
            _editingTarget = row;
            FormTitle = "Edit Department";
            NameInput = row.Name;
            IsActiveInput = row.IsActive;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditFund(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Source of Fund";
            _editingTarget = row;
            FormTitle = "Edit Source of Fund";
            NameInput = row.Name;
            IsActiveInput = row.IsActive;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditClassification(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Classifications";
            _editingTarget = row;
            FormTitle = "Edit Classification";
            NameInput = row.Name;
            IsActiveInput = row.IsActive;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void EditRole(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Roles";
            _editingTarget = row;
            FormTitle = "Edit Role";
            NameInput = row.Name;
            IsActiveInput = row.IsActive;
            IsFormOpen = true;
        }

        [RelayCommand]
        private void CloseForm()
        {
            IsFormOpen = false;
        }

        [RelayCommand]
        private async Task SaveForm()
        {
            var name = (NameInput ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name))
                return;

            var table = GetCurrentTableName();
            var ignoreId = _editingTarget?.Id;

            try
            {
                var exists = await Task.Run(() => _repo.NameExists(table, name, ignoreId));
                if (exists)
                    return;

                if (_editingTarget is null)
                {
                    await Task.Run(() => _repo.Create(table, name, IsActiveInput));
                }
                else
                {
                    var editingId = _editingTarget.Id;
                    await Task.Run(() => _repo.Update(table, editingId, name, IsActiveInput));
                }

                await RefreshAsync();
                IsFormOpen = false;
            }
            catch
            {
            }
        }

        [RelayCommand]
        private void DeleteDepartment(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Departments";
            _deleteTarget = row;
            DeleteMessage = $"Are you sure you want to delete department, {row.Name}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void DeleteFund(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Source of Fund";
            _deleteTarget = row;
            DeleteMessage = $"Are you sure you want to delete source of fund, {row.Name}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void DeleteClassification(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Classifications";
            _deleteTarget = row;
            DeleteMessage = $"Are you sure you want to delete classification, {row.Name}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void DeleteRole(SettingOptionRecord? row)
        {
            if (row is null) return;

            _mode = "Roles";
            _deleteTarget = row;
            DeleteMessage = $"Are you sure you want to delete role, {row.Name}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleteOpen = false;
            _deleteTarget = null;
        }

        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (_deleteTarget is null) return;

            try
            {
                var deleteId = _deleteTarget.Id;
                var table = GetCurrentTableName();
                await Task.Run(() => _repo.Delete(table, deleteId));
                await RefreshAsync();
            }
            catch
            {
            }

            _deleteTarget = null;
            IsDeleteOpen = false;
        }

        private string GetCurrentTableName()
        {
            return _mode switch
            {
                "Departments" => "departments",
                "Source of Fund" => "source_of_funds",
                "Classifications" => "classifications",
                "Roles" => "roles",
                _ => "departments"
            };
        }
    }
}