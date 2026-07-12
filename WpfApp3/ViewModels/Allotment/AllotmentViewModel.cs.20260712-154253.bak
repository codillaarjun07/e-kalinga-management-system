using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Allotment
{
    public partial class AllotmentViewModel : ObservableObject
    {
        private readonly Lazy<AllotmentsRepository> _repo = new(() => new AllotmentsRepository());
        private AllotmentsRepository Repo => _repo.Value;

        private readonly Lazy<SettingsRepository> _settingsRepo = new(() => new SettingsRepository());
        private SettingsRepository SettingsRepo => _settingsRepo.Value;

        private readonly System.Collections.Generic.List<AllotmentRecord> _all = new();

        private const string DepartmentsTable = "departments";
        private const string SourcesOfFundTable = "source_of_funds";

        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private int currentPage = 1;
        [ObservableProperty] private bool isLoading;

        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isDeleteOpen;

        [ObservableProperty] private string formTitle = "Add Allotment";
        [ObservableProperty] private string deleteMessage = "";

        [ObservableProperty] private string projectNameInput = "";
        [ObservableProperty] private string companyInput = "";
        [ObservableProperty] private string? departmentInput;
        [ObservableProperty] private string? sourceOfFundInput;
        [ObservableProperty] private string beneficiariesInput = "";

        [ObservableProperty] private string? budgetTypeInput = "Money";
        [ObservableProperty] private string budgetAmountInput = "";
        [ObservableProperty] private string budgetQtyInput = "";
        [ObservableProperty] private string budgetUnitInput = "";

        [ObservableProperty] private bool canSave;

        [ObservableProperty] private string projectNameError = "";
        [ObservableProperty] private bool hasProjectNameError;

        [ObservableProperty] private string companyError = "";
        [ObservableProperty] private bool hasCompanyError;

        [ObservableProperty] private string departmentError = "";
        [ObservableProperty] private bool hasDepartmentError;

        [ObservableProperty] private string sourceOfFundError = "";
        [ObservableProperty] private bool hasSourceOfFundError;

        [ObservableProperty] private string beneficiariesError = "";
        [ObservableProperty] private bool hasBeneficiariesError;

        [ObservableProperty] private string budgetAmountError = "";
        [ObservableProperty] private bool hasBudgetAmountError;

        [ObservableProperty] private string budgetQtyError = "";
        [ObservableProperty] private bool hasBudgetQtyError;

        [ObservableProperty] private string budgetUnitError = "";
        [ObservableProperty] private bool hasBudgetUnitError;

        private int? _editingId;
        private int? _deleteId;

        public int PageSize { get; } = 8;

        public ObservableCollection<AllotmentRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();
        public ObservableCollection<string> Departments { get; } = new();
        public ObservableCollection<string> SourcesOfFund { get; } = new();

        public ObservableCollection<string> BudgetTypes { get; } = new()
        {
            "Money",
            "InKind"
        };

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        public AllotmentViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                ValidateForm();
                Apply();
                return;
            }

            ValidateForm();
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadAllotmentsAsync();
        }

        private async Task LoadAllotmentsAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                var result = await Task.Run(() =>
                {
                    Repo.EnsureTable();

                    var departments = SettingsRepo.GetAll(DepartmentsTable)
                        .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Name))
                        .Select(x => x.Name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();

                    var sources = SettingsRepo.GetAll(SourcesOfFundTable)
                        .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Name))
                        .Select(x => x.Name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();

                    var records = Repo.GetAll();

                    return new
                    {
                        Departments = departments,
                        Sources = sources,
                        Records = records
                    };
                });

                Departments.Clear();
                foreach (var item in result.Departments)
                    Departments.Add(item);

                SourcesOfFund.Clear();
                foreach (var item in result.Sources)
                    SourcesOfFund.Add(item);

                _all.Clear();
                _all.AddRange(result.Records);

                CurrentPage = 1;
                Apply();
            }
            catch
            {
                Departments.Clear();
                SourcesOfFund.Clear();
                _all.Clear();
                Apply();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadSettingsOptions()
        {
            Departments.Clear();
            SourcesOfFund.Clear();

            var departmentOptions = SettingsRepo.GetAll(DepartmentsTable)
                .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            foreach (var item in departmentOptions)
                Departments.Add(item);

            var sourceOfFundOptions = SettingsRepo.GetAll(SourcesOfFundTable)
                .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            foreach (var item in sourceOfFundOptions)
                SourcesOfFund.Add(item);
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

        partial void OnProjectNameInputChanged(string value) => ValidateForm();
        partial void OnCompanyInputChanged(string value) => ValidateForm();
        partial void OnDepartmentInputChanged(string? value) => ValidateForm();
        partial void OnSourceOfFundInputChanged(string? value) => ValidateForm();
        partial void OnBeneficiariesInputChanged(string value) => ValidateForm();
        partial void OnBudgetTypeInputChanged(string? value) => ValidateForm();
        partial void OnBudgetAmountInputChanged(string value) => ValidateForm();
        partial void OnBudgetQtyInputChanged(string value) => ValidateForm();
        partial void OnBudgetUnitInputChanged(string value) => ValidateForm();

        private System.Collections.Generic.List<AllotmentRecord> Filtered()
        {
            var q = (SearchText ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(q))
                return _all.ToList();

            return _all.Where(x =>
                    (x.ProjectName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Company ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Department ?? "").ToLowerInvariant().Contains(q) ||
                    (x.SourceOfFund ?? "").ToLowerInvariant().Contains(q) ||
                    (x.BudgetDisplay ?? "").ToLowerInvariant().Contains(q) ||
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(q))
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
        }

        private void ValidateForm()
        {
            ProjectNameError = ""; HasProjectNameError = false;
            CompanyError = ""; HasCompanyError = false;
            DepartmentError = ""; HasDepartmentError = false;
            SourceOfFundError = ""; HasSourceOfFundError = false;
            BeneficiariesError = ""; HasBeneficiariesError = false;
            BudgetAmountError = ""; HasBudgetAmountError = false;
            BudgetQtyError = ""; HasBudgetQtyError = false;
            BudgetUnitError = ""; HasBudgetUnitError = false;

            if (string.IsNullOrWhiteSpace(ProjectNameInput))
            {
                ProjectNameError = "Project name is required.";
                HasProjectNameError = true;
            }

            if (string.IsNullOrWhiteSpace(CompanyInput))
            {
                CompanyError = "Company is required.";
                HasCompanyError = true;
            }

            if (string.IsNullOrWhiteSpace(DepartmentInput))
            {
                DepartmentError = "Department is required.";
                HasDepartmentError = true;
            }

            if (string.IsNullOrWhiteSpace(SourceOfFundInput))
            {
                SourceOfFundError = "Source of fund is required.";
                HasSourceOfFundError = true;
            }

            if (!int.TryParse((BeneficiariesInput ?? "").Trim(), out var ben) || ben <= 0)
            {
                BeneficiariesError = "No. of beneficiaries must be a valid number (> 0).";
                HasBeneficiariesError = true;
            }

            var type = (BudgetTypeInput ?? "Money").Trim();
            type = type.Equals("InKind", StringComparison.OrdinalIgnoreCase) ? "InKind" : "Money";

            if (type == "Money")
            {
                var raw = (BudgetAmountInput ?? "").Replace(",", "").Trim();
                if (string.IsNullOrWhiteSpace(raw) ||
                    !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0m)
                {
                    BudgetAmountError = "Budget amount must be a valid number (> 0).";
                    HasBudgetAmountError = true;
                }
            }
            else
            {
                if (!int.TryParse((BudgetQtyInput ?? "").Trim(), out var qty) || qty <= 0)
                {
                    BudgetQtyError = "Quantity must be a valid number (> 0).";
                    HasBudgetQtyError = true;
                }

                if (string.IsNullOrWhiteSpace(BudgetUnitInput))
                {
                    BudgetUnitError = "Unit/Item is required (ex: sacks of rice).";
                    HasBudgetUnitError = true;
                }
            }

            CanSave =
                !(HasProjectNameError || HasCompanyError || HasDepartmentError || HasSourceOfFundError ||
                  HasBeneficiariesError || HasBudgetAmountError || HasBudgetQtyError || HasBudgetUnitError);
        }

        [RelayCommand]
        private void AddAllotment()
        {
            LoadSettingsOptions();

            _editingId = null;
            FormTitle = "Add Allotment";

            ProjectNameInput = "";
            CompanyInput = "";
            DepartmentInput = null;
            SourceOfFundInput = null;
            BeneficiariesInput = "";

            BudgetTypeInput = "Money";
            BudgetAmountInput = "";
            BudgetQtyInput = "";
            BudgetUnitInput = "";

            ValidateForm();
            IsFormOpen = true;
        }

        [RelayCommand]
        private void Edit(AllotmentRecord? row)
        {
            if (row is null)
                return;

            LoadSettingsOptions();

            _editingId = row.Id;
            FormTitle = "Edit Allotment";

            ProjectNameInput = row.ProjectName;
            CompanyInput = row.Company;
            DepartmentInput = row.Department;
            SourceOfFundInput = row.SourceOfFund;
            BeneficiariesInput = row.BeneficiariesCount.ToString(CultureInfo.InvariantCulture);

            BudgetTypeInput = string.Equals(row.BudgetType, "InKind", StringComparison.OrdinalIgnoreCase) ? "InKind" : "Money";
            BudgetAmountInput = (row.BudgetAmount ?? 0m).ToString("N2", CultureInfo.InvariantCulture);
            BudgetQtyInput = (row.BudgetQty ?? 0).ToString(CultureInfo.InvariantCulture);
            BudgetUnitInput = row.BudgetUnit ?? "";

            ValidateForm();
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
            ValidateForm();
            if (!CanSave)
                return;

            var ben = int.Parse((BeneficiariesInput ?? "0").Trim());

            var project = (ProjectNameInput ?? "").Trim();
            var company = (CompanyInput ?? "").Trim();
            var dept = (DepartmentInput ?? "").Trim();
            var source = (SourceOfFundInput ?? "").Trim();

            var type = (BudgetTypeInput ?? "Money").Trim();
            type = type.Equals("InKind", StringComparison.OrdinalIgnoreCase) ? "InKind" : "Money";

            decimal? amount = null;
            int? qty = null;
            var unit = "";

            if (type == "Money")
            {
                var raw = (BudgetAmountInput ?? "").Replace(",", "").Trim();
                decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var m);
                amount = m;
            }
            else
            {
                int.TryParse((BudgetQtyInput ?? "").Trim(), out var q);
                qty = q;
                unit = (BudgetUnitInput ?? "").Trim();
            }

            var rec = new AllotmentRecord
            {
                Id = _editingId ?? 0,
                ProjectName = project,
                Company = company,
                Department = dept,
                SourceOfFund = source,
                BeneficiariesCount = ben,
                BudgetType = type,
                BudgetAmount = amount,
                BudgetQty = qty,
                BudgetUnit = unit
            };

            IsLoading = true;
            try
            {
                await Task.Run(() =>
                {
                    if (_editingId is null)
                    {
                        var newId = Repo.Insert(rec);
                        rec.Id = newId;
                    }
                    else
                    {
                        Repo.Update(rec);
                    }
                });

                IsFormOpen = false;
            }
            finally
            {
                IsLoading = false;
            }

            await LoadAllotmentsAsync();
        }

        [RelayCommand]
        private void Delete(AllotmentRecord? row)
        {
            if (row is null)
                return;

            _deleteId = row.Id;
            DeleteMessage = $"Are you sure you want to delete allotment, {row.ProjectName}? This action cannot be undone.";
            IsDeleteOpen = true;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsDeleteOpen = false;
            _deleteId = null;
        }

        [RelayCommand]
        private async Task ConfirmDelete()
        {
            if (_deleteId is null)
                return;

            IsLoading = true;
            try
            {
                await Task.Run(() => Repo.Delete(_deleteId.Value));

                IsDeleteOpen = false;
                _deleteId = null;
            }
            finally
            {
                IsLoading = false;
            }

            await LoadAllotmentsAsync();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadAllotmentsAsync();
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