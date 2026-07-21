using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp3.Models;
using WpfApp3.Services;
using System.Windows;
using System.ComponentModel;
using static WpfApp3.ViewModels.Validators.ValidatorsViewModel;

namespace WpfApp3.ViewModels.Distribution
{
    public partial class DistributionViewModel : ObservableObject
    {
        private readonly AllotmentsRepository _allotmentRepo = new();
        private readonly AllotmentBeneficiariesRepository _assignRepo = new();
        private readonly ReleaseReportService _reportService = new();

        private List<BeneficiaryRecord> _cache = new();
        [RelayCommand]
        private void SetDistributionStatus(string? status)
        {
            SelectedDistributionStatus =
                string.IsNullOrWhiteSpace(status)
                    ? "All"
                    : status.Trim();
        }
        // paging (main page)
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

        // DISTRIBUTION COMMAND CENTER
        public int TotalBeneficiariesCount => _cache.Count;
        public int ReleasedCount => _cache.Count(x => x.IsReleased);
        public int RemainingCount => Math.Max(0, TotalBeneficiariesCount - ReleasedCount);

        public double CompletionPercentage =>
            TotalBeneficiariesCount == 0
                ? 0d
                : Math.Round(ReleasedCount * 100d / TotalBeneficiariesCount, 1);

        public string CompletionText => $"{CompletionPercentage:0.#}% complete";

        public string SelectedProjectSummary =>
            SelectedProject is null
                ? "Select an allotment to begin distribution."
                : $"{SelectedProject.Department} • {SelectedProject.SourceOfFund}";

        public int ReleaseReleasedCount => ReleaseItems.Count(x => x.IsReleased);
        public int ReleaseRemainingCount => Math.Max(0, ReleaseItems.Count - ReleaseReleasedCount);

        public double ReleaseCompletionPercentage =>
            ReleaseItems.Count == 0
                ? 0d
                : Math.Round(ReleaseReleasedCount * 100d / ReleaseItems.Count, 1);

        public string ReleaseCompletionText =>
            $"{ReleaseReleasedCount} of {ReleaseItems.Count} released";

        public string TotalBudgetText =>
            SelectedProject is null ? "Total Budget: -" : $"Total Budget: {SelectedProject.TotalBudgetText}";

        public bool HasSelectedProject => SelectedProject is not null;

        // ===== Toast =====
        [ObservableProperty] private bool isToastVisible;
        [ObservableProperty] private string toastMessage = "";
        [ObservableProperty] private Brush toastBackground = GetThemeBrush("ThemePrimaryBrush");
        private CancellationTokenSource? _toastCts;

        // ===== Release session modal =====
        [ObservableProperty] private bool isReleaseSessionOpen;
        public ObservableCollection<BeneficiaryRecord> ReleaseItems { get; } = new();
        [ObservableProperty] private BeneficiaryRecord? selectedReleaseRow;

        // shows scanned text in UI textbox
        [ObservableProperty] private string scanInput = "";

        public string ReleaseProjectText => SelectedProject is null ? "" : $"Project: {SelectedProject.ProjectName}";
        public string ReleaseBudgetText => SelectedProject is null ? "" : $"Budget: {SelectedProject.TotalBudgetText}";
        public string ReleaseProgressText =>
            $"Released: {ReleaseItems.Count(x => x.IsReleased)}/{ReleaseItems.Count}";

        // ===== Confirm release modal =====
        [ObservableProperty] private bool isConfirmReleaseOpen;

        private BeneficiaryRecord? _pendingRelease;

        [ObservableProperty] private string confirmId = "";
        [ObservableProperty] private string confirmName = "";
        [ObservableProperty] private string confirmBarangay = "";
        [ObservableProperty] private string confirmClassification = "";
        [ObservableProperty] private string confirmShare = "";

        private bool _ready;

        public ObservableCollection<string> ClassificationOptions { get; } = new();
        [ObservableProperty] private string? selectedClassification;

        public ObservableCollection<string> DistributionStatusOptions { get; } = new()
        {
            "All",
            "Waiting",
            "Released"
        };

        [ObservableProperty] private string? selectedDistributionStatus = "All";

        // ===== Release modal paging =====
        [ObservableProperty] private int releaseCurrentPage = 1;
        public int ReleasePageSize { get; } = 8;

        public ObservableCollection<BeneficiaryRecord> ReleasePagedItems { get; } = new();
        public ObservableCollection<int> ReleasePageNumbers { get; } = new();

        public int ReleaseTotalRecords => ReleaseFiltered().Count();
        public int ReleaseTotalPages => Math.Max(1, (int)Math.Ceiling(ReleaseTotalRecords / (double)ReleasePageSize));

        [ObservableProperty] private string? releaseSelectedClassification = "All";

        [ObservableProperty] private BeneficiaryRecord? pendingRelease;

        private readonly BeneficiariesRepository _beneRepo = new();

        [ObservableProperty] private BitmapImage? confirmProfileImagePreview;

        public bool ConfirmHasProfileImage => ConfirmProfileImagePreview != null;
        public ObservableCollection<ReleaseHistoryItem> ConfirmReleaseHistory { get; } = new();
        public bool HasConfirmReleaseHistory => ConfirmReleaseHistory.Count > 0;

        [ObservableProperty] private bool isGeneratingReport;

        [ObservableProperty] private bool isReportPreviewOpen;
        [ObservableProperty] private string reportPreviewPath = "";
        [ObservableProperty] private string reportPreviewTitle = "Release Report Preview";

        public bool HasReportPreview => !string.IsNullOrWhiteSpace(ReportPreviewPath);

        partial void OnReportPreviewPathChanged(string value)
        {
            OnPropertyChanged(nameof(HasReportPreview));
        }

        public string GenerateReportButtonText =>
            IsGeneratingReport ? "Generating..." : "Generate Report";

        partial void OnIsGeneratingReportChanged(bool value)
        {
            OnPropertyChanged(nameof(GenerateReportButtonText));
        }

        public DistributionViewModel()
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
                ApplyPaging();
                return;
            }

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
            ApplyPaging();
        }

        private List<BeneficiaryRecord> Filtered()
        {
            IEnumerable<BeneficiaryRecord> src = _cache;

            var cls = (SelectedClassification ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(cls) &&
                !cls.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (cls.Equals("None", StringComparison.OrdinalIgnoreCase))
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
                            cls,
                            StringComparison.OrdinalIgnoreCase));
                }
            }

            var status = (SelectedDistributionStatus ?? "All").Trim();

            if (status.Equals("Released", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => x.IsReleased);
            else if (status.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => !x.IsReleased);

            return src.ToList();
        }

        partial void OnSelectedDistributionStatusChanged(string? value)
        {
            if (!_ready)
                return;

            CurrentPage = 1;
            ApplyPaging();
        }

        partial void OnSelectedProjectChanged(AllotmentProjectOption? value)
        {
            SyncProjectSearchTextToSelection();
            OnPropertyChanged(nameof(HasSelectedProject));
            OnPropertyChanged(nameof(TotalBudgetText));
            OnPropertyChanged(nameof(ReleaseProjectText));
            OnPropertyChanged(nameof(ReleaseBudgetText));
            OnPropertyChanged(nameof(SelectedProjectSummary));

            if (value is null)
            {
                IsReleaseSessionOpen = false;
                IsConfirmReleaseOpen = false;
                IsReportPreviewOpen = false;
                ReleaseItems.Clear();
                ReleasePagedItems.Clear();
                ScanInput = "";
                _cache.Clear();
                ApplyPaging();
            }

            if (!_ready) return;
            CurrentPage = 1;
            _ = LoadBeneficiariesAsync();
        }

        partial void OnProjectSearchTextChanged(string value)
        {
            if (_syncingProjectSearch)
                return;

            ApplyProjectFilter(value);

            if (_ready)
                IsProjectDropdownOpen = true;
        }

        partial void OnCurrentPageChanged(int value) => ApplyPaging();

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
                await LoadBeneficiariesCoreAsync();
            }
            catch
            {
                SelectedProject = null;
                Projects.Clear();
                FilteredProjects.Clear();
                _cache.Clear();
                ApplyPaging();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadBeneficiariesAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                await LoadBeneficiariesCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadBeneficiariesCoreAsync()
        {
            _cache.Clear();

            if (SelectedProject is null)
            {
                ApplyPaging();
                return;
            }

            var rows = await Task.Run(() => _assignRepo.GetAssignedEndorsed(SelectedProject.Id));
            _cache = rows ?? new List<BeneficiaryRecord>();
            ApplyPaging();
        }

        private async Task ReloadAfterReleaseAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                await LoadBeneficiariesCoreAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyPaging()
        {
            var filtered = Filtered();

            if (CurrentPage < 1)
                CurrentPage = 1;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(filtered.Count / (double)PageSize));

            if (CurrentPage > totalPages)
                CurrentPage = totalPages;

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

            NotifyCommandCenterState();
        }

        private void NotifyCommandCenterState()
        {
            OnPropertyChanged(nameof(TotalBeneficiariesCount));
            OnPropertyChanged(nameof(ReleasedCount));
            OnPropertyChanged(nameof(RemainingCount));
            OnPropertyChanged(nameof(CompletionPercentage));
            OnPropertyChanged(nameof(CompletionText));
            OnPropertyChanged(nameof(SelectedProjectSummary));

            OnPropertyChanged(nameof(ReleaseReleasedCount));
            OnPropertyChanged(nameof(ReleaseRemainingCount));
            OnPropertyChanged(nameof(ReleaseCompletionPercentage));
            OnPropertyChanged(nameof(ReleaseCompletionText));
            OnPropertyChanged(nameof(ReleaseProgressText));
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

        // ===== Reload list inside Release modal (keeps Released column updated) =====
        private void ReloadReleaseItems()
        {
            ReleaseItems.Clear();

            if (SelectedProject is null)
            {
                ApplyReleasePaging();
                NotifyCommandCenterState();
                return;
            }

            foreach (var record in _assignRepo.GetAssignedEndorsed(SelectedProject.Id))
                ReleaseItems.Add(record);

            if (ReleaseCurrentPage > ReleaseTotalPages)
                ReleaseCurrentPage = ReleaseTotalPages;

            ApplyReleasePaging();
            NotifyCommandCenterState();
        }

                private static Brush GetThemeBrush(string key)
        {
            return Application.Current?.TryFindResource(key) as Brush
                ?? Brushes.SlateGray;
        }

private async void ShowToast(string msg, string kind)
        {
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;

            ToastMessage = msg;
            ToastBackground = kind switch
            {
                "success" => GetThemeBrush("ThemeSuccessBrush"),
                "error" => GetThemeBrush("ThemeDangerBrush"),
                "warning" => GetThemeBrush("ThemeWarningBrush"),
                _ => GetThemeBrush("ThemePrimaryBrush")
            };

            IsToastVisible = true;

            try
            {
                await Task.Delay(2200, token);
                IsToastVisible = false;
            }
            catch { }
        }

        // ================= Commands =================

        [RelayCommand]
        private async Task GenerateReleaseReport()
        {
            if (IsGeneratingReport)
                return;

            if (SelectedProject is null)
            {
                ShowToast("Select a project first.", "warning");
                return;
            }

            List<BeneficiaryRecord> reportRows;
            string activeFilter;

            if (IsReleaseSessionOpen)
            {
                if (ReleaseItems.Count == 0)
                    ReloadReleaseItems();

                activeFilter = NormalizeLabel(ReleaseSelectedClassification, "All");
                reportRows = ReleaseFiltered().ToList();
            }
            else
            {
                activeFilter = NormalizeLabel(SelectedClassification, "All");
                reportRows = Filtered().ToList();
            }

            if (reportRows.Count == 0)
            {
                ShowToast("No release records found for the current filter.", "warning");
                return;
            }

            var fileName = SafeFileName(
                $"{SelectedProject.ProjectName}-Release-Report-{DateTime.Now:yyyyMMdd-HHmm}.pdf");

            var previewFolder = Path.Combine(
                Path.GetTempPath(),
                "WpfApp3",
                "ReportPreviews");

            Directory.CreateDirectory(previewFolder);

            var previewPath = Path.Combine(previewFolder, fileName);

            try
            {
                IsGeneratingReport = true;

                var reportData = BuildReleaseReportData(reportRows, activeFilter);
                await Task.Run(() => _reportService.GeneratePdf(previewPath, reportData));

                ReportPreviewTitle = $"{SelectedProject.ProjectName} - Release Report Preview";
                ReportPreviewPath = previewPath;
                IsReportPreviewOpen = true;

                ShowToast("Release report preview generated successfully.", "success");
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to generate report: {ex.Message}", "error");
            }
            finally
            {
                IsGeneratingReport = false;
            }
        }



        [RelayCommand]
        private void CloseReportPreview()
        {
            IsReportPreviewOpen = false;
        }

        [RelayCommand]
        private void SavePreviewReport()
        {
            if (string.IsNullOrWhiteSpace(ReportPreviewPath) || !File.Exists(ReportPreviewPath))
            {
                ShowToast("Preview file not found.", "error");
                return;
            }

            var baseName = Path.GetFileNameWithoutExtension(ReportPreviewPath);
            var savePath = _reportService.PickSavePath(baseName);

            if (string.IsNullOrWhiteSpace(savePath))
                return;

            try
            {
                File.Copy(ReportPreviewPath, savePath, true);
                ShowToast("Release report saved successfully.", "success");
            }
            catch (Exception ex)
            {
                ShowToast($"Failed to save report: {ex.Message}", "error");
            }
        }

        [RelayCommand]
        private void OpenPreviewExternally()
        {
            if (string.IsNullOrWhiteSpace(ReportPreviewPath) || !File.Exists(ReportPreviewPath))
            {
                ShowToast("Preview file not found.", "error");
                return;
            }

            try
            {
                _reportService.Open(ReportPreviewPath);
            }
            catch (Exception ex)
            {
                ShowToast($"Could not open PDF: {ex.Message}", "warning");
            }
        }

        [RelayCommand]
        private void OpenProjectDetails()
        {
            // optional – reuse Beneficiaries project modal later if you want
            ShowToast("Project details is optional here.", "info");
        }

        [RelayCommand]
        private void OpenReleaseSession()
        {
            if (SelectedProject is null) return;

            ScanInput = "";

            ReleaseCurrentPage = 1;     // ✅ start at page 1
            ReleaseSelectedClassification = SelectedClassification ?? "All";
            ReloadReleaseItems();

            IsReleaseSessionOpen = true;
        }

        [RelayCommand]
        private void CloseReleaseSession()
        {
            IsReleaseSessionOpen = false;
            ScanInput = "";
        }

        // called by code-behind (global scan capture) on Enter
        [RelayCommand]
        private void Scan(string? scanned)
        {
            var raw = (scanned ?? "").Trim();
            if (string.IsNullOrWhiteSpace(raw)) return;

            // ✅ always show scanned value in textbox
            ScanInput = raw;

            // ✅ compare as-is (case-insensitive)
            var hit = ReleaseItems.FirstOrDefault(x =>
                string.Equals((x.BeneficiaryId ?? "").Trim(), raw, StringComparison.OrdinalIgnoreCase));

            var idx = ReleaseItems.IndexOf(hit);
            if (idx >= 0)
            {
                ReleaseCurrentPage = (idx / ReleasePageSize) + 1;
                ApplyReleasePaging();
            }

            if (hit is null)
            {
                ShowToast($"Scan not found: {raw}", "error");
                return;
            }

            if (hit.IsReleased)
            {
                ShowToast($"Already released: {raw}", "warning");
                return;
            }

            _pendingRelease = hit;
            PendingRelease = hit;
            OnPropertyChanged(nameof(PendingRelease));
            SelectedReleaseRow = hit;

            HydratePendingReleaseFromDb(hit);
            LoadConfirmReleaseHistory(hit.Id);

            ConfirmId = hit.BeneficiaryId; // show barcode string
            ConfirmName = $"{hit.FirstName} {hit.LastName}".Trim();
            ConfirmBarangay = hit.Barangay;
            ConfirmClassification = hit.Classification;
            ConfirmShare = hit.ShareText;

            IsConfirmReleaseOpen = true;
            ShowToast($"Scan success: {raw}", "success");
        }

        [RelayCommand]
        private void CloseConfirmRelease()
        {
            IsConfirmReleaseOpen = false;
            _pendingRelease = null;
            ScanInput = "";
            PendingRelease = null;
            OnPropertyChanged(nameof(PendingRelease));
            ConfirmProfileImagePreview = null;
            ConfirmReleaseHistory.Clear();
            OnPropertyChanged(nameof(HasConfirmReleaseHistory));
        }

        [RelayCommand]
        private async Task ConfirmRelease()
        {
            if (SelectedProject is null || _pendingRelease is null) return;

            _assignRepo.MarkReleased(SelectedProject.Id, _pendingRelease.Id);

            // ✅ update modal table
            ReloadReleaseItems();

            // ✅ update main page table
            await ReloadAfterReleaseAsync();

            IsConfirmReleaseOpen = false;
            ShowToast($"Released to ID {_pendingRelease.BeneficiaryId}", "success");
            _pendingRelease = null;
            ScanInput = "";
        }

        partial void OnReleaseCurrentPageChanged(int value) => ApplyReleasePaging();

        private void ApplyReleasePaging()
        {
            var filtered = ReleaseFiltered().ToList();

            if (ReleaseCurrentPage < 1)
                ReleaseCurrentPage = 1;

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(filtered.Count / (double)ReleasePageSize));

            if (ReleaseCurrentPage > totalPages)
                ReleaseCurrentPage = totalPages;

            ReleasePagedItems.Clear();

            foreach (var item in filtered
                .Skip((ReleaseCurrentPage - 1) * ReleasePageSize)
                .Take(ReleasePageSize))
            {
                ReleasePagedItems.Add(item);
            }

            ReleasePageNumbers.Clear();

            for (var page = 1; page <= totalPages; page++)
                ReleasePageNumbers.Add(page);

            OnPropertyChanged(nameof(ReleaseTotalRecords));
            OnPropertyChanged(nameof(ReleaseTotalPages));

            NotifyCommandCenterState();
        }

        partial void OnReleaseSelectedClassificationChanged(string? value)
        {
            if (!_ready) return;
            ReleaseCurrentPage = 1;
            ApplyReleasePaging();
        }

        private IEnumerable<BeneficiaryRecord> ReleaseFiltered()
        {
            IEnumerable<BeneficiaryRecord> src = ReleaseItems;

            var cls = (ReleaseSelectedClassification ?? "").Trim();
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

            return src;
        }


        // paging (main page)
        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDataAsync();
        }

        [RelayCommand] private void PreviousPage() { if (CurrentPage > 1) CurrentPage--; }
        [RelayCommand] private void NextPage() { if (CurrentPage < TotalPages) CurrentPage++; }
        [RelayCommand] private void GoToPage(int page) { CurrentPage = page; }


        [RelayCommand] private void ReleasePreviousPage() { if (ReleaseCurrentPage > 1) ReleaseCurrentPage--; }
        [RelayCommand] private void ReleaseNextPage() { if (ReleaseCurrentPage < ReleaseTotalPages) ReleaseCurrentPage++; }
        [RelayCommand] private void ReleaseGoToPage(int page) { ReleaseCurrentPage = page; }

        partial void OnConfirmProfileImagePreviewChanged(BitmapImage? value)
        {
            OnPropertyChanged(nameof(ConfirmHasProfileImage));
        }

        private static BitmapImage? ToBitmap(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0) return null;

            try
            {
                using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        private void HydratePendingReleaseFromDb(BeneficiaryRecord target)
        {
            try
            {
                // ✅ internal id is target.Id (b.id)
                var full = _beneRepo.GetDetailsByInternalId(target.Id);
                if (full is null)
                {
                    ConfirmProfileImagePreview = null;
                    return;
                }

                // ✅ fill the missing fields so the modal bindings work
                target.BeneficiaryId = full.BeneficiaryId;
                target.CivilRegistryId = full.CivilRegistryId;
                target.FirstName = full.FirstName;
                target.MiddleName = full.MiddleName;
                target.LastName = full.LastName;
                target.Gender = full.Gender;
                target.Barangay = full.Barangay;
                target.Classification = string.IsNullOrWhiteSpace(full.Classification) ? "None" : full.Classification;
                target.PresentAddress = full.PresentAddress;

                // ✅ image for modal avatar
                ConfirmProfileImagePreview = ToBitmap(full.ProfileImage);
            }
            catch (Exception ex)
            {
                ConfirmProfileImagePreview = null;
                ShowToast($"Failed to load beneficiary details: {ex.Message}", "error");
            }
        }


        public sealed class ReleaseHistoryItem
        {
            public int AllotmentId { get; set; }
            public DateTime ReleasedAt { get; set; }
            public string ShareText { get; set; } = "";
            public bool IsLast { get; set; }

            public string ReleasedAtText =>
                ReleasedAt.ToString("MMM dd, yyyy • hh:mm tt", CultureInfo.InvariantCulture);

            public string Description => $"Allotment #{AllotmentId} • {ShareText}";
        }

        private void LoadConfirmReleaseHistory(int beneficiaryInternalId)
        {
            ConfirmReleaseHistory.Clear();


            try
            {
                var rows = _assignRepo.GetReleaseHistory(beneficiaryInternalId);

                var items = rows.Select(x =>
                {
                    var share = x.ShareAmount is not null
                        ? $"₱ {x.ShareAmount.Value:N2}"
                        : (x.ShareQty is not null
                            ? $"{x.ShareQty.Value} {x.ShareUnit}".Trim()
                            : "-");

                    return new ReleaseHistoryItem
                    {
                        AllotmentId = x.AllotmentId,
                        ReleasedAt = x.ReleasedAt,
                        ShareText = share
                    };
                }).ToList();

                for (int i = 0; i < items.Count; i++)
                    items[i].IsLast = (i == items.Count - 1);

                foreach (var it in items)
                    ConfirmReleaseHistory.Add(it);
            }
            catch
            {
                // ignore history failures; don't block confirm release
            }

            OnPropertyChanged(nameof(HasConfirmReleaseHistory));
        }

        private ReleaseReportData BuildReleaseReportData(List<BeneficiaryRecord> rows, string classificationFilter)
        {
            var total = rows.Count;
            var released = rows.Count(x => x.IsReleased);
            var pending = total - released;

            var classificationBreakdown = rows
                .GroupBy(x => NormalizeLabel(x.Classification, "None"))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Select(g => new ReleaseMetricItem
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => ParseAmount(x.ShareText)),
                    Percent = total == 0 ? 0 : g.Count() * 100d / total
                })
                .ToList();

            var barangayBreakdown = rows
                .GroupBy(x => NormalizeLabel(x.Barangay, "Unspecified"))
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(5)
                .Select(g => new ReleaseMetricItem
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Amount = g.Sum(x => ParseAmount(x.ShareText)),
                    Percent = total == 0 ? 0 : g.Count() * 100d / total
                })
                .ToList();

            var beneficiaries = rows
                .OrderBy(x => x.IsReleased)
                .ThenBy(x => x.LastName)
                .ThenBy(x => x.FirstName)
                .Select(x => new ReleaseBeneficiaryItem
                {
                    BeneficiaryId = x.BeneficiaryId ?? "",
                    FullName = $"{x.FirstName} {x.LastName}".Trim(),
                    Barangay = NormalizeLabel(x.Barangay, "Unspecified"),
                    Classification = NormalizeLabel(x.Classification, "None"),
                    ShareText = x.ShareText ?? "-",
                    ReleasedText = x.ReleasedText ?? (x.IsReleased ? "Released" : "Not Released")
                })
                .ToList();

            return new ReleaseReportData
            {
                ProjectName = SelectedProject?.ProjectName ?? "Release Report",
                TotalBudgetText = SelectedProject?.TotalBudgetText ?? "-",
                ClassificationFilter = classificationFilter,
                GeneratedAt = DateTime.Now,
                TotalBeneficiaries = total,
                ReleasedCount = released,
                PendingCount = pending,
                ReleasedAmount = rows.Where(x => x.IsReleased).Sum(x => ParseAmount(x.ShareText)),
                PendingAmount = rows.Where(x => !x.IsReleased).Sum(x => ParseAmount(x.ShareText)),
                ClassificationBreakdown = classificationBreakdown,
                BarangayBreakdown = barangayBreakdown,
                Beneficiaries = beneficiaries
            };
        }

        private static decimal ParseAmount(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0m;

            var cleaned = new string(text
                .Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-')
                .ToArray())
                .Replace(",", "");

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0m;
        }

        private static string NormalizeLabel(string? value, string fallback)
        {
            var text = (value ?? "").Trim();
            return string.IsNullOrWhiteSpace(text) ? fallback : text;
        }

        private static string SafeFileName(string value)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '-');

            return value;
        }
    }
}