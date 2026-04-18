using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.ViewModels.Distribution;

namespace WpfApp3.Views.Distribution
{
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