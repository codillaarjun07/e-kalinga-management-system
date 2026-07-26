#!/usr/bin/env node
'use strict';

// E-Kalinga complete Distribution patch.
// Includes:
// 1. Manual beneficiary ID/internal ID release entry.
// 2. Separate Waiting and Released lists.
// 3. Released-only PDF reports.
// 4. Search on the main Distribution queue and Release Station queue.
// 5. Beneficiary profile pictures in Distribution and Beneficiaries rows.
//
// Dirty working trees are allowed.
// This script does not build, commit, or push.

console.log('Applying complete E-Kalinga Distribution patch...');
console.log('This single file contains all requested patch stages.\n');

// Stage 1: release workflow, split lists, and released-only reporting.
{
'use strict';

const fs = require('fs');
const path = require('path');

const MARKER = 'EKALINGA_DISTRIBUTION_RELEASE_SPLIT_V1';
const root = process.cwd();

const files = {
  vm: path.join(root, 'WpfApp3', 'ViewModels', 'Distribution', 'DistributionViewModel.cs'),
  xaml: path.join(root, 'WpfApp3', 'Views', 'Distribution', 'DistributionView.xaml'),
  codeBehind: path.join(root, 'WpfApp3', 'Views', 'Distribution', 'DistributionView.xaml.cs'),
  report: path.join(root, 'WpfApp3', 'Services', 'ReleaseReportService.cs'),
};

function fail(message) {
  throw new Error(message);
}

function readSource(file) {
  if (!fs.existsSync(file)) {
    fail(`Required file not found: ${path.relative(root, file)}`);
  }

  const raw = fs.readFileSync(file, 'utf8');
  return {
    file,
    hadBom: raw.charCodeAt(0) === 0xfeff,
    eol: raw.includes('\r\n') ? '\r\n' : '\n',
    text: raw.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n'),
  };
}

function replaceOnce(text, search, replacement, label) {
  const first = text.indexOf(search);
  if (first < 0) {
    fail(`Patch anchor not found: ${label}`);
  }
  if (text.indexOf(search, first + search.length) >= 0) {
    fail(`Patch anchor is not unique: ${label}`);
  }
  return text.slice(0, first) + replacement + text.slice(first + search.length);
}

function replaceRegexOnce(text, regex, replacement, label) {
  const match = text.match(regex);
  if (!match) {
    fail(`Patch pattern not found: ${label}`);
  }
  return text.replace(regex, replacement);
}

function restoreSource(source, text) {
  const rendered = text.replace(/\n/g, source.eol);
  return (source.hadBom ? '\uFEFF' : '') + rendered;
}

function patchViewModel(source) {
  let text = source.text;

  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`namespace WpfApp3.ViewModels.Distribution
{`,
`namespace WpfApp3.ViewModels.Distribution
{
    // ${MARKER}`,
    'DistributionViewModel marker'
  );

  text = replaceOnce(
    text,
`        private void SetDistributionStatus(string? status)
        {
            SelectedDistributionStatus =
                string.IsNullOrWhiteSpace(status)
                    ? "All"
                    : status.Trim();
        }`,
`        private void SetDistributionStatus(string? status)
        {
            SelectedDistributionStatus =
                string.IsNullOrWhiteSpace(status)
                    ? "Waiting"
                    : status.Trim();
        }

        [RelayCommand]
        private void SetReleaseStatus(string? status)
        {
            ReleaseSelectedStatus =
                string.IsNullOrWhiteSpace(status)
                    ? "Waiting"
                    : status.Trim();
        }`,
    'status commands'
  );

  text = replaceOnce(
    text,
`        public ObservableCollection<string> DistributionStatusOptions { get; } = new()
        {
            "All",
            "Waiting",
            "Released"
        };

        [ObservableProperty] private string? selectedDistributionStatus = "All";`,
`        public ObservableCollection<string> DistributionStatusOptions { get; } = new()
        {
            "Waiting",
            "Released"
        };

        [ObservableProperty] private string? selectedDistributionStatus = "Waiting";`,
    'main distribution status options'
  );

  text = replaceOnce(
    text,
`        [ObservableProperty] private string? releaseSelectedClassification = "All";

        [ObservableProperty] private BeneficiaryRecord? pendingRelease;`,
`        public ObservableCollection<string> ReleaseStatusOptions { get; } = new()
        {
            "Waiting",
            "Released"
        };

        [ObservableProperty] private string? releaseSelectedStatus = "Waiting";
        [ObservableProperty] private string? releaseSelectedClassification = "All";

        public string ReleaseWaitingTabText => $"Waiting ({ReleaseItems.Count(x => !x.IsReleased)})";
        public string ReleaseReleasedTabText => $"Released ({ReleaseItems.Count(x => x.IsReleased)})";

        [ObservableProperty] private BeneficiaryRecord? pendingRelease;`,
    'release status fields'
  );

  text = replaceOnce(
    text,
`            OnPropertyChanged(nameof(ReleaseProgressText));
        }`,
`            OnPropertyChanged(nameof(ReleaseProgressText));
            OnPropertyChanged(nameof(ReleaseWaitingTabText));
            OnPropertyChanged(nameof(ReleaseReleasedTabText));
        }`,
    'release status notifications'
  );

  text = replaceOnce(
    text,
`            List<BeneficiaryRecord> reportRows;
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
            }`,
`            List<BeneficiaryRecord> reportRows;
            string activeFilter;

            if (IsReleaseSessionOpen)
            {
                if (ReleaseItems.Count == 0)
                    ReloadReleaseItems();

                activeFilter = NormalizeLabel(ReleaseSelectedClassification, "All");
                reportRows = ReleaseItems
                    .Where(x => x.IsReleased)
                    .Where(x => MatchesClassification(x, ReleaseSelectedClassification))
                    .ToList();
            }
            else
            {
                activeFilter = NormalizeLabel(SelectedClassification, "All");
                reportRows = _cache
                    .Where(x => x.IsReleased)
                    .Where(x => MatchesClassification(x, SelectedClassification))
                    .ToList();
            }

            if (reportRows.Count == 0)
            {
                ShowToast("No released beneficiaries are available for this report.", "warning");
                return;
            }`,
    'released-only report rows'
  );

  text = replaceOnce(
    text,
`            ReleaseCurrentPage = 1;     // ✅ start at page 1
            ReleaseSelectedClassification = SelectedClassification ?? "All";
            ReloadReleaseItems();`,
`            ReleaseCurrentPage = 1;
            ReleaseSelectedStatus = "Waiting";
            ReleaseSelectedClassification = SelectedClassification ?? "All";
            ReloadReleaseItems();`,
    'release session defaults'
  );

  text = replaceOnce(
    text,
`            // ✅ compare as-is (case-insensitive)
            var hit = ReleaseItems.FirstOrDefault(x =>
                string.Equals((x.BeneficiaryId ?? "").Trim(), raw, StringComparison.OrdinalIgnoreCase));`,
`            // Match either the public beneficiary ID/barcode or the internal numeric record ID.
            var hit = ReleaseItems.FirstOrDefault(x =>
                string.Equals((x.BeneficiaryId ?? "").Trim(), raw, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Id.ToString(CultureInfo.InvariantCulture), raw, StringComparison.OrdinalIgnoreCase));`,
    'manual or scanner ID lookup'
  );

  text = replaceOnce(
    text,
`            if (hit is null)
            {
                ShowToast($"Scan not found: {raw}", "error");
                return;
            }`,
`            if (hit is null)
            {
                ShowToast($"Beneficiary ID not found: {raw}", "error");
                return;
            }`,
    'lookup failure wording'
  );

  text = replaceOnce(
    text,
`            IsConfirmReleaseOpen = true;
            ShowToast($"Scan success: {raw}", "success");`,
`            IsConfirmReleaseOpen = true;
            ShowToast($"Beneficiary found: {raw}", "success");`,
    'lookup success wording'
  );

  text = replaceOnce(
    text,
`        partial void OnReleaseSelectedClassificationChanged(string? value)
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
        }`,
`        partial void OnReleaseSelectedClassificationChanged(string? value)
        {
            if (!_ready) return;
            ReleaseCurrentPage = 1;
            ApplyReleasePaging();
        }

        partial void OnReleaseSelectedStatusChanged(string? value)
        {
            if (!_ready) return;
            ReleaseCurrentPage = 1;
            ApplyReleasePaging();
        }

        private IEnumerable<BeneficiaryRecord> ReleaseFiltered()
        {
            IEnumerable<BeneficiaryRecord> src = ReleaseItems;

            var status = (ReleaseSelectedStatus ?? "Waiting").Trim();
            src = status.Equals("Released", StringComparison.OrdinalIgnoreCase)
                ? src.Where(x => x.IsReleased)
                : src.Where(x => !x.IsReleased);

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
        }`,
    'release list separation'
  );

  text = replaceOnce(
    text,
`        private ReleaseReportData BuildReleaseReportData(List<BeneficiaryRecord> rows, string classificationFilter)
        {`,
`        private static bool MatchesClassification(BeneficiaryRecord row, string? selectedClassification)
        {
            var selected = (selectedClassification ?? "All").Trim();
            if (string.IsNullOrWhiteSpace(selected) || selected.Equals("All", StringComparison.OrdinalIgnoreCase))
                return true;

            var actual = (row.Classification ?? "").Trim();
            if (selected.Equals("None", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(actual) || actual.Equals("None", StringComparison.OrdinalIgnoreCase);

            return actual.Equals(selected, StringComparison.OrdinalIgnoreCase);
        }

        private ReleaseReportData BuildReleaseReportData(List<BeneficiaryRecord> rows, string classificationFilter)
        {`,
    'classification helper'
  );

  return text;
}

function patchXaml(source) {
  let text = source.text;

  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`<UserControl x:Class="WpfApp3.Views.Distribution.DistributionView"`,
`<!-- ${MARKER} -->
<UserControl x:Class="WpfApp3.Views.Distribution.DistributionView"`,
    'DistributionView XAML marker'
  );

  text = replaceOnce(
    text,
`                    <ColumnDefinition Width="350" />`,
`                    <ColumnDefinition Width="390" />`,
    'scanner panel width'
  );

  text = replaceOnce(
    text,
`                        <StackPanel Grid.Row="1" Margin="0,24,0,0">
                            <TextBlock Text="SCANNED BENEFICIARY ID" Foreground="{DynamicResource ThemeBrush_Info_C9D3E7}" FontSize="10" FontWeight="Bold" Margin="0,0,0,8" />

                            <Border Height="54" CornerRadius="14" Background="{DynamicResource ThemeBrush_Surface_FFFFFF}" BorderBrush="{DynamicResource ThemeBrush_Glass_22FFFFFF}" BorderThickness="1" Padding="16,0">
                                <Grid>
                                    <TextBox IsReadOnly="True" IsTabStop="False" Cursor="Arrow" Text="{Binding ScanInput, UpdateSourceTrigger=PropertyChanged}" Background="Transparent" BorderThickness="0" Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}" FontSize="16" FontWeight="Bold" VerticalContentAlignment="Center" />

                                    <TextBlock Text="Scan now..." Foreground="{DynamicResource ThemeBrush_TextSecondary_94A3B8}" FontSize="14" VerticalAlignment="Center" IsHitTestVisible="False">
                                        <TextBlock.Style>
                                            <Style TargetType="TextBlock">
                                                <Setter Property="Visibility" Value="Collapsed" />
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding ScanInput}" Value="">
                                                        <Setter Property="Visibility" Value="Visible" />
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding ScanInput}" Value="{x:Null}">
                                                        <Setter Property="Visibility" Value="Visible" />
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </TextBlock.Style>
                                    </TextBlock>
                                </Grid>
                            </Border>
                        </StackPanel>`,
`                        <StackPanel Grid.Row="1" Margin="0,24,0,0">
                            <TextBlock Text="SCAN OR ENTER BENEFICIARY ID" Foreground="{DynamicResource ThemeBrush_Info_C9D3E7}" FontSize="10" FontWeight="Bold" Margin="0,0,0,8" />

                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="10" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>

                                <Border Height="54" CornerRadius="14" Background="{DynamicResource ThemeBrush_Surface_FFFFFF}" BorderBrush="{DynamicResource ThemeBrush_Glass_22FFFFFF}" BorderThickness="1" Padding="16,0">
                                    <Grid>
                                        <TextBox x:Name="ManualReleaseIdTextBox"
                                                 Text="{Binding ScanInput, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                 Background="Transparent"
                                                 BorderThickness="0"
                                                 Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}"
                                                 CaretBrush="{DynamicResource ThemeBrush_AccentText_1F2A44}"
                                                 FontSize="15"
                                                 FontWeight="SemiBold"
                                                 VerticalContentAlignment="Center"
                                                 PreviewKeyDown="ManualReleaseIdTextBox_PreviewKeyDown" />

                                        <TextBlock Text="Scan barcode or type ID..."
                                                   Foreground="{DynamicResource ThemeBrush_TextSecondary_94A3B8}"
                                                   FontSize="13"
                                                   VerticalAlignment="Center"
                                                   IsHitTestVisible="False">
                                            <TextBlock.Style>
                                                <Style TargetType="TextBlock">
                                                    <Setter Property="Visibility" Value="Collapsed" />
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding ScanInput}" Value="">
                                                            <Setter Property="Visibility" Value="Visible" />
                                                        </DataTrigger>
                                                        <DataTrigger Binding="{Binding ScanInput}" Value="{x:Null}">
                                                            <Setter Property="Visibility" Value="Visible" />
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </TextBlock.Style>
                                        </TextBlock>
                                    </Grid>
                                </Border>

                                <Button Grid.Column="2"
                                        MinWidth="82"
                                        Height="54"
                                        Style="{StaticResource LightButton}"
                                        Padding="14,0"
                                        Command="{Binding ScanCommand}"
                                        CommandParameter="{Binding ScanInput}"
                                        Content="Verify" />
                            </Grid>

                            <TextBlock Text="Use the beneficiary barcode ID or the internal numeric record ID. Press Enter or click Verify."
                                       Foreground="{DynamicResource ThemeBrush_Info_C9D3E7}"
                                       FontSize="10"
                                       TextWrapping="Wrap"
                                       Margin="0,8,0,0" />
                        </StackPanel>`,
    'manual beneficiary ID input'
  );

  text = replaceOnce(
    text,
`                            <TextBlock Text="Scanner instructions" Foreground="{DynamicResource ThemeBrush_TextPrimary_FFFFFF}" FontSize="13" FontWeight="SemiBold" />
                            <TextBlock Text="Keep this window active, then scan the beneficiary barcode or type through the configured scanner. Verify the profile before confirming release." Foreground="{DynamicResource ThemeBrush_Info_C9D3E7}" FontSize="12" LineHeight="19" TextWrapping="Wrap" Margin="0,10,0,0" />`,
`                            <TextBlock Text="Release instructions" Foreground="{DynamicResource ThemeBrush_TextPrimary_FFFFFF}" FontSize="13" FontWeight="SemiBold" />
                            <TextBlock Text="Scan a beneficiary barcode or manually type the beneficiary ID. Verify the beneficiary profile and allocated share before confirming the release." Foreground="{DynamicResource ThemeBrush_Info_C9D3E7}" FontSize="12" LineHeight="19" TextWrapping="Wrap" Margin="0,10,0,0" />`,
    'release instructions'
  );

  text = replaceOnce(
    text,
`                                <TextBlock Text="Release Queue" Foreground="{DynamicResource ThemeBrush_AccentText_0F172A}" FontSize="18" FontWeight="Bold" />
                                <TextBlock Text="Released beneficiaries remain visible and are dimmed." Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}" FontSize="11" Margin="0,4,0,0" />`,
`                                <TextBlock Text="Release Queue" Foreground="{DynamicResource ThemeBrush_AccentText_0F172A}" FontSize="18" FontWeight="Bold" />
                                <TextBlock Text="Waiting and released beneficiaries are maintained in separate lists." Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}" FontSize="11" Margin="0,4,0,0" />`,
    'release queue subtitle'
  );

  text = replaceOnce(
    text,
`                            <Border HorizontalAlignment="Right" VerticalAlignment="Center" Background="{DynamicResource ThemeBrush_Accent_EEF2FF}" CornerRadius="12" Padding="12,7">
                                <TextBlock Text="{Binding ReleaseProgressText}" Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}" FontSize="11" FontWeight="SemiBold" />
                            </Border>`,
`                            <StackPanel HorizontalAlignment="Right" VerticalAlignment="Center" Orientation="Horizontal">
                                <Border Background="{DynamicResource ThemeBrush_InfoSoft_F8FAFC}"
                                        BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}"
                                        BorderThickness="1"
                                        CornerRadius="12"
                                        Padding="4"
                                        Margin="0,0,10,0">
                                    <StackPanel Orientation="Horizontal">
                                        <Button Content="{Binding ReleaseWaitingTabText}"
                                                Command="{Binding SetReleaseStatusCommand}"
                                                CommandParameter="Waiting">
                                            <Button.Style>
                                                <Style TargetType="Button" BasedOn="{StaticResource SegmentButtonStyle}">
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding ReleaseSelectedStatus}" Value="Waiting">
                                                            <Setter Property="Background" Value="{DynamicResource ThemeBrush_WarningSoft_FFF7E8}" />
                                                            <Setter Property="Foreground" Value="{DynamicResource ThemeBrush_Warning_B45309}" />
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </Button.Style>
                                        </Button>

                                        <Button Content="{Binding ReleaseReleasedTabText}"
                                                Command="{Binding SetReleaseStatusCommand}"
                                                CommandParameter="Released">
                                            <Button.Style>
                                                <Style TargetType="Button" BasedOn="{StaticResource SegmentButtonStyle}">
                                                    <Style.Triggers>
                                                        <DataTrigger Binding="{Binding ReleaseSelectedStatus}" Value="Released">
                                                            <Setter Property="Background" Value="{DynamicResource ThemeBrush_SuccessSoft_ECFDF3}" />
                                                            <Setter Property="Foreground" Value="{DynamicResource ThemeBrush_Success_15803D}" />
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </Button.Style>
                                        </Button>
                                    </StackPanel>
                                </Border>

                                <Border Background="{DynamicResource ThemeBrush_Accent_EEF2FF}" CornerRadius="12" Padding="12,7">
                                    <TextBlock Text="{Binding ReleaseProgressText}" Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}" FontSize="11" FontWeight="SemiBold" />
                                </Border>
                            </StackPanel>`,
    'release queue tabs'
  );

  text = replaceOnce(
    text,
`                                        <DataTrigger Binding="{Binding IsReleased}" Value="True">
                                            <Setter Property="Opacity" Value="0.50" />
                                        </DataTrigger>
`,
``,
    'remove released-row dimming'
  );

  return text;
}

function patchCodeBehind(source) {
  let text = source.text;

  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`namespace WpfApp3.Views.Distribution
{`,
`namespace WpfApp3.Views.Distribution
{
    // ${MARKER}`,
    'DistributionView code-behind marker'
  );

  text = replaceOnce(
    text,
`        private void HookVm()
        {`,
`        private void ManualReleaseIdTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return)
                return;

            if (DataContext is DistributionViewModel vm)
            {
                var value = (vm.ScanInput ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(value) && vm.ScanCommand.CanExecute(value))
                    vm.ScanCommand.Execute(value);
            }

            e.Handled = true;
        }

        private void HookVm()
        {`,
    'manual ID Enter handler'
  );

  text = replaceOnce(
    text,
`            if (vm.IsConfirmReleaseOpen)
            {
                e.Handled = true;
                return;
            }

            _scanBuffer.Append(e.Text);`,
`            if (vm.IsConfirmReleaseOpen)
            {
                e.Handled = true;
                return;
            }

            // Allow normal keyboard typing while the manual ID field is focused.
            if (ManualReleaseIdTextBox?.IsKeyboardFocusWithin == true)
                return;

            _scanBuffer.Append(e.Text);`,
    'manual input text bypass'
  );

  text = replaceOnce(
    text,
`            if (vm.IsConfirmReleaseOpen)
            {
                if (e.Key != Key.Enter && e.Key != Key.Return && e.Key != Key.Escape)
                    e.Handled = true;

                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)`,
`            if (vm.IsConfirmReleaseOpen)
            {
                if (e.Key != Key.Enter && e.Key != Key.Return && e.Key != Key.Escape)
                    e.Handled = true;

                return;
            }

            // The TextBox handles manual entry and Enter itself.
            if (ManualReleaseIdTextBox?.IsKeyboardFocusWithin == true)
                return;

            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)`,
    'manual input key bypass'
  );

  return text;
}

function patchReportService(source) {
  let text = source.text;

  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`namespace WpfApp3.Services
{`,
`namespace WpfApp3.Services
{
    // ${MARKER}`,
    'ReleaseReportService marker'
  );

  text = replaceOnce(
    text,
`                    col.Item().Text("Release Session Report").FontSize(20).Bold();`,
`                    col.Item().Text("Released Beneficiaries Report").FontSize(20).Bold();`,
    'report title'
  );

  text = replaceRegexOnce(
    text,
/        private static void ComposeSummaryCards\(IContainer container, ReleaseReportData data\)\n        \{[\s\S]*?\n        \}\n\n        private static void ComposeCard/,
`        private static void ComposeSummaryCards(IContainer container, ReleaseReportData data)
        {
            container.Row(row =>
            {
                row.Spacing(10);
                row.RelativeItem().Element(c => ComposeCard(c, "Total Budget", data.TotalBudgetText));
                row.RelativeItem().Element(c => ComposeCard(
                    c,
                    "Released Beneficiaries",
                    data.ReleasedCount.ToString("N0", CultureInfo.InvariantCulture)));
                row.RelativeItem().Element(c => ComposeCard(
                    c,
                    "Released Allocation",
                    data.ReleasedAmountText));
                row.RelativeItem().Element(c => ComposeCard(
                    c,
                    "Generated",
                    data.GeneratedAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)));
            });
        }

        private static void ComposeCard`,
    'released-only summary cards'
  );

  text = replaceRegexOnce(
    text,
/        private static void ComposeReleaseStatus\(IContainer container, ReleaseReportData data\)\n        \{[\s\S]*?\n        \}\n\n        private static void ComposeMetricPanel/,
`        private static void ComposeReleaseStatus(IContainer container, ReleaseReportData data)
        {
            container
                .Border(1)
                .BorderColor(Border)
                .CornerRadius(12)
                .Padding(12)
                .Column(col =>
                {
                    col.Spacing(8);
                    col.Item().Text("Released Beneficiary Summary").FontSize(12).SemiBold();
                    col.Item().Text(
                            $"This report contains only released beneficiaries. Total released: {data.ReleasedCount:N0}.")
                        .FontColor(TextSecondary)
                        .FontSize(9);
                    col.Item().Text($"Released allocation: {data.ReleasedAmountText}")
                        .FontSize(11)
                        .SemiBold();
                });
        }

        private static void ComposeMetricPanel`,
    'released-only status summary'
  );

  return text;
}

function main() {
  console.log('Applying E-Kalinga Distribution release patch...');
  console.log('Dirty working trees are allowed. No build, commit, or push will be performed.');

  const sources = {
    vm: readSource(files.vm),
    xaml: readSource(files.xaml),
    codeBehind: readSource(files.codeBehind),
    report: readSource(files.report),
  };

  const outputs = {
    vm: patchViewModel(sources.vm),
    xaml: patchXaml(sources.xaml),
    codeBehind: patchCodeBehind(sources.codeBehind),
    report: patchReportService(sources.report),
  };

  const changed = Object.keys(outputs).filter(key => outputs[key] !== sources[key].text);
  if (changed.length === 0) {
    console.log('Patch is already applied. No files changed.');
    return;
  }

  // All transformations completed successfully before any file is written.
  for (const key of changed) {
    fs.writeFileSync(sources[key].file, restoreSource(sources[key], outputs[key]), 'utf8');
    console.log(`Updated: ${path.relative(root, sources[key].file)}`);
  }

  console.log('\nPatch applied successfully.');
  console.log('Review with: git diff -- WpfApp3/Views/Distribution WpfApp3/ViewModels/Distribution WpfApp3/Services/ReleaseReportService.cs');
  console.log('No build, commit, or push was run.');
}

try {
  main();
} catch (error) {
  console.error(`\nPatch failed: ${error.message}`);
  console.error('No files were written unless the failure occurred during the final write stage.');
  process.exitCode = 1;
}
}

if (process.exitCode) {
  process.exit(process.exitCode);
}

console.log('\n============================================================\n');

// Stage 2: queue search and profile-picture support.
{
'use strict';

const fs = require('fs');
const path = require('path');

const MARKER = 'EKALINGA_DISTRIBUTION_SEARCH_PROFILE_V2';
const root = process.cwd();

const files = {
  distributionVm: path.join(root, 'WpfApp3', 'ViewModels', 'Distribution', 'DistributionViewModel.cs'),
  distributionXaml: path.join(root, 'WpfApp3', 'Views', 'Distribution', 'DistributionView.xaml'),
  distributionCodeBehind: path.join(root, 'WpfApp3', 'Views', 'Distribution', 'DistributionView.xaml.cs'),
  beneficiaryModel: path.join(root, 'WpfApp3', 'Models', 'BeneficiaryRecord.cs'),
  assignmentRepo: path.join(root, 'WpfApp3', 'Services', 'AllotmentBeneficiariesRepository.cs'),
  beneficiariesXaml: path.join(root, 'WpfApp3', 'Views', 'Beneficiaries', 'BeneficiariesView.xaml'),
};

function fail(message) {
  throw new Error(message);
}

function readSource(file) {
  if (!fs.existsSync(file)) {
    fail(`Required file not found: ${path.relative(root, file)}`);
  }

  const raw = fs.readFileSync(file, 'utf8');
  return {
    file,
    hadBom: raw.charCodeAt(0) === 0xfeff,
    eol: raw.includes('\r\n') ? '\r\n' : '\n',
    text: raw.replace(/^\uFEFF/, '').replace(/\r\n/g, '\n'),
  };
}

function restoreSource(source, text) {
  const rendered = text.replace(/\n/g, source.eol);
  return (source.hadBom ? '\uFEFF' : '') + rendered;
}

function replaceOnce(text, search, replacement, label) {
  const first = text.indexOf(search);
  if (first < 0) fail(`Patch anchor not found: ${label}`);
  if (text.indexOf(search, first + search.length) >= 0) {
    fail(`Patch anchor is not unique: ${label}`);
  }
  return text.slice(0, first) + replacement + text.slice(first + search.length);
}

function replaceAllChecked(text, search, replacement, minimumCount, label) {
  let count = 0;
  let index = 0;
  while ((index = text.indexOf(search, index)) >= 0) {
    count++;
    index += search.length;
  }

  if (count < minimumCount) {
    fail(`Patch anchor count too low for ${label}: expected at least ${minimumCount}, found ${count}`);
  }

  return text.split(search).join(replacement);
}

function patchDistributionViewModel(source) {
  let text = source.text;
  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`namespace WpfApp3.ViewModels.Distribution
{`,
`namespace WpfApp3.ViewModels.Distribution
{
    // ${MARKER}`,
    'DistributionViewModel marker'
  );

  text = replaceOnce(
    text,
`        [ObservableProperty] private int currentPage = 1;
        [ObservableProperty] private bool isLoading;`,
`        [ObservableProperty] private int currentPage = 1;
        [ObservableProperty] private bool isLoading;
        [ObservableProperty] private string distributionSearchText = "";`,
    'main distribution search property'
  );

  text = replaceOnce(
    text,
`        // shows scanned text in UI textbox
        [ObservableProperty] private string scanInput = "";`,
`        // shows scanned or manually entered beneficiary ID
        [ObservableProperty] private string scanInput = "";
        [ObservableProperty] private string releaseSearchText = "";`,
    'release search property'
  );

  text = replaceOnce(
    text,
`        partial void OnSelectedClassificationChanged(string? value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            ApplyPaging();
        }`,
`        partial void OnSelectedClassificationChanged(string? value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            ApplyPaging();
        }

        partial void OnDistributionSearchTextChanged(string value)
        {
            if (!_ready) return;
            CurrentPage = 1;
            ApplyPaging();
        }`,
    'main search change handler'
  );

  text = replaceOnce(
    text,
`            if (status.Equals("Released", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => x.IsReleased);
            else if (status.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => !x.IsReleased);

            return src.ToList();`,
`            if (status.Equals("Released", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => x.IsReleased);
            else if (status.Equals("Waiting", StringComparison.OrdinalIgnoreCase))
                src = src.Where(x => !x.IsReleased);

            var search = (DistributionSearchText ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                src = src.Where(x =>
                    ContainsIgnoreCase(x.FirstName, search) ||
                    ContainsIgnoreCase(x.LastName, search) ||
                    ContainsIgnoreCase($"{x.FirstName} {x.LastName}", search) ||
                    ContainsIgnoreCase(x.BeneficiaryId, search));
            }

            return src.ToList();`,
    'main queue name search filter'
  );

  text = replaceOnce(
    text,
`            ReleaseCurrentPage = 1;
            ReleaseSelectedStatus = "Waiting";
            ReleaseSelectedClassification = SelectedClassification ?? "All";
            ReloadReleaseItems();`,
`            ReleaseCurrentPage = 1;
            ReleaseSelectedStatus = "Waiting";
            ReleaseSelectedClassification = SelectedClassification ?? "All";
            ReleaseSearchText = "";
            ReloadReleaseItems();`,
    'clear release search when opening station'
  );

  text = replaceOnce(
    text,
`        partial void OnReleaseSelectedStatusChanged(string? value)
        {
            if (!_ready) return;
            ReleaseCurrentPage = 1;
            ApplyReleasePaging();
        }`,
`        partial void OnReleaseSelectedStatusChanged(string? value)
        {
            if (!_ready) return;
            ReleaseCurrentPage = 1;
            ApplyReleasePaging();
        }

        partial void OnReleaseSearchTextChanged(string value)
        {
            if (!_ready) return;
            ReleaseCurrentPage = 1;
            ApplyReleasePaging();
        }`,
    'release search change handler'
  );

  text = replaceOnce(
    text,
`            return src;
        }


        // paging (main page)`,
`            var search = (ReleaseSearchText ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                src = src.Where(x =>
                    ContainsIgnoreCase(x.FirstName, search) ||
                    ContainsIgnoreCase(x.LastName, search) ||
                    ContainsIgnoreCase($"{x.FirstName} {x.LastName}", search) ||
                    ContainsIgnoreCase(x.BeneficiaryId, search));
            }

            return src;
        }


        // paging (main page)`,
    'release station name search filter'
  );

  return text;
}

function beneficiaryAvatar(size, iconSize, gridColumn = '') {
  const gridAttribute = gridColumn ? ` ${gridColumn}` : '';
  return `<Grid${gridAttribute} Width="${size}" Height="${size}" VerticalAlignment="Center">
                                                        <Ellipse Fill="{DynamicResource ThemeBrush_Accent_EEF2FF}" />
                                                        <Ellipse>
                                                            <Ellipse.Fill>
                                                                <ImageBrush ImageSource="{Binding ProfileImagePreview}" Stretch="UniformToFill" />
                                                            </Ellipse.Fill>
                                                            <Ellipse.Style>
                                                                <Style TargetType="Ellipse">
                                                                    <Setter Property="Visibility" Value="Collapsed" />
                                                                    <Style.Triggers>
                                                                        <DataTrigger Binding="{Binding HasProfileImage}" Value="True">
                                                                            <Setter Property="Visibility" Value="Visible" />
                                                                        </DataTrigger>
                                                                    </Style.Triggers>
                                                                </Style>
                                                            </Ellipse.Style>
                                                        </Ellipse>
                                                        <TextBlock FontFamily="Segoe MDL2 Assets"
                                                                   Text=""
                                                                   Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}"
                                                                   FontSize="${iconSize}"
                                                                   HorizontalAlignment="Center"
                                                                   VerticalAlignment="Center">
                                                            <TextBlock.Style>
                                                                <Style TargetType="TextBlock">
                                                                    <Setter Property="Visibility" Value="Visible" />
                                                                    <Style.Triggers>
                                                                        <DataTrigger Binding="{Binding HasProfileImage}" Value="True">
                                                                            <Setter Property="Visibility" Value="Collapsed" />
                                                                        </DataTrigger>
                                                                    </Style.Triggers>
                                                                </Style>
                                                            </TextBlock.Style>
                                                        </TextBlock>
                                                    </Grid>`;
}

function patchDistributionXaml(source) {
  let text = source.text;
  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`<!-- EKALINGA_DISTRIBUTION_RELEASE_SPLIT_V1 -->
<UserControl x:Class="WpfApp3.Views.Distribution.DistributionView"`,
`<!-- EKALINGA_DISTRIBUTION_RELEASE_SPLIT_V1 -->
<!-- ${MARKER} -->
<UserControl x:Class="WpfApp3.Views.Distribution.DistributionView"`,
    'Distribution XAML V2 marker'
  );

  text = replaceOnce(
    text,
`                            <StackPanel>
                                <TextBlock Text="Distribution Queue" Foreground="{DynamicResource ThemeBrush_AccentText_0F172A}" FontSize="18" FontWeight="Bold" />
                                <TextBlock Text="{Binding FoundText}" Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}" FontSize="11" Margin="0,4,0,0" />
                            </StackPanel>`,
`                            <StackPanel>
                                <TextBlock Text="Distribution Queue" Foreground="{DynamicResource ThemeBrush_AccentText_0F172A}" FontSize="18" FontWeight="Bold" />
                                <TextBlock Text="{Binding FoundText}" Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}" FontSize="11" Margin="0,4,0,0" />

                                <Border Width="280"
                                        Height="38"
                                        Margin="0,10,0,0"
                                        HorizontalAlignment="Left"
                                        CornerRadius="11"
                                        BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}"
                                        BorderThickness="1"
                                        Background="{DynamicResource ThemeBrush_InfoSoft_FCFDFE}"
                                        Padding="11,0">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="24" />
                                            <ColumnDefinition Width="*" />
                                        </Grid.ColumnDefinitions>
                                        <TextBlock FontFamily="Segoe MDL2 Assets"
                                                   Text=""
                                                   Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}"
                                                   FontSize="12"
                                                   VerticalAlignment="Center" />
                                        <Grid Grid.Column="1">
                                            <TextBox Text="{Binding DistributionSearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource PlainInputTextBox}"
                                                     FontSize="12" />
                                            <TextBlock Text="Search beneficiary name..."
                                                       Foreground="{DynamicResource ThemeBrush_TextSecondary_94A3B8}"
                                                       FontSize="12"
                                                       VerticalAlignment="Center"
                                                       IsHitTestVisible="False">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Visibility" Value="Collapsed" />
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding DistributionSearchText}" Value="">
                                                                <Setter Property="Visibility" Value="Visible" />
                                                            </DataTrigger>
                                                            <DataTrigger Binding="{Binding DistributionSearchText}" Value="{x:Null}">
                                                                <Setter Property="Visibility" Value="Visible" />
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                        </Grid>
                                    </Grid>
                                </Border>
                            </StackPanel>`,
    'main distribution search box'
  );

  text = replaceOnce(
    text,
`                            <StackPanel>
                                <TextBlock Text="Release Queue" Foreground="{DynamicResource ThemeBrush_AccentText_0F172A}" FontSize="18" FontWeight="Bold" />
                                <TextBlock Text="Waiting and released beneficiaries are maintained in separate lists." Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}" FontSize="11" Margin="0,4,0,0" />
                            </StackPanel>`,
`                            <StackPanel>
                                <TextBlock Text="Release Queue" Foreground="{DynamicResource ThemeBrush_AccentText_0F172A}" FontSize="18" FontWeight="Bold" />
                                <TextBlock Text="Waiting and released beneficiaries are maintained in separate lists." Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}" FontSize="11" Margin="0,4,0,0" />

                                <Border Width="300"
                                        Height="38"
                                        Margin="0,10,0,0"
                                        HorizontalAlignment="Left"
                                        CornerRadius="11"
                                        BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}"
                                        BorderThickness="1"
                                        Background="{DynamicResource ThemeBrush_InfoSoft_FCFDFE}"
                                        Padding="11,0">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="24" />
                                            <ColumnDefinition Width="*" />
                                        </Grid.ColumnDefinitions>
                                        <TextBlock FontFamily="Segoe MDL2 Assets"
                                                   Text=""
                                                   Foreground="{DynamicResource ThemeBrush_TextSecondary_64748B}"
                                                   FontSize="12"
                                                   VerticalAlignment="Center" />
                                        <Grid Grid.Column="1">
                                            <TextBox x:Name="ReleaseQueueSearchTextBox"
                                                     Text="{Binding ReleaseSearchText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                                     Style="{StaticResource PlainInputTextBox}"
                                                     FontSize="12" />
                                            <TextBlock Text="Search beneficiary name..."
                                                       Foreground="{DynamicResource ThemeBrush_TextSecondary_94A3B8}"
                                                       FontSize="12"
                                                       VerticalAlignment="Center"
                                                       IsHitTestVisible="False">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Visibility" Value="Collapsed" />
                                                        <Style.Triggers>
                                                            <DataTrigger Binding="{Binding ReleaseSearchText}" Value="">
                                                                <Setter Property="Visibility" Value="Visible" />
                                                            </DataTrigger>
                                                            <DataTrigger Binding="{Binding ReleaseSearchText}" Value="{x:Null}">
                                                                <Setter Property="Visibility" Value="Visible" />
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                        </Grid>
                                    </Grid>
                                </Border>
                            </StackPanel>`,
    'release station search box'
  );

  text = replaceOnce(
    text,
`                            <Border Grid.Column="1" Background="{DynamicResource ThemeBrush_InfoSoft_F8FAFC}" BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}" BorderThickness="1" CornerRadius="12" Padding="4" Margin="0,0,10,0">`,
`                            <Border Grid.Column="1" Background="{DynamicResource ThemeBrush_InfoSoft_F8FAFC}" BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}" BorderThickness="1" CornerRadius="12" Padding="4" Margin="0,0,10,0" VerticalAlignment="Center">`,
    'main status tabs alignment'
  );

  text = replaceOnce(
    text,
`                            <Border Grid.Column="2" Width="150" CornerRadius="12" BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}" BorderThickness="1" Background="{DynamicResource ThemeBrush_InfoSoft_FCFDFE}">`,
`                            <Border Grid.Column="2" Width="150" CornerRadius="12" BorderBrush="{DynamicResource ThemeBrush_Border_E7ECF5}" BorderThickness="1" Background="{DynamicResource ThemeBrush_InfoSoft_FCFDFE}" VerticalAlignment="Center">`,
    'main classification alignment'
  );

  const oldAvatar = `<Border Width="32" Height="32" CornerRadius="16" Background="{DynamicResource ThemeBrush_Accent_EEF2FF}" VerticalAlignment="Center"><TextBlock FontFamily="Segoe MDL2 Assets" Text="" Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}" FontSize="15" HorizontalAlignment="Center" VerticalAlignment="Center" /></Border>`;
  const newAvatar = beneficiaryAvatar(32, 15).replace(/\n\s*/g, ' ').replace(/\s{2,}/g, ' ').trim();
  text = replaceAllChecked(text, oldAvatar, newAvatar, 2, 'Distribution beneficiary row avatars');

  return text;
}

function patchDistributionCodeBehind(source) {
  let text = source.text;
  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`namespace WpfApp3.Views.Distribution
{
    // EKALINGA_DISTRIBUTION_RELEASE_SPLIT_V1`,
`namespace WpfApp3.Views.Distribution
{
    // EKALINGA_DISTRIBUTION_RELEASE_SPLIT_V1
    // ${MARKER}`,
    'Distribution code-behind V2 marker'
  );

  text = replaceAllChecked(
    text,
`            if (ManualReleaseIdTextBox?.IsKeyboardFocusWithin == true)
                return;`,
`            if (ManualReleaseIdTextBox?.IsKeyboardFocusWithin == true ||
                ReleaseQueueSearchTextBox?.IsKeyboardFocusWithin == true)
                return;`,
    2,
    'release search scanner bypass'
  );

  return text;
}

function patchBeneficiaryModel(source) {
  let text = source.text;
  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`using CommunityToolkit.Mvvm.ComponentModel;`,
`using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Windows.Media.Imaging;`,
    'BeneficiaryRecord image usings'
  );

  text = replaceOnce(
    text,
`namespace WpfApp3.Models
{`,
`namespace WpfApp3.Models
{
    // ${MARKER}`,
    'BeneficiaryRecord marker'
  );

  text = replaceOnce(
    text,
`        [ObservableProperty] private string civilRegistryId = "";
        [ObservableProperty] private string middleName = "";
        [ObservableProperty] private string presentAddress = "";`,
`        [ObservableProperty] private string civilRegistryId = "";
        [ObservableProperty] private string middleName = "";
        [ObservableProperty] private string presentAddress = "";
        [ObservableProperty] private byte[]? profileImage;
        [ObservableProperty] private BitmapImage? profileImagePreview;

        public bool HasProfileImage => ProfileImagePreview != null;`,
    'BeneficiaryRecord image properties'
  );

  text = replaceOnce(
    text,
`        partial void OnShareAmountChanged(decimal? value) => OnPropertyChanged(nameof(ShareText));
        partial void OnShareQtyChanged(int? value) => OnPropertyChanged(nameof(ShareText));
        partial void OnShareUnitChanged(string? value) => OnPropertyChanged(nameof(ShareText));`,
`        partial void OnShareAmountChanged(decimal? value) => OnPropertyChanged(nameof(ShareText));
        partial void OnShareQtyChanged(int? value) => OnPropertyChanged(nameof(ShareText));
        partial void OnShareUnitChanged(string? value) => OnPropertyChanged(nameof(ShareText));

        partial void OnProfileImageChanged(byte[]? value)
        {
            ProfileImagePreview = ToBitmap(value);
            OnPropertyChanged(nameof(HasProfileImage));
        }

        private static BitmapImage? ToBitmap(byte[]? bytes)
        {
            if (bytes is null || bytes.Length == 0)
                return null;

            try
            {
                using var stream = new MemoryStream(bytes);
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch
            {
                return null;
            }
        }`,
    'BeneficiaryRecord image conversion'
  );

  return text;
}

function patchAssignmentRepository(source) {
  let text = source.text;
  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`namespace WpfApp3.Services
{`,
`namespace WpfApp3.Services
{
    // ${MARKER}`,
    'AllotmentBeneficiariesRepository marker'
  );

  text = replaceOnce(
    text,
`    IFNULL(b.classification,'None') AS classification,
    ab.share_amount,`,
`    IFNULL(b.classification,'None') AS classification,
    b.profile_image,
    ab.share_amount,`,
    'assigned beneficiary profile image select'
  );

  text = replaceOnce(
    text,
`            var oClass = rd.GetOrdinal("classification");
            var oShareAmt = rd.GetOrdinal("share_amount");`,
`            var oClass = rd.GetOrdinal("classification");
            var oProfileImage = rd.GetOrdinal("profile_image");
            var oShareAmt = rd.GetOrdinal("share_amount");`,
    'assigned beneficiary profile image ordinal'
  );

  text = replaceOnce(
    text,
`                    Classification = rd.IsDBNull(oClass) ? "None" : rd.GetString(oClass),
                    ShareAmount = rd.IsDBNull(oShareAmt) ? (decimal?)null : rd.GetDecimal(oShareAmt),`,
`                    Classification = rd.IsDBNull(oClass) ? "None" : rd.GetString(oClass),
                    ProfileImage = rd.IsDBNull(oProfileImage) ? null : (byte[])rd.GetValue(oProfileImage),
                    ShareAmount = rd.IsDBNull(oShareAmt) ? (decimal?)null : rd.GetDecimal(oShareAmt),`,
    'assigned beneficiary profile image mapping'
  );

  text = replaceOnce(
    text,
`    b.barangay,
    IFNULL(b.classification,'None') AS classification
FROM beneficiaries b`,
`    b.barangay,
    IFNULL(b.classification,'None') AS classification,
    b.profile_image
FROM beneficiaries b`,
    'available beneficiary profile image select'
  );

  text = replaceOnce(
    text,
`                    Barangay = rd.GetString("barangay"),
                    Classification = rd.GetString("classification"),`,
`                    Barangay = rd.GetString("barangay"),
                    Classification = rd.GetString("classification"),
                    ProfileImage = rd.IsDBNull(rd.GetOrdinal("profile_image"))
                        ? null
                        : (byte[])rd["profile_image"],`,
    'available beneficiary profile image mapping'
  );

  return text;
}

function patchBeneficiariesXaml(source) {
  let text = source.text;
  if (text.includes(MARKER)) return text;

  text = replaceOnce(
    text,
`<UserControl x:Class="WpfApp3.Views.Beneficiaries.BeneficiariesView"`,
`<!-- ${MARKER} -->
<UserControl x:Class="WpfApp3.Views.Beneficiaries.BeneficiariesView"`,
    'Beneficiaries XAML marker'
  );

  text = replaceOnce(
    text,
`                                                <Border Width="32" Height="32" CornerRadius="16" Background="{DynamicResource ThemeBrush_Accent_EEF2FF}" VerticalAlignment="Center">
                                                    <TextBlock FontFamily="Segoe MDL2 Assets" Text="" Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}" FontSize="15" HorizontalAlignment="Center" VerticalAlignment="Center" />
                                                </Border>`,
beneficiaryAvatar(32, 15),
    'Beneficiaries main table avatar'
  );

  text = replaceOnce(
    text,
`                        <Border Grid.Column="1" Width="30" Height="30" CornerRadius="15" Background="{DynamicResource ThemeBrush_Accent_EEF2FF}" VerticalAlignment="Center">
                            <TextBlock FontFamily="Segoe MDL2 Assets" Text="" Foreground="{DynamicResource ThemeBrush_AccentText_1F2A44}" FontSize="14" HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>`,
beneficiaryAvatar(30, 14, 'Grid.Column="1"'),
    'Add Beneficiaries table avatar'
  );

  return text;
}

function main() {
  console.log('Applying E-Kalinga Distribution search and profile-picture patch...');
  console.log('Dirty working trees are allowed. No build, commit, or push will be performed.');

  const sources = Object.fromEntries(
    Object.entries(files).map(([key, file]) => [key, readSource(file)])
  );

  const outputs = {
    distributionVm: patchDistributionViewModel(sources.distributionVm),
    distributionXaml: patchDistributionXaml(sources.distributionXaml),
    distributionCodeBehind: patchDistributionCodeBehind(sources.distributionCodeBehind),
    beneficiaryModel: patchBeneficiaryModel(sources.beneficiaryModel),
    assignmentRepo: patchAssignmentRepository(sources.assignmentRepo),
    beneficiariesXaml: patchBeneficiariesXaml(sources.beneficiariesXaml),
  };

  const changed = Object.keys(outputs).filter(key => outputs[key] !== sources[key].text);
  if (changed.length === 0) {
    console.log('Patch is already applied. No files changed.');
    return;
  }

  // All transformations complete before any file is written.
  for (const key of changed) {
    fs.writeFileSync(
      sources[key].file,
      restoreSource(sources[key], outputs[key]),
      'utf8'
    );
    console.log(`Updated: ${path.relative(root, sources[key].file)}`);
  }

  console.log('\nPatch applied successfully.');
  console.log('Review with: git diff -- WpfApp3/Models/BeneficiaryRecord.cs WpfApp3/Services/AllotmentBeneficiariesRepository.cs WpfApp3/Views/Beneficiaries WpfApp3/Views/Distribution WpfApp3/ViewModels/Distribution');
  console.log('No build, commit, or push was run.');
}

try {
  main();
} catch (error) {
  console.error(`\nPatch failed: ${error.message}`);
  console.error('No files were written unless the failure occurred during the final write stage.');
  process.exitCode = 1;
}
}

if (process.exitCode) {
  process.exit(process.exitCode);
}

console.log('\nComplete E-Kalinga patch finished.');
console.log('No build, commit, or push was run.');
