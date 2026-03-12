using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveCharts;
using LiveCharts.Wpf;
using System.Globalization;
using System.Windows.Media;
using WpfApp3.Services;

namespace WpfApp3.ViewModels.Dashboard
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly DashboardRepository _repo = new();
        private readonly DashboardPdfService _pdf = new();

        [ObservableProperty] private string totalAllotmentAmount = "₱ 0.00";
        [ObservableProperty] private int beneficiariesCount;
        [ObservableProperty] private int projectsCount;
        [ObservableProperty] private int releasedCount;
        [ObservableProperty] private int pendingReleaseCount;
        [ObservableProperty] private string statusSummary = "0 Released • 0 Pending";
        [ObservableProperty] private bool isLoading;

        public SeriesCollection BeneficiariesPieSeries { get; } = new();
        public SeriesCollection YearlyAllotmentSeries { get; } = new();
        public SeriesCollection ProjectHistorySeries { get; } = new();

        [ObservableProperty] private string[] yearlyLabels = Array.Empty<string>();
        [ObservableProperty] private string[] monthLabels = Array.Empty<string>();

        public Func<double, string> MoneyFormatter { get; }
        public Func<double, string> CountFormatter { get; }

        public DashboardViewModel()
        {
            MoneyFormatter = value => $"₱{value:N0}";
            CountFormatter = value => value.ToString("N0", CultureInfo.InvariantCulture);

            _ = LoadDashboardAsync();
        }

        [RelayCommand]
        private async Task Refresh()
        {
            await LoadDashboardAsync();
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

        private async Task LoadDashboardAsync()
        {
            if (IsLoading)
                return;

            IsLoading = true;

            try
            {
                var data = await Task.Run(() => _repo.GetSnapshot());

                TotalAllotmentAmount = $"₱ {data.TotalAllotmentAmount:N2}";
                BeneficiariesCount = data.BeneficiariesCount;
                ProjectsCount = data.ProjectsCount;
                ReleasedCount = data.ReleasedCount;
                PendingReleaseCount = data.PendingReleaseCount;
                StatusSummary = $"{data.ReleasedCount:N0} Released • {data.PendingReleaseCount:N0} Pending";

                BuildPie(data);
                BuildYearlyLine(data);
                BuildMonthlyArea(data);
            }
            catch
            {
                TotalAllotmentAmount = "₱ 0.00";
                BeneficiariesCount = 0;
                ProjectsCount = 0;
                ReleasedCount = 0;
                PendingReleaseCount = 0;
                StatusSummary = "0 Released • 0 Pending";

                BeneficiariesPieSeries.Clear();
                YearlyAllotmentSeries.Clear();
                ProjectHistorySeries.Clear();
                YearlyLabels = Array.Empty<string>();
                MonthLabels = Array.Empty<string>();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void BuildPie(DashboardSnapshot data)
        {
            BeneficiariesPieSeries.Clear();

            var fills = new[]
            {
                "#3498DB",
                "#FF6B6B",
                "#F4B400",
                "#607D8B",
                "#6C5CE7",
                "#2ECC71"
            };

            for (int i = 0; i < data.BeneficiaryClassification.Count; i++)
            {
                var item = data.BeneficiaryClassification[i];

                BeneficiariesPieSeries.Add(new PieSeries
                {
                    Title = item.Label,
                    Values = new ChartValues<double> { item.Value },
                    DataLabels = true,
                    LabelPoint = cp => cp.Y.ToString("N0", CultureInfo.InvariantCulture),
                    Fill = (SolidColorBrush)new BrushConverter().ConvertFromString(fills[i % fills.Length])!
                });
            }

            OnPropertyChanged(nameof(BeneficiariesPieSeries));
        }

        private void BuildYearlyLine(DashboardSnapshot data)
        {
            YearlyAllotmentSeries.Clear();

            YearlyLabels = data.YearlyAllotments.Select(x => x.Label).ToArray();

            YearlyAllotmentSeries.Add(new LineSeries
            {
                Title = "Allotment",
                Values = new ChartValues<double>(data.YearlyAllotments.Select(x => x.Value)),
                PointGeometrySize = 9,
                StrokeThickness = 3,
                LineSmoothness = 0.15,
                Fill = new SolidColorBrush(Color.FromArgb(40, 52, 152, 219)),
                Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString("#3498DB")!,
                PointForeground = (SolidColorBrush)new BrushConverter().ConvertFromString("#3498DB")!
            });

            OnPropertyChanged(nameof(YearlyAllotmentSeries));
        }

        private void BuildMonthlyArea(DashboardSnapshot data)
        {
            ProjectHistorySeries.Clear();

            MonthLabels = data.MonthlyProjects.Select(x => x.Label).ToArray();

            ProjectHistorySeries.Add(new LineSeries
            {
                Title = "Projects",
                Values = new ChartValues<double>(data.MonthlyProjects.Select(x => x.Value)),
                PointGeometry = null,
                StrokeThickness = 3,
                LineSmoothness = 0.5,
                Stroke = (SolidColorBrush)new BrushConverter().ConvertFromString("#2D9CDB")!,
                Fill = new SolidColorBrush(Color.FromArgb(55, 66, 99, 235))
            });

            OnPropertyChanged(nameof(ProjectHistorySeries));
        }
    }
}