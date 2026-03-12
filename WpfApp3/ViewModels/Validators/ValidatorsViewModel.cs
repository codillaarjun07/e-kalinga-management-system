using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Validators
{
    public enum ValidatorsMainTab
    {
        NotYetValidated,
        Validated
    }

    public enum ValidatorsStatusTab
    {
        Endorsed,
        Pending,
        Rejected
    }

    public partial class ValidatorsViewModel : ObservableObject
    {
        private readonly BeneficiariesRepository _repo = new();
        private readonly AllotmentBeneficiariesRepository _releaseRepo = new();
        private readonly SettingsRepository _settingsRepo = new();
        private const string ClassificationTable = "classifications";

        private readonly List<ValidatorRecord> _externalPeople = new();
        private List<ValidatorRecord> _notYetBase = new();
        private readonly Dictionary<string, List<ValidatorRecord>> _validatedByStatus = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Endorsed"] = new List<ValidatorRecord>(),
            ["Pending"] = new List<ValidatorRecord>(),
            ["Rejected"] = new List<ValidatorRecord>()
        };

        public ObservableCollection<ReleaseHistoryItem> ReleaseHistory { get; } = new();
        public bool HasReleaseHistory => ReleaseHistory.Count > 0;

        [ObservableProperty] private ValidatorsMainTab activeMainTab = ValidatorsMainTab.NotYetValidated;
        [ObservableProperty] private ValidatorsStatusTab activeStatusTab = ValidatorsStatusTab.Endorsed;

        [ObservableProperty] private string searchNotYetText = "";
        [ObservableProperty] private string searchValidatedText = "";

        public ObservableCollection<ValidatorRecord> NotYetItems { get; } = new();
        public ObservableCollection<ValidatorRecord> ValidatedItems { get; } = new();

        [ObservableProperty] private ValidatorRecord? selectedPerson;

        [ObservableProperty] private bool isValidateModalOpen = false;
        [ObservableProperty] private bool isProfileModalOpen = false;
        [ObservableProperty] private bool isSaveConfirmOpen = false;
        [ObservableProperty] private bool isLoading = false;

        [ObservableProperty] private string validateSelectedStatus = "";

        private bool _isAddingProfile;
        public bool IsAddingProfile
        {
            get => _isAddingProfile;
            set => SetProperty(ref _isAddingProfile, value);
        }

        public string NotYetFoundText => $"Found {NotYetItems.Count} records";
        public string ValidatedFoundText => $"Found {ValidatedItems.Count} records";

        public ObservableCollection<string> GenderOptions { get; } = new() { "Male", "Female" };
        public ObservableCollection<string> ClassificationOptions { get; } = new();
        public ObservableCollection<string> ValidateStatusOptions { get; } = new() { "Endorsed", "Pending", "Rejected" };

        public ValidatorsViewModel()
        {
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                _repo.EnsureTable();
                LoadClassificationOptions();
                SeedExternalPeople();
                BuildCaches(
                    _repo.GetByBeneficiaryIds(_externalPeople.Select(x => x.BeneficiaryId)),
                    _repo.GetByStatus("Not Validated") ?? new List<ValidatorRecord>(),
                    _repo.GetByStatus("Endorsed") ?? new List<ValidatorRecord>(),
                    _repo.GetByStatus("Pending") ?? new List<ValidatorRecord>(),
                    _repo.GetByStatus("Rejected") ?? new List<ValidatorRecord>());
                ApplyAllFilters();
                SelectedPerson = NotYetItems.FirstOrDefault() ?? ValidatedItems.FirstOrDefault();
                return;
            }

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await RefreshDataAsync();
        }

        private async Task RefreshDataAsync(string? preferredBeneficiaryId = null)
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                var result = await Task.Run(() =>
                {
                    _repo.EnsureTable();

                    var classifications = _settingsRepo.GetAll(ClassificationTable)
                        .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Name))
                        .Select(x => x.Name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(x => x)
                        .ToList();

                    if (classifications.Count == 0)
                        classifications.Add("None");

                    SeedExternalPeople();

                    var savedByIds = _repo.GetByBeneficiaryIds(_externalPeople.Select(x => x.BeneficiaryId));
                    var notValidated = _repo.GetByStatus("Not Validated") ?? new List<ValidatorRecord>();
                    var endorsed = _repo.GetByStatus("Endorsed") ?? new List<ValidatorRecord>();
                    var pending = _repo.GetByStatus("Pending") ?? new List<ValidatorRecord>();
                    var rejected = _repo.GetByStatus("Rejected") ?? new List<ValidatorRecord>();

                    return new RefreshSnapshot
                    {
                        Classifications = classifications,
                        SavedByIds = savedByIds,
                        NotValidated = notValidated,
                        Endorsed = endorsed,
                        Pending = pending,
                        Rejected = rejected
                    };
                });

                ClassificationOptions.Clear();
                foreach (var item in result.Classifications)
                    ClassificationOptions.Add(item);

                BuildCaches(result.SavedByIds, result.NotValidated, result.Endorsed, result.Pending, result.Rejected);
                ApplyAllFilters();
                RestoreSelection(preferredBeneficiaryId);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BuildCaches(
            Dictionary<string, ValidatorRecord> savedByIds,
            List<ValidatorRecord> notValidated,
            List<ValidatorRecord> endorsed,
            List<ValidatorRecord> pending,
            List<ValidatorRecord> rejected)
        {
            var merged = new List<ValidatorRecord>();

            foreach (var ext in _externalPeople)
            {
                if (savedByIds.TryGetValue(ext.BeneficiaryId, out var dbRow))
                {
                    var st = CanonicalStatus(dbRow.Status);
                    if (IsValidatedStatus(st))
                        continue;

                    merged.Add(dbRow);
                }
                else
                {
                    merged.Add(ext);
                }
            }

            var extIds = new HashSet<string>(
                _externalPeople.Select(x => x.BeneficiaryId),
                StringComparer.OrdinalIgnoreCase);

            var mergedIds = new HashSet<string>(
                merged.Where(x => !string.IsNullOrWhiteSpace(x.BeneficiaryId)).Select(x => x.BeneficiaryId),
                StringComparer.OrdinalIgnoreCase);

            foreach (var dbRow in notValidated)
            {
                if (!string.IsNullOrWhiteSpace(dbRow.BeneficiaryId)
                    && !extIds.Contains(dbRow.BeneficiaryId)
                    && !mergedIds.Contains(dbRow.BeneficiaryId))
                {
                    merged.Add(dbRow);
                    mergedIds.Add(dbRow.BeneficiaryId);
                }
            }

            _notYetBase = merged;
            _validatedByStatus["Endorsed"] = endorsed;
            _validatedByStatus["Pending"] = pending;
            _validatedByStatus["Rejected"] = rejected;
        }

        private void ApplyAllFilters()
        {
            ApplyNotYetFilter();
            ApplyValidatedFilter();
        }

        private void ApplyNotYetFilter()
        {
            NotYetItems.Clear();

            IEnumerable<ValidatorRecord> q = _notYetBase;
            var s = (SearchNotYetText ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(s))
            {
                q = q.Where(x =>
                    (x.FirstName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.BeneficiaryId ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in q)
                NotYetItems.Add(item);

            OnPropertyChanged(nameof(NotYetFoundText));
        }

        private void ApplyValidatedFilter()
        {
            ValidatedItems.Clear();

            var status = CurrentValidatedStatus();
            IEnumerable<ValidatorRecord> q = _validatedByStatus.TryGetValue(status, out var rows)
                ? rows
                : Enumerable.Empty<ValidatorRecord>();

            var s = (SearchValidatedText ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(s))
            {
                q = q.Where(x =>
                    (x.FirstName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.BeneficiaryId ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in q)
                ValidatedItems.Add(item);

            OnPropertyChanged(nameof(ValidatedFoundText));
        }

        private void RestoreSelection(string? preferredBeneficiaryId = null)
        {
            var targetId = !string.IsNullOrWhiteSpace(preferredBeneficiaryId)
                ? preferredBeneficiaryId
                : SelectedPerson?.BeneficiaryId;

            var selected = FindVisibleRecordByBeneficiaryId(targetId);
            if (selected is not null)
            {
                SelectedPerson = selected;
                return;
            }

            SelectedPerson = ActiveMainTab == ValidatorsMainTab.NotYetValidated
                ? NotYetItems.FirstOrDefault() ?? ValidatedItems.FirstOrDefault()
                : ValidatedItems.FirstOrDefault() ?? NotYetItems.FirstOrDefault();
        }

        private ValidatorRecord? FindVisibleRecordByBeneficiaryId(string? beneficiaryId)
        {
            if (string.IsNullOrWhiteSpace(beneficiaryId))
                return null;

            return NotYetItems.FirstOrDefault(x => string.Equals(x.BeneficiaryId, beneficiaryId, StringComparison.OrdinalIgnoreCase))
                ?? ValidatedItems.FirstOrDefault(x => string.Equals(x.BeneficiaryId, beneficiaryId, StringComparison.OrdinalIgnoreCase));
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await RefreshDataAsync();
        }

        [RelayCommand]
        private void CloseAllModals()
        {
            IsValidateModalOpen = false;
            IsProfileModalOpen = false;
            IsSaveConfirmOpen = false;
        }

        private void SeedExternalPeople()
        {
            _externalPeople.Clear();

            _externalPeople.AddRange(new[]
            {
                NewExternal(101,"BENE-000101","CR-900101","Arjun","M.","Codilla","Male","25 January 1990","PWD","San Jose","San Jose, California, USA"),
                NewExternal(102,"BENE-000102","CR-900102","Maria","L.","Santos","Female","03 March 1992","Senior Citizen","Quezon City","Quezon City, Philippines"),
                NewExternal(103,"BENE-000103","CR-900103","John","A.","Dela Cruz","Male","12 December 1988","PWD","Cebu City","Cebu City, Philippines"),
                NewExternal(104,"BENE-000104","CR-900104","Angel","R.","Reyes","Female","06 June 1996","Indigenous","Davao City","Davao City, Philippines"),
                NewExternal(105,"BENE-000105","CR-900105","Paolo","S.","Garcia","Male","09 September 1991","None","Baguio City","Baguio City, Philippines"),
                NewExternal(106,"BENE-000106","CR-900106","Kristine","P.","Navarro","Female","21 July 1994","PWD","Iloilo City","Iloilo City, Philippines"),
                NewExternal(107,"BENE-000107","CR-900107","Mark","T.","Flores","Male","10 October 1993","Vendor","Cagayan de Oro","Cagayan de Oro, Philippines"),
                NewExternal(108,"BENE-000108","CR-900108","Lea","G.","Mendoza","Female","14 February 1995","Farmer","Legazpi","Legazpi, Philippines"),
                NewExternal(109,"BENE-000109","CR-900109","Joshua","K.","Ramos","Male","18 August 1987","Fisherfolk","Navotas City","Navotas City, Metro Manila, Philippines"),
                NewExternal(110,"BENE-000110","CR-900110","Andrea","M.","Villanueva","Female","02 February 1998","Student","Makati City","Makati City, Metro Manila, Philippines"),
                NewExternal(111,"BENE-000111","CR-900111","Rafael","D.","Lim","Male","27 November 1984","OFW","Pasig City","Pasig City, Metro Manila, Philippines"),
                NewExternal(112,"BENE-000112","CR-900112","Shaina","C.","Del Rosario","Female","05 May 1990","Single Parent","Taguig City","Taguig City, Metro Manila, Philippines"),
                NewExternal(113,"BENE-000113","CR-900113","Noel","B.","Aquino","Male","11 January 1979","Senior Citizen","Manila","Manila, Metro Manila, Philippines"),
                NewExternal(114,"BENE-000114","CR-900114","Patricia","E.","Castro","Female","19 September 1993","PWD","Caloocan City","Caloocan City, Metro Manila, Philippines"),
                NewExternal(115,"BENE-000115","CR-900115","Harold","S.","Gonzales","Male","30 June 1995","Vendor","Bacolod City","Bacolod City, Negros Occidental, Philippines"),
                NewExternal(116,"BENE-000116","CR-900116","Jenica","T.","Soriano","Female","07 July 1991","Farmer","Malaybalay City","Malaybalay City, Bukidnon, Philippines"),
                NewExternal(117,"BENE-000117","CR-900117","Dennis","A.","Pineda","Male","22 October 1989","Indigenous","Kidapawan City","Kidapawan City, Cotabato, Philippines"),
                NewExternal(118,"BENE-000118","CR-900118","Aileen","R.","Salazar","Female","13 March 1997","Unemployed","Zamboanga City","Zamboanga City, Zamboanga Peninsula, Philippines"),
                NewExternal(119,"BENE-000119","CR-900119","Gerald","N.","Uy","Male","04 April 1992","None","General Santos City","General Santos City, South Cotabato, Philippines"),
                NewExternal(120,"BENE-000120","CR-900120","Clarisse","P.","Bautista","Female","16 December 1986","Healthcare","Tacloban City","Tacloban City, Leyte, Philippines"),
            });
        }

        private static ValidatorRecord NewExternal(
            int sourceId,
            string beneId,
            string civilId,
            string fn,
            string mn,
            string ln,
            string gender,
            string dob,
            string classification,
            string barangay,
            string address)
        {
            return new ValidatorRecord
            {
                Id = sourceId,
                BeneficiaryId = beneId,
                CivilRegistryId = civilId,
                FirstName = fn,
                MiddleName = mn,
                LastName = ln,
                Gender = gender,
                DateOfBirth = dob,
                Classification = classification,
                Barangay = barangay,
                PresentAddress = address,
                Status = ""
            };
        }

        private static bool IsValidatedStatus(string status)
        {
            return string.Equals(status, "Endorsed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase);
        }

        private static string CanonicalStatus(string status)
        {
            status = (status ?? "").Trim();
            if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "Pending";
            if (status.Equals("Rejected", StringComparison.OrdinalIgnoreCase)) return "Rejected";
            if (status.Equals("Endorsed", StringComparison.OrdinalIgnoreCase)) return "Endorsed";
            if (status.Equals("Not Validated", StringComparison.OrdinalIgnoreCase)) return "Not Validated";
            return status;
        }

        private void LoadClassificationOptions()
        {
            ClassificationOptions.Clear();

            var items = _settingsRepo.GetAll(ClassificationTable)
                .Where(x => x.IsActive && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            foreach (var item in items)
                ClassificationOptions.Add(item);

            if (ClassificationOptions.Count == 0)
                ClassificationOptions.Add("None");
        }

        private string CurrentValidatedStatus()
        {
            return ActiveStatusTab switch
            {
                ValidatorsStatusTab.Endorsed => "Endorsed",
                ValidatorsStatusTab.Pending => "Pending",
                ValidatorsStatusTab.Rejected => "Rejected",
                _ => "Endorsed"
            };
        }

        [RelayCommand]
        private void SetMainTab(ValidatorsMainTab tab)
        {
            ActiveMainTab = tab;
            RestoreSelection();
            IsAddingProfile = false;
        }

        [RelayCommand]
        private void SetStatusTab(ValidatorsStatusTab tab)
        {
            ActiveStatusTab = tab;
            ApplyValidatedFilter();
            RestoreSelection();
            IsAddingProfile = false;
        }

        [RelayCommand]
        private void SearchNotYet()
        {
            ApplyNotYetFilter();
            RestoreSelection();
        }

        [RelayCommand]
        private void SearchValidated()
        {
            ApplyValidatedFilter();
            RestoreSelection();
        }

        [RelayCommand]
        private void UploadProfileImage()
        {
            var person = SelectedPerson;
            if (person is null) return;

            var dlg = new OpenFileDialog
            {
                Title = "Select profile image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                person.ProfileImage = File.ReadAllBytes(dlg.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to load image.\n\n{ex.Message}",
                    "Upload",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        [RelayCommand]
        private void RemoveProfileImage()
        {
            var person = SelectedPerson;
            if (person is null) return;

            person.ProfileImage = null;
        }

        [RelayCommand]
        private void AddProfile()
        {
            ActiveMainTab = ValidatorsMainTab.NotYetValidated;
            SearchNotYetText = "";
            ApplyNotYetFilter();

            var next = GetNextBeneNumber();
            var beneId = $"BENE-{next:000000}";
            var civilId = $"CR-{next:000000}";

            var fresh = new ValidatorRecord
            {
                Id = 0,
                BeneficiaryId = beneId,
                CivilRegistryId = civilId,
                FirstName = "",
                MiddleName = "",
                LastName = "",
                Gender = GenderOptions.FirstOrDefault() ?? "Male",
                DateOfBirth = "",
                Classification = ClassificationOptions.FirstOrDefault() ?? "None",
                Barangay = "",
                PresentAddress = "",
                Status = "Not Validated",
                ProfileImage = null
            };

            NotYetItems.Insert(0, fresh);
            SelectedPerson = fresh;
            IsAddingProfile = true;
            OnPropertyChanged(nameof(NotYetFoundText));
        }

        [RelayCommand]
        private void OpenProfileModal(ValidatorRecord? person)
        {
            if (person is null) return;
            SelectedPerson = person;

            LoadReleaseHistoryForSelected();

            IsProfileModalOpen = true;
            IsAddingProfile = false;
        }

        [RelayCommand]
        private void CloseProfileModal()
        {
            IsProfileModalOpen = false;
            ReleaseHistory.Clear();
            OnPropertyChanged(nameof(HasReleaseHistory));
        }

        [RelayCommand]
        private void OpenSaveConfirm()
        {
            if (SelectedPerson is null) return;
            IsSaveConfirmOpen = true;
        }

        [RelayCommand]
        private void CloseSaveConfirm()
        {
            IsSaveConfirmOpen = false;
        }

        [RelayCommand]
        private async Task ConfirmSaveProfile()
        {
            var person = SelectedPerson;
            if (person is null) return;

            var current = CanonicalStatus(person.Status);
            var statusToSave = IsValidatedStatus(current) ? current : "Not Validated";

            await Task.Run(() =>
            {
                _repo.Upsert(person, statusToSave);
            });

            person.Status = statusToSave;
            IsSaveConfirmOpen = false;
            IsAddingProfile = false;

            await RefreshDataAsync(person.BeneficiaryId);
        }

        [RelayCommand]
        private void OpenValidateModal(ValidatorRecord? person)
        {
            var p = person ?? SelectedPerson;
            if (p is null) return;

            SelectedPerson = p;
            ValidateSelectedStatus = string.IsNullOrWhiteSpace(p.Status) || p.Status.Equals("Not Validated", StringComparison.OrdinalIgnoreCase)
                ? "Endorsed"
                : CanonicalStatus(p.Status);

            IsValidateModalOpen = true;
        }

        [RelayCommand]
        private void CloseValidateModal()
        {
            IsValidateModalOpen = false;
        }

        [RelayCommand]
        private async Task ConfirmValidate()
        {
            var person = SelectedPerson;
            if (person is null) return;

            var newStatus = CanonicalStatus(ValidateSelectedStatus);
            if (!IsValidatedStatus(newStatus)) return;

            person.Status = newStatus;

            await Task.Run(() =>
            {
                _repo.Upsert(person, newStatus);
            });

            ActiveMainTab = ValidatorsMainTab.Validated;
            ActiveStatusTab = newStatus switch
            {
                "Pending" => ValidatorsStatusTab.Pending,
                "Rejected" => ValidatorsStatusTab.Rejected,
                _ => ValidatorsStatusTab.Endorsed
            };

            IsValidateModalOpen = false;
            IsAddingProfile = false;

            await RefreshDataAsync(person.BeneficiaryId);
        }

        private int GetNextBeneNumber()
        {
            var max = _externalPeople.Count == 0 ? 0 : _externalPeople.Max(x => ExtractDigits(x.BeneficiaryId));

            if (_notYetBase.Count > 0)
                max = Math.Max(max, _notYetBase.Max(x => ExtractDigits(x.BeneficiaryId)));

            foreach (var list in _validatedByStatus.Values)
            {
                if (list.Count > 0)
                    max = Math.Max(max, list.Max(x => ExtractDigits(x.BeneficiaryId)));
            }

            if (max <= 0) max = 100000;

            return max + 1;
        }

        private static int ExtractDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            var digits = new string(value.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) ? n : 0;
        }

        private void LoadReleaseHistoryForSelected()
        {
            ReleaseHistory.Clear();
            OnPropertyChanged(nameof(HasReleaseHistory));

            var p = SelectedPerson;
            if (p is null) return;

            var internalId = _repo.GetInternalIdByBeneficiaryId(p.BeneficiaryId);
            if (internalId is null) return;

            var rows = _releaseRepo.GetReleaseHistory(internalId.Value);

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
                ReleaseHistory.Add(it);

            OnPropertyChanged(nameof(HasReleaseHistory));
        }

        private sealed class RefreshSnapshot
        {
            public List<string> Classifications { get; set; } = new();
            public Dictionary<string, ValidatorRecord> SavedByIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
            public List<ValidatorRecord> NotValidated { get; set; } = new();
            public List<ValidatorRecord> Endorsed { get; set; } = new();
            public List<ValidatorRecord> Pending { get; set; } = new();
            public List<ValidatorRecord> Rejected { get; set; } = new();
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
    }
}
