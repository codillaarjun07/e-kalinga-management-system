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

        // keep same delegate instances for RemoveHandler
        private readonly TextCompositionEventHandler _textHandler;
        private readonly KeyEventHandler _keyHandler;

        public DistributionView()
        {
            InitializeComponent();

            _textHandler = OnPreviewTextInput;
            _keyHandler = OnPreviewKeyDown;

            Loaded += (_, __) => HookVm();
            DataContextChanged += (_, __) => HookVm();
            Unloaded += (_, __) => UnhookGlobalScan();
        }

        private void HookVm()
        {
            if (DataContext is not DistributionViewModel vm) return;

            vm.PropertyChanged -= Vm_PropertyChanged;
            vm.PropertyChanged += Vm_PropertyChanged;

            if (vm.IsReleaseSessionOpen) HookGlobalScan();
            else UnhookGlobalScan();
        }

        private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not DistributionViewModel vm) return;

            if (e.PropertyName == nameof(DistributionViewModel.IsReleaseSessionOpen))
            {
                if (vm.IsReleaseSessionOpen) HookGlobalScan();
                else UnhookGlobalScan();
            }

            // optional: if confirm modal opens/closes, reset buffer so next scan is clean
            if (e.PropertyName == nameof(DistributionViewModel.IsConfirmReleaseOpen))
            {
                _scanBuffer.Clear();
            }
        }

        private void HookGlobalScan()
        {
            if (_hooked) return;

            _hostWindow = Window.GetWindow(this);
            if (_hostWindow is null) return;

            _hooked = true;
            _scanBuffer.Clear();

            // handledEventsToo = true so we still receive input even if something else handles it
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

            // ✅ While confirm modal is open, swallow scanner characters so it won't "type" into buttons
            if (vm.IsConfirmReleaseOpen)
            {
                e.Handled = true;
                return;
            }

            _scanBuffer.Append(e.Text);

            // ✅ show live scanned characters in textbox
            vm.ScanInput = _scanBuffer.ToString();

            // prevent scan characters from going into other focused controls
            e.Handled = true;
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not DistributionViewModel vm) return;
            if (!vm.IsReleaseSessionOpen) return;

            // ✅ While confirm modal open: block scanner keys EXCEPT Enter/Esc (so user can confirm/cancel)
            if (vm.IsConfirmReleaseOpen)
            {
                if (e.Key != Key.Enter && e.Key != Key.Return && e.Key != Key.Escape)
                    e.Handled = true;

                return;
            }

            // Many scanners terminate with Enter (some with Tab). Support both safely:
            if (e.Key == Key.Enter || e.Key == Key.Return || e.Key == Key.Tab)
            {
                var raw = _scanBuffer.ToString().Trim();
                _scanBuffer.Clear();

                // show final scan value
                vm.ScanInput = raw;

                if (!string.IsNullOrWhiteSpace(raw))
                    vm.ScanCommand.Execute(raw);

                e.Handled = true;
            }
        }
    }
}