using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp3.Services;
using WpfApp3.Views.Allotment;
using WpfApp3.Views.Beneficiaries;
using WpfApp3.Views.Dashboard;
using WpfApp3.Views.Distribution;
using WpfApp3.Views.Users;
using WpfApp3.Views.Validators;

namespace WpfApp3.ViewModels;


public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private UserControl currentView = new DashboardView();
    [ObservableProperty] private string pageTitle = "Dashboard";

    public ObservableCollection<NavItem> NavItems { get; }

    [ObservableProperty]
    private NavItem? selectedNavItem;

    public event Action? LogoutRequested;
    public ICommand LogoutCommand { get; }

    public MainViewModel()
    {
        NavItems = new ObservableCollection<NavItem>
        {
            new NavItem("📊 Dashboard", NavigateDashboardCommand),
            new NavItem("🔀 Allotment", NavigateAllotmentCommand),
            new NavItem("👥 Beneficiaries", NavigateBeneficiariesCommand),
            new NavItem("📦 Distribution", NavigateDistributionCommand),
            //new NavItem("👤 Client Profile", NavigateClientProfileCommand),
            new NavItem("🔐 Validators", NavigateValidatorsCommand),
            new NavItem("🖥️ Users", NavigateUsersCommand),
        };

        // Default selection (highlights Dashboard on startup)
        SelectedNavItem = NavItems[0];
        LogoutCommand = new RelayCommand(Logout);

    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
        // When user clicks a menu item, run its command
        value?.Command.Execute(null);
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        PageTitle = "Dashboard";
        CurrentView = new DashboardView();
    }

    [RelayCommand]
    private void NavigateAllotment()
    {
        PageTitle = "Allotment";
        CurrentView = new AllotmentView();
    }

    [RelayCommand] private void NavigateBeneficiaries()
    {
        PageTitle = "Beneficiaries";
        CurrentView = new BeneficiariesView();
    }
    [RelayCommand] private void NavigateDistribution()
    {
        PageTitle = "Distribution";
        CurrentView = new DistributionView();
    }
    [RelayCommand] private void NavigateClientProfile() => NavigatePlaceholder("Client Profile");
    [RelayCommand] private void NavigateValidators()
    {
        PageTitle = "Validators";
        CurrentView = new ValidatorsView();
    }

    [RelayCommand] private void NavigateUsers()
    {
        PageTitle = "Users";
        CurrentView = new UsersView();
    }

    private void NavigatePlaceholder(string title)
    {
        PageTitle = title;
        CurrentView = new UserControl
        {
            Content = new TextBlock
            {
                Text = $"{title} page (coming soon)",
                FontSize = 22,
                FontWeight = System.Windows.FontWeights.SemiBold,
                Margin = new System.Windows.Thickness(24)
            }
        };
    }

    private void Logout()
    {
        // Clear session (POC)
        SessionService.Clear();

        // Ask the window to switch back to Login
        LogoutRequested?.Invoke();
    }
}
