using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Diagnostics;
using System.Globalization;

namespace WpfApp3.Services
{
    public sealed class ReleaseReportService
    {
        private const string Primary = "#4F46E5";
        private const string PrimarySoft = "#EEF2FF";
        private const string Border = "#E7ECF5";
        private const string TextPrimary = "#0F172A";
        private const string TextSecondary = "#64748B";

        static ReleaseReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public string? PickSavePath(string suggestedFileName)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                DefaultExt = ".pdf",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = suggestedFileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void GeneratePdf(string filePath, ReleaseReportData data)
        {
            var classificationMax = Math.Max(
                1,
                data.ClassificationBreakdown.Count == 0 ? 1 : data.ClassificationBreakdown.Max(x => x.Count));

            var barangayMax = Math.Max(
                1,
                data.BarangayBreakdown.Count == 0 ? 1 : data.BarangayBreakdown.Max(x => x.Count));

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
                        col.Item().Text("Release Session Report").FontSize(20).Bold();
                        col.Item().Text(data.ProjectName).FontSize(14).SemiBold();
                        col.Item().Text($"Generated: {data.GeneratedAt:MMMM dd, yyyy hh:mm tt}")
                            .FontColor(TextSecondary);
                        col.Item().Text($"Classification Filter: {data.ClassificationFilter}")
                            .FontColor(TextSecondary);
                    });

                    page.Content().PaddingTop(10).Column(col =>
                    {
                        col.Spacing(16);
                        col.Item().Element(c => ComposeSummaryCards(c, data));
                        col.Item().Element(c => ComposeReleaseStatus(c, data));

                        col.Item().Row(row =>
                        {
                            row.RelativeItem()
                                .Element(c => ComposeMetricPanel(
                                    c,
                                    "Classification Breakdown",
                                    data.ClassificationBreakdown,
                                    classificationMax,
                                    Primary));

                            row.ConstantItem(12);

                            row.RelativeItem()
                                .Element(c => ComposeMetricPanel(
                                    c,
                                    "Top Barangays",
                                    data.BarangayBreakdown,
                                    barangayMax,
                                    "#16A34A"));
                        });

                        col.Item().Element(c => ComposeBeneficiaryTable(c, data));
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

        public void Print(string filePath)
        {
            Process.Start(new ProcessStartInfo(filePath)
            {
                UseShellExecute = true,
                Verb = "print"
            });
        }

        private static void ComposeSummaryCards(IContainer container, ReleaseReportData data)
        {
            container.Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => ComposeCard(c, "Total Budget", data.TotalBudgetText));
                row.RelativeItem().Element(c => ComposeCard(
                    c,
                    "Beneficiaries",
                    data.TotalBeneficiaries.ToString("N0", CultureInfo.InvariantCulture)));
                row.RelativeItem().Element(c => ComposeCard(
                    c,
                    "Released",
                    $"{data.ReleasedCount:N0} ({data.ReleaseRate:0.0}%)"));
                row.RelativeItem().Element(c => ComposeCard(
                    c,
                    "Outstanding",
                    $"{data.PendingCount:N0} ({100 - data.ReleaseRate:0.0}%)"));
            });
        }

        private static void ComposeCard(IContainer container, string title, string value)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .Background("#F8FAFC")
                .CornerRadius(10)
                .Padding(12)
                .Column(col =>
                {
                    col.Item().Text(title).FontColor(TextSecondary).FontSize(9);
                    col.Item().PaddingTop(4).Text(value).FontSize(13).SemiBold();
                });
        }

        private static void ComposeReleaseStatus(IContainer container, ReleaseReportData data)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .CornerRadius(12)
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Release Status Overview").FontSize(12).SemiBold();
                    col.Item().Text(
                            $"Released amount: {data.ReleasedAmountText}    Pending amount: {data.PendingAmountText}")
                        .FontColor(TextSecondary)
                        .FontSize(9);

                    col.Item().Row(row =>
                    {
                        row.ConstantItem(430).Height(18).Layers(layers =>
                        {
                            var totalWidth = 430f;
                            var releasedWidth = data.TotalBeneficiaries == 0
                                ? 0
                                : totalWidth * data.ReleasedCount / (float)data.TotalBeneficiaries;

                            // MUST always exist
                            layers.PrimaryLayer()
                                .Background("#E2E8F0")
                                .CornerRadius(9);

                            if (releasedWidth > 0)
                            {
                                layers.Layer()
                                    .AlignLeft()
                                    .Width(releasedWidth)
                                    .Background(Primary)
                                    .CornerRadius(9);
                            }
                        });

                        row.ConstantItem(12);

                        row.RelativeItem().Column(inner =>
                        {
                            inner.Item().Text($"{data.ReleasedCount} released").SemiBold();
                            inner.Item().Text($"{data.PendingCount} pending")
                                .FontColor(TextSecondary)
                                .FontSize(9);
                        });
                    });
                });
        }

        private static void ComposeMetricPanel(
    IContainer container,
    string title,
    IReadOnlyList<ReleaseMetricItem> items,
    int maxCount,
    string accentColor)
        {
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
                                var filledWidth = maxCount == 0 ? 0 : 220f * item.Count / maxCount;

                                // MUST always exist
                                layers.PrimaryLayer()
                                    .Background("#E2E8F0")
                                    .CornerRadius(6);

                                if (filledWidth > 0)
                                {
                                    layers.Layer()
                                        .AlignLeft()
                                        .Width(filledWidth)
                                        .Background(accentColor)
                                        .CornerRadius(6);
                                }
                            });

                            row.ConstantItem(10);

                            row.RelativeItem()
                                .AlignRight()
                                .Text($"{item.Count} • {item.Percent:0.0}%")
                                .FontSize(9);
                        });
                    }
                });
        }

        private static void ComposeBeneficiaryTable(IContainer container, ReleaseReportData data)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .CornerRadius(12)
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Detailed Beneficiary List").FontSize(12).SemiBold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(95);
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(1.4f);
                            columns.RelativeColumn(1.4f);
                            columns.ConstantColumn(90);
                            columns.ConstantColumn(85);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Background(PrimarySoft).Text("Beneficiary ID").SemiBold().FontSize(9);
                            header.Cell().Element(CellStyle).Background(PrimarySoft).Text("Name").SemiBold().FontSize(9);
                            header.Cell().Element(CellStyle).Background(PrimarySoft).Text("Barangay").SemiBold().FontSize(9);
                            header.Cell().Element(CellStyle).Background(PrimarySoft).Text("Classification").SemiBold().FontSize(9);
                            header.Cell().Element(CellStyle).Background(PrimarySoft).Text("Share").SemiBold().FontSize(9);
                            header.Cell().Element(CellStyle).Background(PrimarySoft).Text("Status").SemiBold().FontSize(9);
                        });

                        foreach (var row in data.Beneficiaries)
                        {
                            table.Cell().Element(CellStyle).Text(row.BeneficiaryId).FontSize(9);
                            table.Cell().Element(CellStyle).Text(row.FullName).FontSize(9);
                            table.Cell().Element(CellStyle).Text(row.Barangay).FontSize(9);
                            table.Cell().Element(CellStyle).Text(row.Classification).FontSize(9);
                            table.Cell().Element(CellStyle).Text(row.ShareText).FontSize(9);
                            table.Cell().Element(CellStyle).Text(row.ReleasedText).FontSize(9);
                        }
                    });
                });
        }


        public void Open(string filePath)
        {
            Process.Start(new ProcessStartInfo(filePath)
            {
                UseShellExecute = true,
                Verb = "open"
            });
        }

        private static IContainer CellStyle(IContainer container)
        {
            return container
                .BorderBottom(1)
                .BorderColor(Border)
                .PaddingVertical(8)
                .PaddingHorizontal(6);
        }
    }

    public sealed class ReleaseReportData
    {
        public string ProjectName { get; set; } = "";
        public string TotalBudgetText { get; set; } = "-";
        public string ClassificationFilter { get; set; } = "All";
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public int TotalBeneficiaries { get; set; }
        public int ReleasedCount { get; set; }
        public int PendingCount { get; set; }
        public decimal ReleasedAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public List<ReleaseMetricItem> ClassificationBreakdown { get; set; } = new();
        public List<ReleaseMetricItem> BarangayBreakdown { get; set; } = new();
        public List<ReleaseBeneficiaryItem> Beneficiaries { get; set; } = new();

        public double ReleaseRate => TotalBeneficiaries == 0
            ? 0
            : ReleasedCount * 100d / TotalBeneficiaries;

        public string ReleasedAmountText => $"₱ {ReleasedAmount:N2}";
        public string PendingAmountText => $"₱ {PendingAmount:N2}";
    }

    public sealed class ReleaseMetricItem
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public double Percent { get; set; }
    }

    public sealed class ReleaseBeneficiaryItem
    {
        public string BeneficiaryId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Barangay { get; set; } = "";
        public string Classification { get; set; } = "";
        public string ShareText { get; set; } = "";
        public string ReleasedText { get; set; } = "";
    }
}