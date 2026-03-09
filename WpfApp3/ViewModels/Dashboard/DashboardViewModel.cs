using CommunityToolkit.Mvvm.ComponentModel;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Windows.Media;
// heyheyhey
namespace WpfApp3.ViewModels.Dashboard
{
    public partial class DashboardViewModel : ObservableObject
    {
        // KPI tilesss
        [ObservableProperty] private string totalAllotmentAmount = "₱ 2,500,000";
        [ObservableProperty] private int beneficiariesCount = 1250;
        [ObservableProperty] private int projectsCount = 20;

        // PIE
        public SeriesCollection BeneficiariesPieSeries { get; }
        public Func<ChartPoint, string> PieLabelPoint { get; }

        // YEARLY LINE
        public SeriesCollection YearlyAllotmentSeries { get; }
        public string[] YearlyLabels { get; }
        public Func<double, string> MoneyFormatter { get; }

        // PROJECT HISTORY (AREA)
        public SeriesCollection ProjectHistorySeries { get; }
        public string[] MonthLabels { get; }
        public Func<double, string> CountFormatter { get; }

        public DashboardViewModel()
        {
            // --- Pie chart (dummy % split)
            BeneficiariesPieSeries = new SeriesCollection
            {
                new PieSeries { Title = "PWD", Values = new ChartValues<double> { 30 }, DataLabels = true },
                new PieSeries { Title = "Scholars", Values = new ChartValues<double> { 15 }, DataLabels = true },
                new PieSeries { Title = "Others", Values = new ChartValues<double> { 35 }, DataLabels = true },
                new PieSeries { Title = "Indigenous", Values = new ChartValues<double> { 20 }, DataLabels = true },
            };

            PieLabelPoint = cp => $"{cp.SeriesView.Title}\n{cp.Participation:P0}";

            // --- Yearly allotment line (dummy values)
            YearlyAllotmentSeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Allotment",
                    Values = new ChartValues<double> { 300000, 1400000, 800000, 3800000, 1000000, 2200000 },
                    PointGeometrySize = 10,
                    LineSmoothness = 0.2
                }
            };

            YearlyLabels = new[] { "2020", "2021", "2022", "2023", "2024", "2025" };
            MoneyFormatter = value => $"₱{value:N0}";

            // --- Project history (dummy monthly trend)
            // This is an "area" by setting Fill.
            ProjectHistorySeries = new SeriesCollection
            {
                new LineSeries
                {
                    Title = "Projects",
                    Values = new ChartValues<double> { 120, 340, 260, 480, 430, 780, 220, 560, 260, 640 },
                    PointGeometry = null,
                    StrokeThickness = 3,
                    LineSmoothness = 0.6,
                    Fill = new SolidColorBrush(Color.FromArgb(40, 66, 99, 235)) // soft translucent fill
                }
            };

            MonthLabels = new[] { "Jul", "Aug", "Sep", "Oct", "Nov", "Dec", "Jan", "Feb", "Mar", "Apr" };
            CountFormatter = value => value.ToString("N0");
        }
    }
}
