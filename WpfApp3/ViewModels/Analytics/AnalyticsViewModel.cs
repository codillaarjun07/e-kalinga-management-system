using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Analytics
{
    public partial class AnalyticsViewModel : ObservableObject
    {
        private readonly DashboardRepository _repo = new();
        private readonly DashboardPdfService _pdf = new();

        private decimal _totalAllotmentAmountValue;

        [ObservableProperty] private string totalAllotmentAmount = "₱ 0.00";
        [ObservableProperty] private int beneficiariesCount;
        [ObservableProperty] private int projectsCount;
        [ObservableProperty] private int releasedCount;
        [ObservableProperty] private int pendingReleaseCount;
        [ObservableProperty] private string statusSummary = "0 Released • 0 Pending";
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string lastUpdatedText = "Not yet refreshed";

        public SeriesCollection BeneficiariesPieSeries { get; } = new();
        public SeriesCollection YearlyAllotmentSeries { get; } = new();
        public SeriesCollection ProjectHistorySeries { get; } = new();

        [ObservableProperty] private string[] yearlyLabels = Array.Empty<string>();
        [ObservableProperty] private string[] monthLabels = Array.Empty<string>();

        public Func<double, string> MoneyFormatter { get; }
        public Func<double, string> CountFormatter { get; }

        public int TotalAssignmentsCount =>
            ReleasedCount + PendingReleaseCount;

        public double ReleaseRateValue =>
            TotalAssignmentsCount <= 0
                ? 0d
                : ReleasedCount * 100d / TotalAssignmentsCount;

        public string ReleaseRateText =>
            $"{ReleaseRateValue:N1}%";

        public string ReleasedSummaryText =>
            $"{ReleasedCount:N0} released";

        public string PendingSummaryText =>
            $"{PendingReleaseCount:N0} pending";

        public string AverageAllocationText =>
            ProjectsCount <= 0
                ? "₱ 0.00"
                : $"₱ {_totalAllotmentAmountValue / ProjectsCount:N2}";

        public string BeneficiariesPerProjectText =>
            ProjectsCount <= 0
                ? "0.0"
                : ((double)BeneficiariesCount / ProjectsCount)
                    .ToString("N1", CultureInfo.InvariantCulture);

        public AnalyticsViewModel()
        {
            MoneyFormatter = value => $"₱{value:N0}";
            CountFormatter = value =>
                value.ToString("N0", CultureInfo.InvariantCulture);

            _ = LoadAnalyticsAsync();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadAnalyticsAsync();
        }

        [RelayCommand]
        private void ExportPdf()
        {
            try
            {
                var snapshot = _repo.GetSnapshot();
                var path = _pdf.PickSavePath();

                if (string.IsNullOrWhiteSpace(path))
                    return;

                _pdf.GeneratePdf(path, snapshot);
                _pdf.OpenFile(path);
            }
            catch
            {
            }
        }

        private async Task LoadAnalyticsAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                var data = await Task.Run(() => _repo.GetSnapshot());

                _totalAllotmentAmountValue =
                    data.TotalAllotmentAmount;

                TotalAllotmentAmount =
                    $"₱ {data.TotalAllotmentAmount:N2}";

                BeneficiariesCount =
                    data.BeneficiariesCount;

                ProjectsCount =
                    data.ProjectsCount;

                ReleasedCount =
                    data.ReleasedCount;

                PendingReleaseCount =
                    data.PendingReleaseCount;

                StatusSummary =
                    $"{data.ReleasedCount:N0} Released • " +
                    $"{data.PendingReleaseCount:N0} Pending";

                LastUpdatedText =
                    $"Last updated: " +
                    $"{DateTime.Now:MMMM dd, yyyy • hh:mm tt}";

                BuildPie(data);
                BuildYearlyColumns(data);
                BuildMonthlyArea(data);
                NotifyDerivedAnalytics();
            }
            catch
            {
                _totalAllotmentAmountValue = 0m;

                TotalAllotmentAmount = "₱ 0.00";
                BeneficiariesCount = 0;
                ProjectsCount = 0;
                ReleasedCount = 0;
                PendingReleaseCount = 0;
                StatusSummary = "0 Released • 0 Pending";
                LastUpdatedText = "Could not load analytics.";

                BeneficiariesPieSeries.Clear();
                YearlyAllotmentSeries.Clear();
                ProjectHistorySeries.Clear();

                YearlyLabels = Array.Empty<string>();
                MonthLabels = Array.Empty<string>();

                NotifyDerivedAnalytics();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void NotifyDerivedAnalytics()
        {
            OnPropertyChanged(nameof(TotalAssignmentsCount));
            OnPropertyChanged(nameof(ReleaseRateValue));
            OnPropertyChanged(nameof(ReleaseRateText));
            OnPropertyChanged(nameof(ReleasedSummaryText));
            OnPropertyChanged(nameof(PendingSummaryText));
            OnPropertyChanged(nameof(AverageAllocationText));
            OnPropertyChanged(nameof(BeneficiariesPerProjectText));
        }

        private void BuildPie(DashboardSnapshot data)
        {
            BeneficiariesPieSeries.Clear();

            foreach (var item in data.BeneficiaryClassification)
            {
                BeneficiariesPieSeries.Add(new PieSeries
                {
                    Title = item.Label,
                    Values = new ChartValues<double>
                    {
                        item.Value
                    },
                    DataLabels = true,
                    LabelPoint = chartPoint =>
                        chartPoint.Y.ToString(
                            "N0",
                            CultureInfo.InvariantCulture),
                    Fill = GetClassificationBrush(
                        item.Label)
                });
            }

            OnPropertyChanged(
                nameof(BeneficiariesPieSeries));
        }

        private void BuildYearlyColumns(
            DashboardSnapshot data)
        {
            YearlyAllotmentSeries.Clear();

            YearlyLabels =
                data.YearlyAllotments
                    .Select(x => x.Label)
                    .ToArray();

            YearlyAllotmentSeries.Add(
                new ColumnSeries
                {
                    Title = "Monetary Allotment",
                    Values =
                        new ChartValues<double>(
                            data.YearlyAllotments
                                .Select(x => x.Value)),
                    Fill = CreateBrush("#1F2A44"),
                    DataLabels = true,
                    LabelPoint = chartPoint =>
                        $"₱{chartPoint.Y:N0}"
                });

            OnPropertyChanged(
                nameof(YearlyAllotmentSeries));
        }

        private void BuildMonthlyArea(
            DashboardSnapshot data)
        {
            ProjectHistorySeries.Clear();

            MonthLabels =
                data.MonthlyProjects
                    .Select(x => x.Label)
                    .ToArray();

            ProjectHistorySeries.Add(
                new LineSeries
                {
                    Title = "Projects",
                    Values =
                        new ChartValues<double>(
                            data.MonthlyProjects
                                .Select(x => x.Value)),
                    PointGeometry =
                        DefaultGeometries.Circle,
                    PointGeometrySize = 7,
                    StrokeThickness = 3,
                    LineSmoothness = 0.35,
                    Stroke = CreateBrush("#2563EB"),
                    PointForeground =
                        CreateBrush("#1F2A44"),
                    Fill =
                        new SolidColorBrush(
                            Color.FromArgb(
                                38,
                                37,
                                99,
                                235))
                });

            OnPropertyChanged(
                nameof(ProjectHistorySeries));
        }

        private static SolidColorBrush
            GetClassificationBrush(
                string? classification)
        {
            var value =
                (classification ?? "")
                    .Trim();

            if (value.Equals(
                "PWD",
                StringComparison.OrdinalIgnoreCase))
            {
                return CreateBrush("#7C3AED");
            }

            if (value.Equals(
                "Farmer",
                StringComparison.OrdinalIgnoreCase))
            {
                return CreateBrush("#16A34A");
            }

            if (value.Equals(
                "Vendor",
                StringComparison.OrdinalIgnoreCase))
            {
                return CreateBrush("#2563EB");
            }

            if (value.Equals(
                "Senior Citizen",
                StringComparison.OrdinalIgnoreCase))
            {
                return CreateBrush("#D97706");
            }

            if (
                value.Equals(
                    "Indigenous",
                    StringComparison.OrdinalIgnoreCase) ||
                value.Equals(
                    "Indigenous People",
                    StringComparison.OrdinalIgnoreCase))
            {
                return CreateBrush("#0F766E");
            }

            return CreateBrush("#94A3B8");
        }

        private static SolidColorBrush CreateBrush(
            string color)
        {
            return (SolidColorBrush)
                new BrushConverter()
                    .ConvertFromString(color)!;
        }
    }
}