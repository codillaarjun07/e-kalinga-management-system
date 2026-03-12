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
        [ObservableProperty] private AllotmentProjectOption? selectedProject;

        public ObservableCollection<BeneficiaryRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));
        public string FoundText => $"Found {TotalRecords} records";

        public string TotalBudgetText =>
            SelectedProject is null ? "Total Budget: ₱ 0.00" : $"Total Budget: {SelectedProject.TotalBudgetText}";

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
            await LoadDataAsync(selectFirstProject: true);
        }

        partial void OnSelectedClassificationChanged(string? value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            Apply();
        }

        partial void OnSearchTextChanged(string value) { CurrentPage = 1; Apply(); }
        partial void OnCurrentPageChanged(int value) { Apply(); }

        partial void OnSelectedProjectChanged(AllotmentProjectOption? value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            _ = ReloadEverythingAsync();
        }

        partial void OnAddSearchTextChanged(string value)
        {
            AddCurrentPage = 1;
            _ = RefreshAddListAsync();
        }

        private async Task RefreshAddListAsync()
        {
            await BuildAddListAsync();
            ApplyAddPaging();
        }

        private async Task LoadDataAsync(bool selectFirstProject = false)
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                var projects = await Task.Run(() => _allotmentRepo.GetAllProjects());

                Projects.Clear();
                foreach (var p in projects)
                    Projects.Add(p);

                if (selectFirstProject || SelectedProject is null || Projects.All(x => x.Id != SelectedProject.Id))
                    SelectedProject = Projects.FirstOrDefault();

                await ReloadEverythingCoreAsync();
            }
            catch
            {
                Projects.Clear();
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
        }

        private List<BeneficiaryRecord> Filtered()
        {
            IEnumerable<BeneficiaryRecord> src = _assignedCache;

            // classification filter (optional)
            var cls = (SelectedClassification ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(cls) && !cls.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (cls.Equals("None", StringComparison.OrdinalIgnoreCase))
                {
                    src = src.Where(x =>
                    {
                        var v = (x.Classification ?? "").Trim();
                        return string.IsNullOrWhiteSpace(v) || v.Equals("None", StringComparison.OrdinalIgnoreCase);
                    });
                }
                else
                {
                    src = src.Where(x =>
                        string.Equals((x.Classification ?? "").Trim(), cls, StringComparison.OrdinalIgnoreCase));
                }
            }

            var q = (SearchText ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(q))
            {
                src = src.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(q) ||
                    (x.FirstName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.LastName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Barangay ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Classification ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Gender ?? "").ToLowerInvariant().Contains(q));
                ;
            }

            return src.ToList();
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

        // -------- Add Beneficiaries (modal) --------
        private async Task BuildAddListAsync()
        {
            AddItems.Clear();
            IsAddAllSelected = false;

            if (SelectedProject is null)
            {
                OnPropertyChanged(nameof(AddSelectedCount));
                OnPropertyChanged(nameof(AddButtonText));
                OnPropertyChanged(nameof(AddFoundText));
                return;
            }

            var q = (AddSearchText ?? "").Trim().ToLowerInvariant();

            // ✅ Endorsed beneficiaries NOT yet assigned to this project
            var src = await Task.Run(() => _assignRepo.GetAvailableEndorsedNotAssigned(SelectedProject.Id, q));

            foreach (var r in src)
            {
                r.IsSelected = false;

                r.PropertyChanged -= AddRow_PropertyChanged;
                r.PropertyChanged += AddRow_PropertyChanged;

                AddItems.Add(r);
            }

            OnPropertyChanged(nameof(AddSelectedCount));
            OnPropertyChanged(nameof(AddButtonText));
            OnPropertyChanged(nameof(AddFoundText));
        }

        private void AddRow_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(BeneficiaryRecord.IsSelected))
            {
                OnPropertyChanged(nameof(AddSelectedCount));
                OnPropertyChanged(nameof(AddButtonText));

                if (AddItems.Count > 0)
                    IsAddAllSelected = AddItems.All(x => x.IsSelected);
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
            AddSearchText = "";
            AddCurrentPage = 1;
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
            var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)AddPageSize));

            if (AddCurrentPage < 1) AddCurrentPage = 1;
            if (AddCurrentPage > totalPages) AddCurrentPage = totalPages;

            AddPagedItems.Clear();
            foreach (var it in filtered.Skip((AddCurrentPage - 1) * AddPageSize).Take(AddPageSize))
                AddPagedItems.Add(it);

            AddPageNumbers.Clear();
            for (int i = 1; i <= totalPages; i++) AddPageNumbers.Add(i);

            OnPropertyChanged(nameof(AddFoundText));
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
