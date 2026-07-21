using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Beneficiaries
{
    public partial class BeneficiariesViewModel : ObservableObject
    {

        private readonly BeneficiariesRepository _beneficiariesRepo = new();

        [ObservableProperty] private bool isProfileOpen;

        [ObservableProperty] private string profileBeneficiaryId = "";
        [ObservableProperty] private string profileCivilRegistryId = "";
        [ObservableProperty] private string profileFullName = "";
        [ObservableProperty] private string profileGender = "";
        [ObservableProperty] private string profileClassification = "";
        [ObservableProperty] private string profileBarangay = "";
        [ObservableProperty] private string profilePresentAddress = "";
        [ObservableProperty] private string profileShareText = "";
        [ObservableProperty] private string profileReleasedText = "";
        [ObservableProperty] private string profileHistoryEmptyText = "No past releases found.";

        public ObservableCollection<BeneficiaryReleaseHistoryRow> ProfileHistory { get; } = new();

        private readonly AllotmentsRepository _allotmentRepo = new();
        private readonly AllotmentBeneficiariesRepository _assignRepo = new();

        private List<BeneficiaryRecord> _assignedCache = new();

        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private int currentPage = 1;
        [ObservableProperty] private bool isLoading;

        public int PageSize { get; } = 8;

        public ObservableCollection<AllotmentProjectOption> Projects { get; } = new();
        public ObservableCollection<AllotmentProjectOption> FilteredProjects { get; } = new();

        [ObservableProperty] private AllotmentProjectOption? selectedProject;
        [ObservableProperty] private string projectSearchText = "";
        [ObservableProperty] private bool isProjectDropdownOpen;

        private bool _syncingProjectSearch;

        public ObservableCollection<BeneficiaryRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        
        // BENEFICIARY REGISTRY REVAMP
        public int AssignedCount => _assignedCache.Count;
        public int ReleasedCount => _assignedCache.Count(x => x.IsReleased);
        public int WaitingCount => Math.Max(0, AssignedCount - ReleasedCount);
        public int ClassificationCount => _assignedCache
            .Select(x => string.IsNullOrWhiteSpace(x.Classification) ? "None" : x.Classification.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        public string BudgetValueText =>
            SelectedProject is null ? "₱ 0.00" : SelectedProject.TotalBudgetText;

        public string SelectedProjectSummary =>
            SelectedProject is null
                ? "Select an allotment to view assigned beneficiaries."
                : $"{SelectedProject.Department} • {SelectedProject.SourceOfFund}";

public string TotalBudgetText =>
            SelectedProject is null ? "Total Budget: ₱ 0.00" : $"Total Budget: {SelectedProject.TotalBudgetText}";

        public bool HasSelectedProject => SelectedProject is not null;

        public string AddBeneficiariesTitle =>
            SelectedProject is null
                ? "Add Beneficiaries"
                : $"Add Beneficiaries - {SelectedProject.ProjectName}";

        // ---------------- MODALS ----------------
        [ObservableProperty] private bool isProjectDetailsOpen;
        [ObservableProperty] private bool isAddBeneficiariesOpen;
        [ObservableProperty] private bool isEditShareOpen;
        [ObservableProperty] private bool isRemoveOpen;

        // Project details modal fields
        [ObservableProperty] private string projectNameDetails = "";
        [ObservableProperty] private string companyDetails = "";
        [ObservableProperty] private string departmentDetails = "";
        [ObservableProperty] private string sourceOfFundDetails = "";
        [ObservableProperty] private string totalBudgetDetails = "";

        // Add beneficiaries modal
        [ObservableProperty] private string addSearchText = "";
        public ObservableCollection<BeneficiaryRecord> AddItems { get; } = new();

        public int AddSelectedCount => AddItems.Count(x => x.IsSelected);
        public string AddButtonText => $"Add {AddSelectedCount}";
        public string AddFoundText => $"Found {AddItems.Count} records";

        [ObservableProperty] private bool isAddAllSelected;
        private bool _syncingAddSelectAll;

        // Edit share modal inputs + validation
        private BeneficiaryRecord? _editTarget;

        [ObservableProperty] private string shareAmountInput = "";
        [ObservableProperty] private string shareQtyInput = "";
        [ObservableProperty] private string shareUnitInput = "";

        [ObservableProperty] private string shareAmountError = "";
        [ObservableProperty] private bool hasShareAmountError;

        [ObservableProperty] private string shareInKindError = "";
        [ObservableProperty] private bool hasShareInKindError;

        // Remove modal
        private BeneficiaryRecord? _removeTarget;
        [ObservableProperty] private string removeMessage = "";

        private bool _ready;

        public ObservableCollection<string> ClassificationOptions { get; } = new();
        [ObservableProperty] private string? selectedClassification;

        public ObservableCollection<string> ReleaseStatusOptions { get; } = new()
        {
            "All",
            "Waiting",
            "Released"
        };

        [ObservableProperty] private string? selectedReleaseStatus = "All";


        // ===== Add modal paging =====
        [ObservableProperty] private int addCurrentPage = 1;
        public int AddPageSize { get; } = 8;

        public ObservableCollection<BeneficiaryRecord> AddPagedItems { get; } = new();
        public ObservableCollection<int> AddPageNumbers { get; } = new();


        public BeneficiariesViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                ClassificationOptions.Add("All");
                ClassificationOptions.Add("PWD");
                ClassificationOptions.Add("Senior Citizen");
                ClassificationOptions.Add("Indigenous");
                ClassificationOptions.Add("Farmer");
                ClassificationOptions.Add("Vendor");
                ClassificationOptions.Add("None");

                SelectedClassification = "All";
                _ready = true;
                Apply();
                return;
            }

            // classification filter options
            ClassificationOptions.Add("All");
            ClassificationOptions.Add("PWD");
            ClassificationOptions.Add("Senior Citizen");
            ClassificationOptions.Add("Indigenous");
            ClassificationOptions.Add("Farmer");
            ClassificationOptions.Add("Vendor");
            ClassificationOptions.Add("None");

            SelectedClassification = "All";
            _ready = true;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadDataAsync();
        }

        partial void OnSelectedClassificationChanged(string? value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            Apply();
        }

        
        partial void OnSelectedReleaseStatusChanged(string? value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            Apply();
        }

partial void OnSearchTextChanged(string value) { CurrentPage = 1; Apply(); }
        partial void OnCurrentPageChanged(int value) { Apply(); }

        partial void OnSelectedProjectChanged(AllotmentProjectOption? value)
        {
            SyncProjectSearchTextToSelection();
            OnPropertyChanged(nameof(HasSelectedProject));
            OnPropertyChanged(nameof(TotalBudgetText));
            OnPropertyChanged(nameof(AddBeneficiariesTitle));
            OnPropertyChanged(nameof(BudgetValueText));
            OnPropertyChanged(nameof(SelectedProjectSummary));

            if (value is null)
            {
                IsProjectDetailsOpen = false;
                IsAddBeneficiariesOpen = false;
                IsEditShareOpen = false;
                IsRemoveOpen = false;
                _assignedCache.Clear();
                AddItems.Clear();
                AddPagedItems.Clear();
                AddPageNumbers.Clear();
                Apply();
            }

            if (!_ready) return;
            CurrentPage = 1;
            _ = ReloadEverythingAsync();
        }

        partial void OnProjectSearchTextChanged(string value)
        {
            if (_syncingProjectSearch)
                return;

            ApplyProjectFilter(value);

            if (_ready)
                IsProjectDropdownOpen = true;
        }

        partial void OnAddSearchTextChanged(string value)
        {
            AddCurrentPage = 1;
            ApplyAddPaging();
        }

        partial void OnIsAddAllSelectedChanged(bool value)
        {
            if (_syncingAddSelectAll)
                return;

            var matchingRows = AddFiltered().ToList();

            _syncingAddSelectAll = true;
            try
            {
                foreach (var item in matchingRows)
                    item.IsSelected = value;
            }
            finally
            {
                _syncingAddSelectAll = false;
            }

            OnPropertyChanged(nameof(AddSelectedCount));
            OnPropertyChanged(nameof(AddButtonText));
        }

        private async Task RefreshAddListAsync()
        {
            await BuildAddListAsync();
            ApplyAddPaging();
        }

        private async Task LoadDataAsync()
        {
            if (IsLoading)
                return;

            var selectedProjectId = SelectedProject?.Id;
            IsLoading = true;

            try
            {
                var projects = await Task.Run(() => _allotmentRepo.GetAllProjects());

                Projects.Clear();
                foreach (var project in projects)
                    Projects.Add(project);

                ApplyProjectFilter("");

                AllotmentProjectOption? preservedSelection = null;
                if (selectedProjectId is not null)
                {
                    foreach (var project in Projects)
                    {
                        if (project.Id != selectedProjectId.Value)
                            continue;

                        preservedSelection = project;
                        break;
                    }
                }

                SelectedProject = preservedSelection;
                SyncProjectSearchTextToSelection();
                await ReloadEverythingCoreAsync();
            }
            catch
            {
                SelectedProject = null;
                Projects.Clear();
                FilteredProjects.Clear();
                _assignedCache.Clear();
                AddItems.Clear();
                AddPagedItems.Clear();
                AddPageNumbers.Clear();
                Apply();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReloadEverythingAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                await ReloadEverythingCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ReloadEverythingCoreAsync()
        {
            var selectedProjectId = SelectedProject?.Id;

            var assigned = selectedProjectId is null
                ? new List<BeneficiaryRecord>()
                : await Task.Run(() => _assignRepo.GetAssignedEndorsed(selectedProjectId.Value));

            _assignedCache = assigned;
            Apply();
            await BuildAddListAsync();

            OnPropertyChanged(nameof(TotalBudgetText));
            OnPropertyChanged(nameof(AddBeneficiariesTitle));
        }

        private List<BeneficiaryRecord> Filtered()
        {
            IEnumerable<BeneficiaryRecord> src = _assignedCache;

            var classification = (SelectedClassification ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(classification) &&
                !classification.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (classification.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    src = src.Where(x =>
                    {
                        var value = (x.Classification ?? "").Trim();
                        return string.IsNullOrWhiteSpace(value) ||
                               value.Equals("None", StringComparison.OrdinalIgnoreCase);
                    });
                }
                else
                {
                    src = src.Where(x =>
                        string.Equals(
                            (x.Classification ?? "").Trim(),
                            classification,
                            StringComparison.OrdinalIgnoreCase));
                }
            }

            var releaseStatus = (SelectedReleaseStatus ?? "All").Trim();
            if (releaseStatus.Equals("Released", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => x.IsReleased);
            else if (releaseStatus.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => !x.IsReleased);

            var query = (SearchText ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(query))
            {
                src = src.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(query) ||
                    (x.BeneficiaryId ?? "").ToLowerInvariant().Contains(query) ||
                    (x.FirstName ?? "").ToLowerInvariant().Contains(query) ||
                    (x.LastName ?? "").ToLowerInvariant().Contains(query) ||
                    (x.Barangay ?? "").ToLowerInvariant().Contains(query) ||
                    (x.Classification ?? "").ToLowerInvariant().Contains(query) ||
                    (x.Gender ?? "").ToLowerInvariant().Contains(query));
            }

            return src.ToList();
        }


        private void Apply()
        {
            var filtered = Filtered();
            var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));

            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > totalPages) CurrentPage = totalPages;

            Items.Clear();
            foreach (var item in filtered
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize))
            {
                Items.Add(item);
            }

            PageNumbers.Clear();
            for (var page = 1; page <= totalPages; page++)
                PageNumbers.Add(page);

            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(FoundText));
            NotifyRegistryState();
        }

        private void NotifyRegistryState()
        {
            OnPropertyChanged(nameof(AssignedCount));
            OnPropertyChanged(nameof(ReleasedCount));
            OnPropertyChanged(nameof(WaitingCount));
            OnPropertyChanged(nameof(ClassificationCount));
            OnPropertyChanged(nameof(BudgetValueText));
            OnPropertyChanged(nameof(SelectedProjectSummary));
        }


        private void ApplyProjectFilter(string? query)
        {
            var q = (query ?? "").Trim();

            var filtered = string.IsNullOrWhiteSpace(q)
                ? Projects.ToList()
                : Projects.Where(x =>
                    ContainsIgnoreCase(x.ProjectName, q) ||
                    ContainsIgnoreCase(x.Company, q) ||
                    ContainsIgnoreCase(x.Department, q) ||
                    ContainsIgnoreCase(x.SourceOfFund, q))
                    .ToList();

            FilteredProjects.Clear();
            foreach (var project in filtered)
                FilteredProjects.Add(project);
        }

        private static bool ContainsIgnoreCase(string? value, string query)
            => (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private void SyncProjectSearchTextToSelection()
        {
            _syncingProjectSearch = true;
            try
            {
                ProjectSearchText = SelectedProject?.ProjectName ?? "";
            }
            finally
            {
                _syncingProjectSearch = false;
            }

            ApplyProjectFilter("");
        }

        [RelayCommand]
        private void OpenProjectDropdown()
        {
            ApplyProjectFilter(ProjectSearchText);
            IsProjectDropdownOpen = true;
        }

        [RelayCommand]
        private void ShowAllProjectDropdown()
        {
            _syncingProjectSearch = true;
            try
            {
                ProjectSearchText = "";
            }
            finally
            {
                _syncingProjectSearch = false;
            }

            ApplyProjectFilter("");
            IsProjectDropdownOpen = true;
        }

        [RelayCommand]
        private void CloseProjectDropdown()
        {
            IsProjectDropdownOpen = false;
            SyncProjectSearchTextToSelection();
        }

        [RelayCommand]
        private void SelectProject(AllotmentProjectOption? project)
        {
            if (project is null)
                return;

            SelectedProject = project;
            IsProjectDropdownOpen = false;
            SyncProjectSearchTextToSelection();
        }

        // -------- Add Beneficiaries (modal) --------
        private async Task BuildAddListAsync()
        {
            AddItems.Clear();

            _syncingAddSelectAll = true;
            try
            {
                IsAddAllSelected = false;
            }
            finally
            {
                _syncingAddSelectAll = false;
            }

            if (SelectedProject is null)
            {
                AddPagedItems.Clear();
                AddPageNumbers.Clear();
                OnPropertyChanged(nameof(AddSelectedCount));
                OnPropertyChanged(nameof(AddButtonText));
                OnPropertyChanged(nameof(AddFoundText));
                return;
            }

            // Load the complete available set once. Search and paging are local so
            // selected rows remain selected while moving between pages or searches.
            var source = await Task.Run(() =>
                _assignRepo.GetAvailableEndorsedNotAssigned(SelectedProject.Id, ""));

            foreach (var row in source)
            {
                row.IsSelected = false;
                row.PropertyChanged -= AddRow_PropertyChanged;
                row.PropertyChanged += AddRow_PropertyChanged;
                AddItems.Add(row);
            }

            OnPropertyChanged(nameof(AddSelectedCount));
            OnPropertyChanged(nameof(AddButtonText));
            OnPropertyChanged(nameof(AddFoundText));
        }

        private void AddRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(BeneficiaryRecord.IsSelected))
                return;

            OnPropertyChanged(nameof(AddSelectedCount));
            OnPropertyChanged(nameof(AddButtonText));

            if (_syncingAddSelectAll)
                return;

            var matchingRows = AddFiltered().ToList();

            _syncingAddSelectAll = true;
            try
            {
                IsAddAllSelected =
                    matchingRows.Count > 0 &&
                    matchingRows.All(item => item.IsSelected);
            }
            finally
            {
                _syncingAddSelectAll = false;
            }
        }

        // ---------------- Commands ----------------

        [RelayCommand]
        private void OpenProjectDetails()
        {
            if (SelectedProject is null) return;

            ProjectNameDetails = SelectedProject.ProjectName;
            CompanyDetails = SelectedProject.Company;
            DepartmentDetails = SelectedProject.Department;
            SourceOfFundDetails = SelectedProject.SourceOfFund;
            TotalBudgetDetails = SelectedProject.TotalBudgetText;

            IsProjectDetailsOpen = true;
        }

        [RelayCommand] private void CloseProjectDetails() => IsProjectDetailsOpen = false;

        [RelayCommand]
        private async Task OpenAddBeneficiaries()
        {
            // EKALINGA_PROJECT_SELECTION_GUARD
            if (SelectedProject is null) return;

            AddSearchText = "";
            AddCurrentPage = 1;
            OnPropertyChanged(nameof(AddBeneficiariesTitle));
            await RefreshAddListAsync();
            IsAddBeneficiariesOpen = true;
        }

        [RelayCommand] private void CloseAddBeneficiaries() => IsAddBeneficiariesOpen = false;

        [RelayCommand]
        private async Task ConfirmAddSelected()
        {
            if (SelectedProject is null || IsLoading) return;

            var picked = AddItems.Where(x => x.IsSelected).Select(x => x.Id).ToList();
            if (picked.Count == 0) return;

            IsLoading = true;
            try
            {
                await Task.Run(() => _assignRepo.AddAssignments(SelectedProject.Id, picked));

                IsAddBeneficiariesOpen = false;
                await ReloadEverythingCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Edit share
        [RelayCommand]
        private void OpenEditShare(BeneficiaryRecord? row)
        {
            if (row is null || SelectedProject is null) return;

            _editTarget = row;
            ClearShareErrors();

            if (SelectedProject.BudgetType == "Money")
            {
                ShareAmountInput = (row.ShareAmount ?? 0m).ToString("N0", CultureInfo.InvariantCulture);
                ShareQtyInput = "";
                ShareUnitInput = "";
            }
            else
            {
                ShareAmountInput = "";
                ShareQtyInput = (row.ShareQty ?? 0).ToString(CultureInfo.InvariantCulture);
                ShareUnitInput = row.ShareUnit ?? "";
            }

            IsEditShareOpen = true;
        }

        [RelayCommand] private void CloseEditShare() => IsEditShareOpen = false;

        [RelayCommand]
        private async Task ConfirmEditShare()
        {
            if (SelectedProject is null || _editTarget is null || IsLoading) return;

            ClearShareErrors();

            IsLoading = true;
            try
            {
                if (SelectedProject.BudgetType == "Money")
                {
                    var raw = (ShareAmountInput ?? "").Replace(",", "").Trim();
                    if (!decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
                    {
                        ShareAmountError = "Share amount must be a valid number (> 0).";
                        HasShareAmountError = true;
                        return;
                    }

                    await Task.Run(() => _assignRepo.UpdateShareMoney(SelectedProject.Id, _editTarget.Id, amt));
                }
                else
                {
                    if (!int.TryParse((ShareQtyInput ?? "").Trim(), out var qty) || qty <= 0)
                    {
                        ShareInKindError = "Quantity must be a valid number (> 0) and unit is required.";
                        HasShareInKindError = true;
                        return;
                    }

                    var unit = (ShareUnitInput ?? "").Trim();
                    if (string.IsNullOrWhiteSpace(unit))
                    {
                        ShareInKindError = "Quantity must be a valid number (> 0) and unit is required.";
                        HasShareInKindError = true;
                        return;
                    }

                    await Task.Run(() => _assignRepo.UpdateShareInKind(SelectedProject.Id, _editTarget.Id, qty, unit));
                }

                IsEditShareOpen = false;
                _editTarget = null;

                await ReloadEverythingCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearShareErrors()
        {
            HasShareAmountError = false;
            ShareAmountError = "";

            HasShareInKindError = false;
            ShareInKindError = "";
        }

        // Remove assignment
        [RelayCommand]
        private void OpenRemove(BeneficiaryRecord? row)
        {
            if (row is null || SelectedProject is null) return;

            _removeTarget = row;
            RemoveMessage = $"Remove {row.FirstName} {row.LastName} from this project?";
            IsRemoveOpen = true;
        }

        [RelayCommand]
        private void CloseRemove()
        {
            IsRemoveOpen = false;
            _removeTarget = null;
        }

        [RelayCommand]
        private async Task ConfirmRemove()
        {
            if (SelectedProject is null || _removeTarget is null || IsLoading) return;

            IsLoading = true;
            try
            {
                await Task.Run(() => _assignRepo.RemoveAssignment(SelectedProject.Id, _removeTarget.Id));

                IsRemoveOpen = false;
                _removeTarget = null;

                await ReloadEverythingCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<BeneficiaryRecord> AddFiltered()
        {
            var q = (AddSearchText ?? "").Trim().ToLowerInvariant();

            IEnumerable<BeneficiaryRecord> src = AddItems; // <-- your master list
            if (!string.IsNullOrWhiteSpace(q))
            {
                src = src.Where(x =>
                    (x.FirstName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.LastName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Barangay ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Classification ?? "").ToLowerInvariant().Contains(q) ||
                    x.Id.ToString().Contains(q));
            }

            return src;
        }

        private void ApplyAddPaging()
        {
            var filtered = AddFiltered().ToList();
            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(filtered.Count / (double)AddPageSize));

            if (AddCurrentPage < 1)
                AddCurrentPage = 1;

            if (AddCurrentPage > totalPages)
                AddCurrentPage = totalPages;

            AddPagedItems.Clear();
            foreach (var item in filtered
                .Skip((AddCurrentPage - 1) * AddPageSize)
                .Take(AddPageSize))
            {
                AddPagedItems.Add(item);
            }

            AddPageNumbers.Clear();
            for (var page = 1; page <= totalPages; page++)
                AddPageNumbers.Add(page);

            _syncingAddSelectAll = true;
            try
            {
                IsAddAllSelected =
                    filtered.Count > 0 &&
                    filtered.All(item => item.IsSelected);
            }
            finally
            {
                _syncingAddSelectAll = false;
            }

            OnPropertyChanged(nameof(AddSelectedCount));
            OnPropertyChanged(nameof(AddButtonText));
            OnPropertyChanged(nameof(AddFoundText));
        }
        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDataAsync();
        }

        // paging
        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }
        [RelayCommand] private void GoToPage(int page) { CurrentPage = page; }

        [RelayCommand] private void AddPreviousPage() { if (AddCurrentPage > 1) AddCurrentPage--; ApplyAddPaging(); }
        [RelayCommand] private void AddNextPage() { AddCurrentPage++; ApplyAddPaging(); }
        [RelayCommand] private void AddGoToPage(int page) { AddCurrentPage = page; ApplyAddPaging(); }

        public partial class BeneficiaryReleaseHistoryRow : ObservableObject
        {
            [ObservableProperty] private string projectName = "";
            [ObservableProperty] private string shareText = "";
            [ObservableProperty] private string releasedText = "";
        }


        [RelayCommand]
        private void OpenProfile(BeneficiaryRecord? row)
        {
            if (row is null) return;

            var details = _beneficiariesRepo.GetDetailsByInternalId(row.Id);

            ProfileBeneficiaryId = details?.BeneficiaryId ?? row.BeneficiaryId ?? "";
            ProfileCivilRegistryId = details?.CivilRegistryId ?? row.CivilRegistryId ?? "";
            ProfileFullName = $"{details?.FirstName ?? row.FirstName} {details?.MiddleName ?? row.MiddleName} {details?.LastName ?? row.LastName}".Replace("  ", " ").Trim();
            ProfileGender = details?.Gender ?? row.Gender ?? "";
            ProfileClassification = details?.Classification ?? row.Classification ?? "";
            ProfileBarangay = details?.Barangay ?? row.Barangay ?? "";
            ProfilePresentAddress = details?.PresentAddress ?? row.PresentAddress ?? "";
            ProfileShareText = row.ShareText;
            ProfileReleasedText = row.ReleasedText;

            LoadProfileHistory(row.Id);

            IsProfileOpen = true;
        }

        [RelayCommand]
        private void CloseProfile()
        {
            IsProfileOpen = false;

            ProfileBeneficiaryId = "";
            ProfileCivilRegistryId = "";
            ProfileFullName = "";
            ProfileGender = "";
            ProfileClassification = "";
            ProfileBarangay = "";
            ProfilePresentAddress = "";
            ProfileShareText = "";
            ProfileReleasedText = "";
            ProfileHistory.Clear();
            ProfileHistoryEmptyText = "No past releases found.";
        }

        private void LoadProfileHistory(int beneficiaryId)
        {
            ProfileHistory.Clear();

            var rows = _beneficiariesRepo.GetPastReleasesByBeneficiaryId(
                beneficiaryId,
                SelectedProject?.Id
            );

            foreach (var row in rows)
            {
                ProfileHistory.Add(new BeneficiaryReleaseHistoryRow
                {
                    ProjectName = row.ProjectName,
                    ShareText = row.ShareText,
                    ReleasedText = row.ReleasedText
                });
            }

            ProfileHistoryEmptyText = ProfileHistory.Count == 0
                ? "No past releases found."
                : "";
        }
    }
}
