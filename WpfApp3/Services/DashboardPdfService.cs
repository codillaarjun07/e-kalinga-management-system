using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Diagnostics;
using System.Globalization;

namespace WpfApp3.Services
{
    public class DashboardPdfService
    {
        private const string Border = "#E7ECF5";
        private const string TextPrimary = "#0F172A";
        private const string TextSecondary = "#64748B";
        private const string Accent = "#2E3A59";

        static DashboardPdfService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string? PickSavePath()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = $"dashboard-report-{DateTime.Now:yyyyMMdd-HHmm}.pdf"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void GeneratePdf(string filePath, DashboardSnapshot data)
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(28);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(TextPrimary));

                    page.Header().Column(col =>
                    {
                        col.Spacing(4);
                        col.Item().Text("Dashboard Summary Report").FontSize(20).Bold();
                        col.Item().Text($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontColor(TextSecondary);
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(14);

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c => ComposeCard(c, "Total Allotment Amount", $"₱ {data.TotalAllotmentAmount:N2}"));
                            row.ConstantItem(10);
                            row.RelativeItem().Element(c => ComposeCard(c, "Number of Beneficiaries", data.BeneficiariesCount.ToString("N0", CultureInfo.InvariantCulture)));
                            row.ConstantItem(10);
                            row.RelativeItem().Element(c => ComposeCard(c, "Number of Projects", data.ProjectsCount.ToString("N0", CultureInfo.InvariantCulture)));
                            row.ConstantItem(10);
                            row.RelativeItem().Element(c => ComposeCard(c, "Released / Pending", $"{data.ReleasedCount:N0} / {data.PendingReleaseCount:N0}"));
                        });

                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Element(c => ComposeMetricPanel(
                                c,
                                "Beneficiary Classifications",
                                data.BeneficiaryClassification.Select(x => new PdfMetricItem
                                {
                                    Label = x.Label,
                                    Value = (int)x.Value
                                }).ToList(),
                                "#2563EB"));

                            row.ConstantItem(12);

                            row.RelativeItem().Element(c => ComposeMetricPanel(
                                c,
                                "Yearly Allotment Totals",
                                data.YearlyAllotments.Select(x => new PdfMetricItem
                                {
                                    Label = x.Label,
                                    ValueText = $"₱ {x.Value:N0}"
                                }).ToList(),
                                "#16A34A"));
                        });

                        col.Item().Element(c => ComposeMetricPanel(
                            c,
                            "Projects Created Per Month",
                            data.MonthlyProjects.Select(x => new PdfMetricItem
                            {
                                Label = x.Label,
                                Value = (int)x.Value
                            }).ToList(),
                            Accent));
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf(filePath);
        }

        public void OpenFile(string filePath)
        {
            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        private static void ComposeCard(IContainer container, string title, string value)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background("#F8FAFC")
                .CornerRadius(12)
                .Padding(14)
                .Column(col =>
                {
                    col.Item().Text(title).FontColor(TextSecondary).FontSize(9);
                    col.Item().PaddingTop(4).Text(value).FontSize(14).SemiBold();
                });
        }

        private static void ComposeMetricPanel(IContainer container, string title, IReadOnlyList<PdfMetricItem> items, string accent)
        {
            var max = Math.Max(1, items.Count == 0 ? 1 : items.Max(x => x.Value));

            container
                .Border(1)
                .BorderColor(Border)
                .CornerRadius(12)
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text(title).FontSize(12).SemiBold();

                    if (items.Count == 0)
                    {
                        col.Item().Text("No data available.").FontColor(TextSecondary);
                        return;
                    }

                    foreach (var item in items)
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(110).Text(item.Label).FontSize(9);

                            row.ConstantItem(220).Height(12).Layers(layers =>
                            {
                                var width = max == 0 ? 0 : 220f * item.Value / max;

                                layers.PrimaryLayer()
                                    .Background("#E2E8F0")
                                    .CornerRadius(6);

                                if (width > 0)
                                {
                                    layers.Layer()
                                        .AlignLeft()
                                        .Width(width)
                                        .Background(accent)
                                        .CornerRadius(6);
                                }
                            });

                            row.ConstantItem(10);

                            row.RelativeItem()
                                .AlignRight()
                                .Text(string.IsNullOrWhiteSpace(item.ValueText) ? item.Value.ToString("N0", CultureInfo.InvariantCulture) : item.ValueText)
                                .FontSize(9);
                        });
                    }
                });
        }

        private sealed class PdfMetricItem
        {
            public string Label { get; set; } = "";
            public int Value { get; set; }
            public string ValueText { get; set; } = "";
        }
    }
}