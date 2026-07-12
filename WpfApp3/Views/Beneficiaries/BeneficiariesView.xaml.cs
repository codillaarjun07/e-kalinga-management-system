using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using WpfApp3.Models;
using WpfApp3.ViewModels.Beneficiaries;

namespace WpfApp3.Views.Beneficiaries
{
    public partial class BeneficiariesView : UserControl
    {
        private static readonly Regex DigitsOnly = new(@"^\d+$");
        private static readonly Regex MoneyChars = new(@"^[0-9.,]+$");

        private CheckBox? _addSelectAllHeaderCheckBox;
        private BeneficiariesViewModel? _addSelectAllViewModel;
        private bool _syncingAddSelectAllHeader;
        public BeneficiariesView()
        {
            InitializeComponent();
        }

        private void ProjectSearchHost_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;

            // Important: do not let the host Border handle the dropdown button click first.
            // The button has its own true toggle behavior below.
            if (IsInsideButtonBase(source))
                return;

            if (DataContext is BeneficiariesViewModel vm && vm.OpenProjectDropdownCommand.CanExecute(null))
                vm.OpenProjectDropdownCommand.Execute(null);

            // Let the TextBox handle its own mouse click, otherwise make the whole field focus the TextBox.
            if (IsInsideTextBox(source))
                return;

            e.Handled = true;
            FocusProjectSearchBox(selectAll: true);
        }

        private void ProjectSearchTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (DataContext is BeneficiariesViewModel vm && vm.OpenProjectDropdownCommand.CanExecute(null))
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
            if (DataContext is not BeneficiariesViewModel vm)
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
            if (DataContext is not BeneficiariesViewModel vm)
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

            if (DataContext is BeneficiariesViewModel vm && vm.SelectProjectCommand.CanExecute(project))
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

                if (DataContext is BeneficiariesViewModel vm && vm.CloseProjectDropdownCommand.CanExecute(null))
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

        private void BlockSpace_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space) e.Handled = true;
        }

        private void DigitsOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !DigitsOnly.IsMatch(e.Text);
        }

        private void Money_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !MoneyChars.IsMatch(e.Text);
        }


        private void AttachAddSelectAllViewModel(
            BeneficiariesViewModel? viewModel)
        {
            if (ReferenceEquals(_addSelectAllViewModel, viewModel))
            {
                SyncAddSelectAllHeader();
                return;
            }

            if (_addSelectAllViewModel is not null)
            {
                _addSelectAllViewModel.PropertyChanged -=
                    AddSelectAllViewModel_PropertyChanged;
            }

            _addSelectAllViewModel = viewModel;

            if (_addSelectAllViewModel is not null)
            {
                _addSelectAllViewModel.PropertyChanged +=
                    AddSelectAllViewModel_PropertyChanged;
            }

            SyncAddSelectAllHeader();
        }

        private void AddSelectAllViewModel_PropertyChanged(
            object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName !=
                nameof(BeneficiariesViewModel.IsAddAllSelected))
            {
                return;
            }

            Dispatcher.BeginInvoke(
                new System.Action(SyncAddSelectAllHeader));
        }

        private void AddSelectAllCheckBox_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            _addSelectAllHeaderCheckBox = checkBox;

            AttachAddSelectAllViewModel(
                DataContext as BeneficiariesViewModel);

            SyncAddSelectAllHeader();
        }

        private void AddSelectAllCheckBox_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            if (!ReferenceEquals(
                _addSelectAllHeaderCheckBox,
                sender))
            {
                return;
            }

            _addSelectAllHeaderCheckBox = null;

            if (_addSelectAllViewModel is not null)
            {
                _addSelectAllViewModel.PropertyChanged -=
                    AddSelectAllViewModel_PropertyChanged;
            }

            _addSelectAllViewModel = null;
        }

        private void AddSelectAllCheckBox_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_syncingAddSelectAllHeader)
                return;

            if (sender is not CheckBox checkBox)
                return;

            if (DataContext is not BeneficiariesViewModel viewModel)
                return;

            // Capture the state selected by the user before attaching or
            // synchronizing. The old handler synchronized first, which reset
            // the checkbox to the previous ViewModel value and made the click
            // appear to do nothing.
            var shouldSelectAll =
                checkBox.IsChecked == true;

            if (!ReferenceEquals(
                _addSelectAllViewModel,
                viewModel))
            {
                AttachAddSelectAllViewModel(viewModel);
            }

            viewModel.IsAddAllSelected =
                shouldSelectAll;

            SyncAddSelectAllHeader();
            e.Handled = true;
        }

        private void SyncAddSelectAllHeader()
        {
            if (_addSelectAllHeaderCheckBox is null ||
                _addSelectAllViewModel is null)
            {
                return;
            }

            _syncingAddSelectAllHeader = true;

            try
            {
                _addSelectAllHeaderCheckBox.IsChecked =
                    _addSelectAllViewModel.IsAddAllSelected;
            }
            finally
            {
                _syncingAddSelectAllHeader = false;
            }
        }
        private void BeneficiaryRow_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source) return;

            // ignore clicks on buttons / checkboxes inside the row
            var current = source;
            while (current != null)
            {
                if (current is ButtonBase || current is CheckBox)
                    return;

                current = VisualTreeHelper.GetParent(current);
            }

            if (sender is not DataGridRow row) return;
            if (row.Item is not BeneficiaryRecord record) return;
            if (DataContext is not BeneficiariesViewModel vm) return;

            if (vm.OpenProfileCommand.CanExecute(record))
                vm.OpenProfileCommand.Execute(record);
        }
    }
}
