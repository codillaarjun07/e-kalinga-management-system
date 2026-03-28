using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using WpfApp3.Models;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.AuditLogs
{
    public partial class AuditLogsViewModel : ObservableObject
    {
        private readonly AuditLogsService _service = new();
        private readonly List<AuditLogRecord> _all = new();

        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string selectedTab = "ALL";
        [ObservableProperty] private string searchText = "";
        [ObservableProperty] private string statusMessage = "";
        [ObservableProperty] private string statusBrush = "#64748B";
        [ObservableProperty] private int currentPage = 1;

        public int PageSize { get; } = 8;

        public ObservableCollection<AuditLogRecord> Items { get; } = new();
        public ObservableCollection<int> PageNumbers { get; } = new();

        public int TotalRecords => Filtered().Count;
        public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize));

        public string FoundText => $"Found {TotalRecords} records";
        public string PageSummaryText => $"Page {CurrentPage} of {TotalPages}";
        public string PagingStatusText => TotalRecords == 0
            ? "Showing 0 of 0 records"
            : $"Showing {((CurrentPage - 1) * PageSize) + 1}-{Math.Min(CurrentPage * PageSize, TotalRecords)} of {TotalRecords} records";

        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        public AuditLogsViewModel()
        {
            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
                _ = LoadAsync();
            else
                Apply();
        }

        partial void OnSearchTextChanged(string value)
        {
            CurrentPage = 1;
            Apply();
        }

        partial void OnSelectedTabChanged(string value)
        {
            CurrentPage = 1;
            Apply();
        }

        partial void OnCurrentPageChanged(int value)
        {
            Apply();
        }

        [RelayCommand]
        private void SelectTab(string? tab)
        {
            if (string.IsNullOrWhiteSpace(tab))
                return;

            SelectedTab = tab.Trim().ToUpperInvariant();
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            await LoadAsync();
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CanGoPrevious)
                CurrentPage--;
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CanGoNext)
                CurrentPage++;
        }

        [RelayCommand]
        private void GoToPage(int page)
        {
            if (page < 1 || page > TotalPages)
                return;

            CurrentPage = page;
        }

        private async Task LoadAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;
            try
            {
                var logs = await Task.Run(() =>
                {
                    _service.EnsureAuditLogsTable();
                    return _service.GetAll();
                });

                _all.Clear();
                _all.AddRange(logs);

                CurrentPage = 1;
                Apply();

                StatusMessage = $"Loaded {_all.Count} audit logs.";
                StatusBrush = "#16A34A";
            }
            catch (Exception ex)
            {
                _all.Clear();
                CurrentPage = 1;
                Apply();

                StatusMessage = $"Failed to load audit logs: {ex.Message}";
                StatusBrush = "#E11D48";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<AuditLogRecord> Filtered()
        {
            IEnumerable<AuditLogRecord> query = _all;

            var q = (SearchText ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(x =>
                    x.Id.ToString(CultureInfo.InvariantCulture).Contains(q) ||
                    NormalizeOperationType(x.OperationType).ToLowerInvariant().Contains(q) ||
                    (x.OperationType ?? "").ToLowerInvariant().Contains(q) ||
                    (x.TableName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.RecordId ?? "").ToLowerInvariant().Contains(q) ||
                    (x.ActorName ?? "").ToLowerInvariant().Contains(q) ||
                    (x.Description ?? "").ToLowerInvariant().Contains(q) ||
                    (x.CreatedAtText ?? "").ToLowerInvariant().Contains(q));
            }

            query = SelectedTab switch
            {
                "CREATE" => query.Where(x => IsCreateType(x.OperationType)),
                "UPDATE" => query.Where(x => IsUpdateType(x.OperationType)),
                "DELETE" => query.Where(x => IsDeleteType(x.OperationType)),
                _ => query
            };

            return query
                .OrderByDescending(x => x.Id)
                .ToList();
        }

        private void Apply()
        {
            var filtered = Filtered();

            if (CurrentPage < 1)
                CurrentPage = 1;

            var totalPages = Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)PageSize));
            if (CurrentPage > totalPages)
                CurrentPage = totalPages;

            Items.Clear();
            foreach (var item in filtered
                         .Skip((CurrentPage - 1) * PageSize)
                         .Take(PageSize))
            {
                item.OperationType = NormalizeOperationType(item.OperationType);
                Items.Add(item);
            }

            PageNumbers.Clear();
            for (int i = 1; i <= totalPages; i++)
                PageNumbers.Add(i);

            OnPropertyChanged(nameof(TotalRecords));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(FoundText));
            OnPropertyChanged(nameof(PageSummaryText));
            OnPropertyChanged(nameof(PagingStatusText));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }

        private static bool IsCreateType(string? value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized is "CREATE" or "INSERT" or "ADDED" or "ADD";
        }

        private static bool IsUpdateType(string? value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized is "UPDATE" or "EDIT" or "MODIFY" or "MODIFIED";
        }

        private static bool IsDeleteType(string? value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized is "DELETE" or "REMOVE" or "REMOVED";
        }

        private static string NormalizeOperationType(string? value)
        {
            if (IsCreateType(value)) return "CREATE";
            if (IsUpdateType(value)) return "UPDATE";
            if (IsDeleteType(value)) return "DELETE";
            return (value ?? "").Trim().ToUpperInvariant();
        }
    }
}