using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using WpfApp3.Models;
using WpfApp3.Services;
using System.ComponentModel;
using System.Windows;

namespace WpfApp3.ViewModels.Allotment
{
    public partial class AllotmentViewModel : ObservableObject
    {
        // ✅ LAZY repo so Designer won't crash trying to read EkalingaDb
        private readonly Lazy<AllotmentsRepository> _repo = new(() => new AllotmentsRepository());
        private AllotmentsRepository Repo => _repo.Value;

        private readonly System.Collections.Generic.List<AllotmentRecord> _all = new();

        // table/search/paging
        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private int currentPage = 1;

        public int PageSize { get; } = 8;

        public ObservableCollection<AllotmentRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        // ===== MODALS STATE =====
        [ObservableProperty] private bool isFormOpen;
        [ObservableProperty] private bool isDeleteOpen;

        [ObservableProperty] private string formTitle = "Add Allotment";

        private int? _editingId;
        private int? _deleteId;

        [ObservableProperty] private string deleteMessage = "";

        // ===== FORM FIELDS =====
        [ObservableProperty] private string projectNameInput = "";
        [ObservableProperty] private string companyInput = "";
        [ObservableProperty] private string? departmentInput;
        [ObservableProperty] private string? sourceOfFundInput;
        [ObservableProperty] private string beneficiariesInput = "";

        // NEW: budget inputs
        [ObservableProperty] private string? budgetTypeInput = "Money"; // Money | InKind
        [ObservableProperty] private string budgetAmountInput = "";     // money
        [ObservableProperty] private string budgetQtyInput = "";        // in-kind
        [ObservableProperty] private string budgetUnitInput = "";       // in-kind

        // ===== VALIDATION =====
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

        // dropdown options (dummy for now)
        public ObservableCollection<string> Departments { get; } = new()
        {
            "Operations", "Finance", "Health", "Admin"
        };

        public ObservableCollection<string> SourcesOfFund { get; } = new()
        {
            "LGU Admin", "Donation"
        };

        public ObservableCollection<string> BudgetTypes { get; } = new()
        {
            "Money", "InKind"
        };

        public AllotmentViewModel()
        {
            // ✅ IMPORTANT: do nothing in Visual Studio Designer
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                // keep UI working in designer without DB
                ValidateForm();
                Apply();
                return;
            }

            try
            {
                Repo.EnsureTable();
                ReloadFromDb();
            }
            catch
            {
                // If DB/config is not available, just show empty list (prevents crash)
                _all.Clear();
            }

            Apply();
            ValidateForm();
        }

        private void ReloadFromDb()
        {
            _all.Clear();
            _all.AddRange(Repo.GetAll());
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

        // validate on any form change
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
            for (int i = 1; i <= TotalPages; i++) PageNumbers.Add(i);

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
            { ProjectNameError = "Project name is required."; HasProjectNameError = true; }

            if (string.IsNullOrWhiteSpace(CompanyInput))
            { CompanyError = "Company is required."; HasCompanyError = true; }

            if (string.IsNullOrWhiteSpace(DepartmentInput))
            { DepartmentError = "Department is required."; HasDepartmentError = true; }

            if (string.IsNullOrWhiteSpace(SourceOfFundInput))
            { SourceOfFundError = "Source of fund is required."; HasSourceOfFundError = true; }

            if (!int.TryParse((BeneficiariesInput ?? "").Trim(), out var ben) || ben <= 0)
            { BeneficiariesError = "No. of beneficiaries must be a valid number (> 0)."; HasBeneficiariesError = true; }

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

        // ===== COMMANDS =====

        [RelayCommand]
        private void AddAllotment()
        {
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
            if (row is null) return;

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
        private void SaveForm()
        {
            ValidateForm();
            if (!CanSave) return;

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

            if (_editingId is null)
            {
                var newId = Repo.Insert(rec);
                rec.Id = newId;
            }
            else
            {
                Repo.Update(rec);
            }

            IsFormOpen = false;
            ReloadFromDb();
            Apply();
        }

        [RelayCommand]
        private void Delete(AllotmentRecord? row)
        {
            if (row is null) return;

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
        private void ConfirmDelete()
        {
            if (_deleteId is not null)
                Repo.Delete(_deleteId.Value);

            IsDeleteOpen = false;
            _deleteId = null;

            ReloadFromDb();
            Apply();
        }

        // paging
        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }
        [RelayCommand] private void GoToPage(int page) { CurrentPage = page; }
    }
}