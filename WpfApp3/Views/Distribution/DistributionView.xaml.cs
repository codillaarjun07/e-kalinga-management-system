using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using WpfApp3.Models;
using WpfApp3.ViewModels.Distribution;

namespace WpfApp3.Views.Distribution
{
    // EKALINGA_DISTRIBUTION_RELEASE_SPLIT_V1
    // EKALINGA_DISTRIBUTION_SEARCH_PROFILE_V2
    public partial class DistributionView : UserControl
    {
        private readonly StringBuilder _scanBuffer = new();

        private Window? _hostWindow;
        private bool _hooked;
        private DistributionViewModel? _observedVm;

        private readonly TextCompositionEventHandler _textHandler;
        private readonly KeyEventHandler _keyHandler;

        public DistributionView()
        {
            InitializeComponent();

            _textHandler = OnPreviewTextInput;
            _keyHandler = OnPreviewKeyDown;

            Loaded += (_, __) => HookVm();
            DataContextChanged += (_, __) => HookVm();
            Unloaded += (_, __) =>
            {
                UnhookGlobalScan();

                if (_observedVm is not null)
                    _observedVm.PropertyChanged -= Vm_PropertyChanged;
            };
        }

        private void ProjectSearchHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            // The dropdown button has its own toggle behavior.
            if (IsInsideButtonBase(source))
                return;

            if (DataContext is DistributionViewModel vm && vm.OpenProjectDropdownCommand.CanExecute(null))
                vm.OpenProjectDropdownCommand.Execute(null);

            // Let the TextBox receive normal caret/selection clicks.
            if (IsInsideTextBox(source))
                return;

            e.Handled = true;
            FocusProjectSearchBox(selectAll: true);
        }

        private void ProjectSearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is DistributionViewModel vm && vm.OpenProjectDropdownCommand.CanExecute(null))
                vm.OpenProjectDropdownCommand.Execute(null);
        }

        private void ProjectSearchTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CloseProjectDropdownIfFocusMovedAway();
        }

        private void ProjectOptionsList_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CloseProjectDropdownIfFocusMovedAway();
        }

        private void ProjectSearchTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not DistributionViewModel vm)
                return;

            if (e.Key == Key.Escape)
            {
                if (vm.CloseProjectDropdownCommand.CanExecute(null))
                    vm.CloseProjectDropdownCommand.Execute(null);

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                var first = vm.FilteredProjects.FirstOrDefault();
                if (first is not null && vm.SelectProjectCommand.CanExecute(first))
                    vm.SelectProjectCommand.Execute(first);

                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                if (!vm.IsProjectDropdownOpen && vm.OpenProjectDropdownCommand.CanExecute(null))
                    vm.OpenProjectDropdownCommand.Execute(null);

                Dispatcher.BeginInvoke(new System.Action(() =>
                {
                    if (ProjectOptionsList.Items.Count > 0)
                    {
                        ProjectOptionsList.Focus();
                        ProjectOptionsList.SelectedIndex = 0;
                    }
                }), DispatcherPriority.Input);

                e.Handled = true;
            }
        }

        private void ProjectDropButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not DistributionViewModel vm)
                return;

            if (vm.IsProjectDropdownOpen)
            {
                if (vm.CloseProjectDropdownCommand.CanExecute(null))
                    vm.CloseProjectDropdownCommand.Execute(null);
            }
            else
            {
                // Dropdown button always opens the complete allotment/project list first.
                if (vm.ShowAllProjectDropdownCommand.CanExecute(null))
                    vm.ShowAllProjectDropdownCommand.Execute(null);

                FocusProjectSearchBox(selectAll: false);
            }

            e.Handled = true;
        }

        private void ProjectOptionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProjectOptionsList.SelectedItem is not AllotmentProjectOption project)
                return;

            if (DataContext is DistributionViewModel vm && vm.SelectProjectCommand.CanExecute(project))
                vm.SelectProjectCommand.Execute(project);

            ProjectOptionsList.SelectedItem = null;
        }

        private void FocusProjectSearchBox(bool selectAll)
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                ProjectSearchTextBox.Focus();

                if (selectAll)
                    ProjectSearchTextBox.SelectAll();
                else
                    ProjectSearchTextBox.CaretIndex = ProjectSearchTextBox.Text.Length;
            }), DispatcherPriority.Input);
        }

        private void CloseProjectDropdownIfFocusMovedAway()
        {
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                if (ProjectSearchTextBox.IsKeyboardFocusWithin || ProjectOptionsList.IsKeyboardFocusWithin)
                    return;

                if (DataContext is DistributionViewModel vm && vm.CloseProjectDropdownCommand.CanExecute(null))
                    vm.CloseProjectDropdownCommand.Execute(null);
            }), DispatcherPriority.Background);
        }

        private static bool IsInsideTextBox(DependencyObject? source)
        {
            var current = source;
            while (current is not null)
            {
                if (current is TextBox)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private static bool IsInsideButtonBase(DependencyObject? source)
        {
            var current = source;
            while (current is not null)
            {
                if (current is ButtonBase)
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void ManualReleaseIdTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
        {
            if (_observedVm is not null)
                _observedVm.PropertyChanged -= Vm_PropertyChanged;

            _observedVm = DataContext as DistributionViewModel;

            if (_observedVm is null)
            {
                UnhookGlobalScan();
                return;
            }

            _observedVm.PropertyChanged += Vm_PropertyChanged;

            if (_observedVm.IsReleaseSessionOpen) HookGlobalScan();
            else UnhookGlobalScan();

            RefreshPdfPreview(_observedVm);
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DistributionViewModel vm) return;

            if (e.PropertyName == nameof(DistributionViewModel.IsReleaseSessionOpen))
            {
                if (vm.IsReleaseSessionOpen) HookGlobalScan();
                else UnhookGlobalScan();
            }

            if (e.PropertyName == nameof(DistributionViewModel.IsConfirmReleaseOpen))
            {
                _scanBuffer.Clear();
            }

            if (e.PropertyName == nameof(DistributionViewModel.IsReportPreviewOpen) ||
                e.PropertyName == nameof(DistributionViewModel.ReportPreviewPath))
            {
                Dispatcher.Invoke(() => RefreshPdfPreview(vm));
            }
        }

        private void RefreshPdfPreview(DistributionViewModel vm)
        {
            if (PdfPreviewBrowser == null)
                return;

            try
            {
                if (vm.IsReportPreviewOpen &&
                    !string.IsNullOrWhiteSpace(vm.ReportPreviewPath) &&
                    System.IO.File.Exists(vm.ReportPreviewPath))
                {
                    PdfPreviewBrowser.Navigate(new System.Uri(vm.ReportPreviewPath));
                }
                else
                {
                    PdfPreviewBrowser.Navigate("about:blank");
                }
            }
            catch
            {
                try
                {
                    PdfPreviewBrowser.Navigate("about:blank");
                }
                catch
                {
                    // ignore browser reset failures
                }
            }
        }

        private void HookGlobalScan()
        {
            if (_hooked) return;

            _hostWindow = Window.GetWindow(this);
            if (_hostWindow is null) return;

            _hooked = true;
            _scanBuffer.Clear();

            _hostWindow.AddHandler(UIElement.PreviewTextInputEvent, _textHandler, true);
            _hostWindow.AddHandler(UIElement.PreviewKeyDownEvent, _keyHandler, true);
        }

        private void UnhookGlobalScan()
        {
            if (!_hooked) return;
            _hooked = false;

            if (_hostWindow is not null)
            {
                _hostWindow.RemoveHandler(UIElement.PreviewTextInputEvent, _textHandler);
                _hostWindow.RemoveHandler(UIElement.PreviewKeyDownEvent, _keyHandler);
            }

            _hostWindow = null;
            _scanBuffer.Clear();
        }

        private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (DataContext is not DistributionViewModel vm) return;
            if (!vm.IsReleaseSessionOpen) return;

            if (vm.IsConfirmReleaseOpen)
            {
                e.Handled = true;
                return;
            }

            // Allow normal keyboard typing while the manual ID field is focused.
            if (ManualReleaseIdTextBox?.IsKeyboardFocusWithin == true ||
                ReleaseQueueSearchTextBox?.IsKeyboardFocusWithin == true)
                return;

            _scanBuffer.Append(e.Text);
            vm.ScanInput = _scanBuffer.ToString();
            e.Handled = true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not DistributionViewModel vm) return;
            if (!vm.IsReleaseSessionOpen) return;

            if (vm.IsConfirmReleaseOpen)
            {
                if (e.Key != Key.Enter && e.Key != Key.Return && e.Key != Key.Escape)
                    e.Handled = true;

                return;
            }

            // The TextBox handles manual entry and Enter itself.
            if (ManualReleaseIdTextBox?.IsKeyboardFocusWithin == true ||
                ReleaseQueueSearchTextBox?.IsKeyboardFocusWithin == true)
                return;

            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)
            {
                var raw = _scanBuffer.ToString().Trim();
                _scanBuffer.Clear();

                vm.ScanInput = raw;

                if (!string.IsNullOrWhiteSpace(raw))
                    vm.ScanCommand.Execute(raw);

                e.Handled = true;
            }
        }
    }
}