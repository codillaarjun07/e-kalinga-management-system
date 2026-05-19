using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfApp3.Models;
using WpfApp3.Services;
using WpfApp3.Views.Allotment;
using WpfApp3.Views.Analytics;
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
    private readonly AuditLogsService _auditLogsService = new();
    private readonly DispatcherTimer _notificationTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private bool _isRefreshingNotifications;
    private bool _isSyncingSelectedNavItem;

    [ObservableProperty] private UserControl currentView = new DashboardView();
    [ObservableProperty] private string pageTitle = "Dashboard";
    [ObservableProperty] private string currentUserLabel = "User";
    [ObservableProperty] private ImageSource? currentUserProfileImage;
    [ObservableProperty] private bool isCurrentUserProfileImageEmpty = true;

    [ObservableProperty] private ImageSource? appLogo;

    [ObservableProperty] private bool isNotificationsOpen;
    [ObservableProperty] private int unreadNotificationCount;
    [ObservableProperty] private bool isLogoutConfirmOpen;
    [ObservableProperty] private bool isNotificationCenterOpen;
    [ObservableProperty] private bool isSystemInfoOpen;
    [ObservableProperty] private bool isWelcomeOpen;

    public ObservableCollection<NavItem> NavItems { get; }
    public ObservableCollection<AuditLogRecord> Notifications { get; } = new();

    [ObservableProperty] private string notificationEmptyText = "No notifications yet.";

    public bool HasNotifications => Notifications.Count > 0;

    public string NotificationBadgeText => UnreadNotificationCount > 99
        ? "99+"
        : UnreadNotificationCount.ToString();

    public string SystemName => "E-Kalinga Management System";
    public string SystemCreatorText => "Created by ArjunCode Technologies";
    public string SystemDescription =>
        "A desktop management system for organizing social assistance projects, validating beneficiaries, tracking allotments, managing releases, reviewing audit logs, and keeping administrative records in one place.";

    public string WelcomeTitle => $"Welcome, {CurrentUserLabel}!";

    public string WelcomeNotificationText => UnreadNotificationCount > 0
        ? $"You have {NotificationBadgeText} unread notification{(UnreadNotificationCount == 1 ? "" : "s")} from other users. Review them before you continue your work."
        : "You have no unread notifications right now. New alerts from other users will appear in the bell icon and notifications tile.";

    [ObservableProperty]
    private NavItem? selectedNavItem;

    public event Action? LogoutRequested;
    public ICommand LogoutCommand { get; }

    public MainViewModel()
    {
        NavItems = new ObservableCollection<NavItem>
        {
            new NavItem("📊 Dashboard", NavigateDashboardCommand),
            new NavItem("📈 Analytics", NavigateAnalyticsCommand),
            new NavItem("🔀 Allotment", NavigateAllotmentCommand),
            new NavItem("👥 Beneficiaries", NavigateBeneficiariesCommand),
            new NavItem("📦 Distribution", NavigateDistributionCommand),
            new NavItem("🔐 Master List", NavigateValidatorsCommand),
            new NavItem("🖥️ Users", NavigateUsersCommand),
            new NavItem("⚙️ Settings", NavigateSettingsCommand),
        };

        if (IsSuperadmin)
        {
            NavItems.Add(new NavItem("🕵 Audit Logs", NavigateAuditLogsCommand));
            NavItems.Add(new NavItem("🗄️ Backup", NavigateBackupCommand));
        }

        SelectedNavItem = NavItems[0];
        LogoutCommand = new RelayCommand(OpenLogoutConfirm);

        LoadCurrentUser();
        LoadAppLogo();
        IsWelcomeOpen = true;
        InitializeNotifications();
    }

    public bool IsSuperadmin =>
        string.Equals(SessionService.Role, "Superadmin", StringComparison.OrdinalIgnoreCase);

    private string CurrentAuditActorName =>
        string.IsNullOrWhiteSpace(SessionService.Username)
            ? CurrentUserLabel
            : SessionService.Username.Trim();

    partial void OnCurrentUserLabelChanged(string value)
    {
        OnPropertyChanged(nameof(WelcomeTitle));
    }

    partial void OnUnreadNotificationCountChanged(int value)
    {
        OnPropertyChanged(nameof(NotificationBadgeText));
        OnPropertyChanged(nameof(WelcomeNotificationText));
    }

    private void InitializeNotifications()
    {
        _notificationTimer.Tick += async (_, __) => await RefreshNotificationsAsync();
        _notificationTimer.Start();

        _ = RefreshNotificationsAsync();
    }

    private async Task RefreshNotificationsAsync()
    {
        if (_isRefreshingNotifications)
            return;

        _isRefreshingNotifications = true;

        try
        {
            var actorName = CurrentAuditActorName;

            var data = await Task.Run(() => new
            {
                Items = _auditLogsService.GetRecentNotificationsForUser(actorName, 15),
                UnreadCount = _auditLogsService.GetUnreadNotificationCountForUser(actorName)
            });

            Notifications.Clear();
            foreach (var item in data.Items)
                Notifications.Add(item);

            UnreadNotificationCount = data.UnreadCount;
            NotificationEmptyText = Notifications.Count == 0
                ? "No notifications from other users yet."
                : "";
            OnPropertyChanged(nameof(HasNotifications));
        }
        catch (Exception ex)
        {
            Notifications.Clear();
            UnreadNotificationCount = 0;
            NotificationEmptyText = $"Could not load notifications: {ex.Message}";
            OnPropertyChanged(nameof(HasNotifications));
        }
        finally
        {
            _isRefreshingNotifications = false;
        }
    }

    [RelayCommand]
    private async Task ToggleNotifications()
    {
        if (IsNotificationsOpen)
        {
            IsNotificationsOpen = false;
            return;
        }

        IsWelcomeOpen = false;
        IsNotificationCenterOpen = false;
        IsSystemInfoOpen = false;
        IsLogoutConfirmOpen = false;
        IsNotificationsOpen = true;
        await RefreshNotificationsAsync();
    }

    [RelayCommand]
    private async Task OpenNotificationCenter()
    {
        IsWelcomeOpen = false;
        IsNotificationsOpen = false;
        IsSystemInfoOpen = false;
        IsLogoutConfirmOpen = false;
        IsNotificationCenterOpen = true;
        await RefreshNotificationsAsync();
    }

    [RelayCommand]
    private void CloseNotificationCenter()
    {
        IsNotificationCenterOpen = false;
    }

    [RelayCommand]
    private async Task MarkNotificationsRead()
    {
        try
        {
            await Task.Run(() => _auditLogsService.MarkNotificationsReadForUser(CurrentAuditActorName));

            foreach (var item in Notifications)
                item.IsUnread = false;

            UnreadNotificationCount = 0;
        }
        catch (Exception ex)
        {
            NotificationEmptyText = $"Could not mark notifications as read: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task OpenNotification(AuditLogRecord? notification)
    {
        if (notification is null)
            return;

        IsNotificationsOpen = false;
        IsNotificationCenterOpen = false;
        IsSystemInfoOpen = false;

        try
        {
            await Task.Run(() => _auditLogsService.MarkNotificationReadUpToForUser(CurrentAuditActorName, notification.Id));
            await RefreshNotificationsAsync();
        }
        catch (Exception ex)
        {
            NotificationEmptyText = $"Could not update notification read state: {ex.Message}";
        }

        ShowAuditLogs(notification.Id);
    }

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
        if (_isSyncingSelectedNavItem)
            return;

        value?.Command.Execute(null);
    }

    private void SyncSelectedNavItem(string titleKeyword)
    {
        if (string.IsNullOrWhiteSpace(titleKeyword))
            return;

        var nav = NavItems.FirstOrDefault(x =>
            (x.Title ?? "").IndexOf(titleKeyword, StringComparison.OrdinalIgnoreCase) >= 0);

        if (nav is null || ReferenceEquals(SelectedNavItem, nav))
            return;

        try
        {
            _isSyncingSelectedNavItem = true;
            SelectedNavItem = nav;
        }
        finally
        {
            _isSyncingSelectedNavItem = false;
        }
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        SyncSelectedNavItem("Dashboard");
        PageTitle = "Dashboard";
        CurrentView = new DashboardView();
    }

    [RelayCommand]
    private void NavigateAnalytics()
    {
        SyncSelectedNavItem("Analytics");
        PageTitle = "Analytics";
        CurrentView = new AnalyticsView();
    }

    [RelayCommand]
    private void NavigateAllotment()
    {
        SyncSelectedNavItem("Allotment");
        PageTitle = "Allotment";
        CurrentView = new AllotmentView();
    }

    [RelayCommand]
    private void NavigateBeneficiaries()
    {
        SyncSelectedNavItem("Beneficiaries");
        PageTitle = "Beneficiaries";
        CurrentView = new BeneficiariesView();
    }

    [RelayCommand]
    private void NavigateDistribution()
    {
        SyncSelectedNavItem("Distribution");
        PageTitle = "Distribution";
        CurrentView = new DistributionView();
    }

    [RelayCommand]
    private void NavigateClientProfile()
    {
        SyncSelectedNavItem("Client Profile");
        NavigatePlaceholder("Client Profile");
    }

    [RelayCommand]
    private void NavigateValidators()
    {
        SyncSelectedNavItem("Master List");
        PageTitle = "Master List";
        CurrentView = new ValidatorsView();
    }

    [RelayCommand]
    private void NavigateUsers()
    {
        SyncSelectedNavItem("Users");
        PageTitle = "Users";
        CurrentView = new UsersView();
    }

    [RelayCommand]
    private void NavigateSettings()
    {
        SyncSelectedNavItem("Settings");
        PageTitle = "Settings";
        CurrentView = new SettingsView();
    }

    [RelayCommand]
    private void NavigateBackup()
    {
        if (!IsSuperadmin)
            return;

        SyncSelectedNavItem("Backup");
        PageTitle = "Backup";
        CurrentView = new BackupView();
    }

    [RelayCommand]
    private void NavigateAuditLogs()
    {
        ShowAuditLogs();
    }

    private void ShowAuditLogs(int focusedAuditLogId = 0)
    {
        if (!IsSuperadmin)
            return;

        SyncSelectedNavItem("Audit Logs");

        PageTitle = "Audit Logs";
        CurrentView = focusedAuditLogId > 0
            ? new AuditLogsView(focusedAuditLogId)
            : new AuditLogsView();
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

    [RelayCommand]
    private void CloseWelcome()
    {
        IsWelcomeOpen = false;
    }

    [RelayCommand]
    private async Task ViewWelcomeNotifications()
    {
        IsWelcomeOpen = false;
        await OpenNotificationCenter();
    }

    [RelayCommand]
    private void OpenSystemInfo()
    {
        IsWelcomeOpen = false;
        IsNotificationsOpen = false;
        IsNotificationCenterOpen = false;
        IsLogoutConfirmOpen = false;
        IsSystemInfoOpen = true;
    }

    [RelayCommand]
    private void CloseSystemInfo()
    {
        IsSystemInfoOpen = false;
    }

    private void OpenLogoutConfirm()
    {
        IsWelcomeOpen = false;
        IsNotificationsOpen = false;
        IsNotificationCenterOpen = false;
        IsSystemInfoOpen = false;
        IsLogoutConfirmOpen = true;
    }

    [RelayCommand]
    private void CancelLogout()
    {
        IsLogoutConfirmOpen = false;
    }

    [RelayCommand]
    private void ConfirmLogout()
    {
        IsWelcomeOpen = false;
        IsLogoutConfirmOpen = false;
        _notificationTimer.Stop();
        SessionService.Clear();
        LogoutRequested?.Invoke();
    }
}
