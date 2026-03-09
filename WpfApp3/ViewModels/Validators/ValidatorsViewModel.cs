using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;
using System.Globalization;


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

        public ObservableCollection<ReleaseHistoryItem> ReleaseHistory { get; } = new();

        public bool HasReleaseHistory => ReleaseHistory.Count > 0;

        // This represents the EXTERNAL DATABASE source (replace later with real external DB pull)
        private readonly List<ValidatorRecord> _externalPeople = new();

        // ===== Tabs =====
        [ObservableProperty] private ValidatorsMainTab activeMainTab = ValidatorsMainTab.NotYetValidated;
        [ObservableProperty] private ValidatorsStatusTab activeStatusTab = ValidatorsStatusTab.Endorsed;

        // ===== Search =====
        [ObservableProperty] private string searchNotYetText = "";
        [ObservableProperty] private string searchValidatedText = "";

        // ===== Lists shown in UI =====
        public ObservableCollection<ValidatorRecord> NotYetItems { get; } = new();
        public ObservableCollection<ValidatorRecord> ValidatedItems { get; } = new();

        // ===== Selection =====
        [ObservableProperty] private ValidatorRecord? selectedPerson;

        // ===== Modals =====
        [ObservableProperty] private bool isValidateModalOpen = false;
        [ObservableProperty] private bool isProfileModalOpen = false;
        [ObservableProperty] private bool isSaveConfirmOpen = false;

        [ObservableProperty] private string validateSelectedStatus = ""; // Endorsed/Pending/Rejected

        // ✅ Add Profile state
        private bool _isAddingProfile;
        public bool IsAddingProfile
        {
            get => _isAddingProfile;
            set => SetProperty(ref _isAddingProfile, value);
        }

        // ===== UI text =====
        public string NotYetFoundText => $"Found {NotYetItems.Count} records";
        public string ValidatedFoundText => $"Found {ValidatedItems.Count} records";

        // ===== Dropdown Sources =====
        public ObservableCollection<string> GenderOptions { get; } = new() { "Male", "Female" };

        // ✅ updated classification list (Farmer, Vendor)
        public ObservableCollection<string> ClassificationOptions { get; } =
            new() { "PWD", "Senior Citizen", "Indigenous", "Farmer", "Vendor", "None" };

        public ObservableCollection<string> ValidateStatusOptions { get; } = new() { "Endorsed", "Pending", "Rejected" };

        public ValidatorsViewModel()
        {
            _repo.EnsureTable();

            SeedExternalPeople(); // replace later with actual external DB pull

            LoadNotYet();
            LoadValidated();

            SelectedPerson = NotYetItems.FirstOrDefault() ?? ValidatedItems.FirstOrDefault();
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

            // External DB dummy list (left side)
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
                NewExternal(109, "BENE-000109", "CR-900109", "Joshua", "K.", "Ramos", "Male",   "18 August 1987", "Fisherfolk",     "Navotas City",        "Navotas City, Metro Manila, Philippines"),
                NewExternal(110, "BENE-000110", "CR-900110", "Andrea", "M.", "Villanueva", "Female","02 February 1998", "Student",       "Makati City",         "Makati City, Metro Manila, Philippines"),
NewExternal(111, "BENE-000111", "CR-900111", "Rafael", "D.", "Lim", "Male",    "27 November 1984", "OFW",            "Pasig City",          "Pasig City, Metro Manila, Philippines"),
NewExternal(112, "BENE-000112", "CR-900112", "Shaina", "C.", "Del Rosario", "Female","05 May 1990",  "Single Parent",  "Taguig City",         "Taguig City, Metro Manila, Philippines"),
NewExternal(113, "BENE-000113", "CR-900113", "Noel",   "B.", "Aquino", "Male",  "11 January 1979", "Senior Citizen", "Manila",              "Manila, Metro Manila, Philippines"),
NewExternal(114, "BENE-000114", "CR-900114", "Patricia","E.","Castro", "Female","19 September 1993","PWD",            "Caloocan City",       "Caloocan City, Metro Manila, Philippines"),
NewExternal(115, "BENE-000115", "CR-900115", "Harold", "S.", "Gonzales", "Male","30 June 1995",     "Vendor",         "Bacolod City",        "Bacolod City, Negros Occidental, Philippines"),
NewExternal(116, "BENE-000116", "CR-900116", "Jenica", "T.", "Soriano", "Female","07 July 1991",    "Farmer",         "Malaybalay City",     "Malaybalay City, Bukidnon, Philippines"),
NewExternal(117, "BENE-000117", "CR-900117", "Dennis", "A.", "Pineda", "Male", "22 October 1989",  "Indigenous",     "Kidapawan City",      "Kidapawan City, Cotabato, Philippines"),
NewExternal(118, "BENE-000118", "CR-900118", "Aileen", "R.", "Salazar", "Female","13 March 1997",   "Unemployed",     "Zamboanga City",      "Zamboanga City, Zamboanga Peninsula, Philippines"),
NewExternal(119, "BENE-000119", "CR-900119", "Gerald", "N.", "Uy", "Male",     "04 April 1992",    "None",           "General Santos City", "General Santos City, South Cotabato, Philippines"),
NewExternal(120, "BENE-000120", "CR-900120", "Clarisse","P.","Bautista", "Female","16 December 1986","Healthcare",     "Tacloban City",       "Tacloban City, Leyte, Philippines"),
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
                Id = sourceId, // external id
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
                Status = "" // external has no status
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

        private void LoadNotYet()
        {
            NotYetItems.Clear();

            // Pull any saved rows from OUR DB for these external beneficiary IDs
            var saved = _repo.GetByBeneficiaryIds(_externalPeople.Select(x => x.BeneficiaryId));

            // Merge:
            // - if already validated in DB => do NOT show in Not Yet Validated
            // - if saved as Not Validated => show DB version (includes saved edits)
            // - if not in DB => show external version
            var merged = new List<ValidatorRecord>();

            foreach (var ext in _externalPeople)
            {
                if (saved.TryGetValue(ext.BeneficiaryId, out var dbRow))
                {
                    var st = CanonicalStatus(dbRow.Status);
                    if (IsValidatedStatus(st))
                        continue;

                    merged.Add(dbRow); // Not Validated rows show here
                }
                else
                {
                    merged.Add(ext);
                }
            }

            // ✅ IMPORTANT FIX:
            // Include DB "Not Validated" rows that are NOT in the external list
            // (this is what makes newly added profiles appear after reload)
            var extIds = new HashSet<string>(
                _externalPeople.Select(x => x.BeneficiaryId),
                StringComparer.OrdinalIgnoreCase);

            var mergedIds = new HashSet<string>(
                merged.Select(x => x.BeneficiaryId),
                StringComparer.OrdinalIgnoreCase);

            var dbNotValidated = _repo.GetByStatus("Not Validated") ?? new List<ValidatorRecord>();
            foreach (var dbRow in dbNotValidated)
            {
                if (!string.IsNullOrWhiteSpace(dbRow.BeneficiaryId)
                    && !extIds.Contains(dbRow.BeneficiaryId)
                    && !mergedIds.Contains(dbRow.BeneficiaryId))
                {
                    merged.Add(dbRow);
                    mergedIds.Add(dbRow.BeneficiaryId);
                }
            }

            var q = merged.AsEnumerable();
            var s = (SearchNotYetText ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(s))
            {
                q = q.Where(x =>
                    (x.FirstName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.BeneficiaryId ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in q) NotYetItems.Add(item);
            OnPropertyChanged(nameof(NotYetFoundText));
        }

        private void LoadValidated()
        {
            ValidatedItems.Clear();

            var status = CurrentValidatedStatus();
            var rows = _repo.GetByStatus(status) ?? new List<ValidatorRecord>();

            var q = rows.AsEnumerable();
            var s = (SearchValidatedText ?? "").Trim();

            if (!string.IsNullOrWhiteSpace(s))
            {
                q = q.Where(x =>
                    (x.FirstName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.LastName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.BeneficiaryId ?? "").Contains(s, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var item in q) ValidatedItems.Add(item);
            OnPropertyChanged(nameof(ValidatedFoundText));
        }

        // ========= Commands that match your XAML =========

        [RelayCommand]
        private void SetMainTab(ValidatorsMainTab tab)
        {
            ActiveMainTab = tab;

            if (ActiveMainTab == ValidatorsMainTab.NotYetValidated)
            {
                LoadNotYet();
                SelectedPerson = NotYetItems.FirstOrDefault();
            }
            else
            {
                LoadValidated();
                SelectedPerson = ValidatedItems.FirstOrDefault();
            }

            IsAddingProfile = false;
        }

        [RelayCommand]
        private void SetStatusTab(ValidatorsStatusTab tab)
        {
            ActiveStatusTab = tab;
            LoadValidated();
            SelectedPerson = ValidatedItems.FirstOrDefault();

            IsAddingProfile = false;
        }

        [RelayCommand] private void SearchNotYet() => LoadNotYet();
        [RelayCommand] private void SearchValidated() => LoadValidated();

        // ===== Profile image upload/remove =====

        // ✅ Profile image upload
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

        // ✅ ADD PROFILE (new)
        [RelayCommand]
        private void AddProfile()
        {
            // Ensure we are in Not Yet Validated tab
            ActiveMainTab = ValidatorsMainTab.NotYetValidated;

            // Clear search so the new record won't be hidden
            SearchNotYetText = "";

            // reload list first (so we insert into the current view)
            LoadNotYet();

            // Generate new ids
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

            LoadReleaseHistoryForSelected();   // ✅ add this

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

        // ===== Save profile confirm modal =====
        [RelayCommand]
        private void OpenSaveConfirm()
        {
            if (SelectedPerson is null) return;
            IsSaveConfirmOpen = true;
        }

        [RelayCommand] private void CloseSaveConfirm() => IsSaveConfirmOpen = false;

        [RelayCommand]
        private void ConfirmSaveProfile()
        {
            var person = SelectedPerson;
            if (person is null) return;

            // Save Profile rule:
            // - if already validated => keep current validated status
            // - else => set to Not Validated
            var current = CanonicalStatus(person.Status);
            var statusToSave = IsValidatedStatus(current) ? current : "Not Validated";

            _repo.Upsert(person, statusToSave);
            person.Status = statusToSave;

            IsSaveConfirmOpen = false;

            var bene = person.BeneficiaryId;

            // refresh lists so overlay stays correct
            LoadNotYet();
            LoadValidated();

            // keep selection on the saved row if present
            SelectedPerson =
                NotYetItems.FirstOrDefault(x => string.Equals(x.BeneficiaryId, bene, StringComparison.OrdinalIgnoreCase))
                ?? ValidatedItems.FirstOrDefault(x => string.Equals(x.BeneficiaryId, bene, StringComparison.OrdinalIgnoreCase))
                ?? NotYetItems.FirstOrDefault()
                ?? ValidatedItems.FirstOrDefault();

            IsAddingProfile = false;
        }

        // ===== Validate modal =====
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

        [RelayCommand] private void CloseValidateModal() => IsValidateModalOpen = false;

        [RelayCommand]
        private void ConfirmValidate()
        {
            var person = SelectedPerson;
            if (person is null) return;

            var newStatus = CanonicalStatus(ValidateSelectedStatus);
            if (!IsValidatedStatus(newStatus)) return;

            // write to DB
            person.Status = newStatus;
            _repo.Upsert(person, newStatus);

            // switch to Validated tab + correct status pill
            ActiveMainTab = ValidatorsMainTab.Validated;
            ActiveStatusTab = newStatus switch
            {
                "Pending" => ValidatorsStatusTab.Pending,
                "Rejected" => ValidatorsStatusTab.Rejected,
                _ => ValidatorsStatusTab.Endorsed
            };

            // refresh lists
            LoadNotYet();
            LoadValidated();

            // keep selection on the newly validated row if present
            SelectedPerson =
                ValidatedItems.FirstOrDefault(x => string.Equals(x.BeneficiaryId, person.BeneficiaryId, StringComparison.OrdinalIgnoreCase))
                ?? ValidatedItems.FirstOrDefault();

            IsValidateModalOpen = false;
            IsAddingProfile = false;
        }

        // ===== Helpers =====

        private int GetNextBeneNumber()
        {
            // Start from external max
            var max = _externalPeople.Count == 0 ? 0 : _externalPeople.Max(x => ExtractDigits(x.BeneficiaryId));

            // Include DB rows across statuses to avoid duplicates
            foreach (var st in new[] { "Not Validated", "Endorsed", "Pending", "Rejected" })
            {
                var rows = _repo.GetByStatus(st) ?? new List<ValidatorRecord>();
                if (rows.Count > 0)
                    max = Math.Max(max, rows.Max(x => ExtractDigits(x.BeneficiaryId)));
            }

            // Fallback
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

            // must exist in beneficiaries table to have releases
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
