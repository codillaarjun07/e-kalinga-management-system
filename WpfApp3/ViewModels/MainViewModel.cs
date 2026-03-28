using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp3.Services;
using WpfApp3.Views.Allotment;
using WpfApp3.Views.Backup;
using WpfApp3.Views.Beneficiaries;
using WpfApp3.Views.Dashboard;
using WpfApp3.Views.Distribution;
using WpfApp3.Views.Users;
using WpfApp3.Views.Settings;
using WpfApp3.Views.Validators;
using WpfApp3.Views.AuditLogs;

namespace WpfApp3.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private UserControl currentView = new DashboardView();
    [ObservableProperty] private string pageTitle = "Dashboard";
    [ObservableProperty] private string currentUserLabel = "User";
    [ObservableProperty] private ImageSource? currentUserProfileImage;
    [ObservableProperty] private bool isCurrentUserProfileImageEmpty = true;

    [ObservableProperty] private ImageSource? appLogo;

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
            new NavItem("🔐 Validators", NavigateValidatorsCommand),
            new NavItem("🖥️ Users", NavigateUsersCommand),
            new NavItem("⚙️ Settings", NavigateSettingsCommand),
        };

        if (IsSuperadmin)
        {
            NavItems.Add(new NavItem("🕵 Audit Logs", NavigateAuditLogsCommand));
            NavItems.Add(new NavItem("🗄️ Backup", NavigateBackupCommand));
        }

        SelectedNavItem = NavItems[0];
        LogoutCommand = new RelayCommand(Logout);

        LoadCurrentUser();
        LoadAppLogo();
    }

    public bool IsSuperadmin =>
        string.Equals(SessionService.Role, "Superadmin", StringComparison.OrdinalIgnoreCase);

    private void LoadCurrentUser()
    {
        try
        {
            var repo = new UsersRepository();
            var username = SessionService.Username;

            var user = repo.GetAll()
                           .FirstOrDefault(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                CurrentUserLabel = "User";
                CurrentUserProfileImage = null;
                IsCurrentUserProfileImageEmpty = true;
                return;
            }

            CurrentUserLabel = $"{user.FirstName} {user.LastName}".Trim();

            if (string.IsNullOrWhiteSpace(CurrentUserLabel))
                CurrentUserLabel = user.Username ?? "User";

            if (user.ProfilePicture != null && user.ProfilePicture.Length > 0)
            {
                CurrentUserProfileImage = ToImage(user.ProfilePicture);
                IsCurrentUserProfileImageEmpty = CurrentUserProfileImage == null;
            }
            else
            {
                CurrentUserProfileImage = null;
                IsCurrentUserProfileImageEmpty = true;
            }
        }
        catch
        {
            CurrentUserLabel = "User";
            CurrentUserProfileImage = null;
            IsCurrentUserProfileImageEmpty = true;
        }
    }

    private void LoadAppLogo()
    {
        try
        {
            var logoRepo = new LogosRepository();
            logoRepo.EnsureTable();

            var activeLogo = logoRepo.GetActive();

            if (activeLogo?.ImageData != null && activeLogo.ImageData.Length > 0)
            {
                var dbLogo = ToImage(activeLogo.ImageData);
                if (dbLogo != null)
                {
                    AppLogo = dbLogo;
                    return;
                }
            }
        }
        catch
        {
            // fall back to filesystem logo
        }

        AppLogo = LoadDefaultLogoFromFileSystem();
    }

    private ImageSource? LoadDefaultLogoFromFileSystem()
    {
        try
        {
            // Replace this with the exact same logo path currently used in your MainWindow.xaml
            var uri = new Uri("pack://application:,,,/ekaling.png", UriKind.Absolute);

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = uri;
            image.EndInit();
            image.Freeze();

            return image;
        }
        catch
        {
            return null;
        }
    }

    private ImageSource? ToImage(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    partial void OnSelectedNavItemChanged(NavItem? value)
    {
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

    [RelayCommand]
    private void NavigateBeneficiaries()
    {
        PageTitle = "Beneficiaries";
        CurrentView = new BeneficiariesView();
    }

    [RelayCommand]
    private void NavigateDistribution()
    {
        PageTitle = "Distribution";
        CurrentView = new DistributionView();
    }

    [RelayCommand]
    private void NavigateClientProfile() => NavigatePlaceholder("Client Profile");

    [RelayCommand]
    private void NavigateValidators()
    {
        PageTitle = "Validators";
        CurrentView = new ValidatorsView();
    }

    [RelayCommand]
    private void NavigateUsers()
    {
        PageTitle = "Users";
        CurrentView = new UsersView();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        PageTitle = "Settings";
        CurrentView = new SettingsView();
    }

    [RelayCommand]
    private void NavigateBackup()
    {
        if (!IsSuperadmin)
            return;

        PageTitle = "Backup";
        CurrentView = new BackupView();
    }

    [RelayCommand]
    private void NavigateAuditLogs()
    {
        if (!IsSuperadmin)
            return;

        PageTitle = "Audit Logs";
        CurrentView = new AuditLogsView();
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
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(24)
            }
        };
    }

    private void Logout()
    {
        SessionService.Clear();
        LogoutRequested?.Invoke();
    }
}