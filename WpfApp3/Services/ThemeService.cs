// EKALINGA-DARK-CONTROL-FIX-V7
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows;

namespace WpfApp3.Services
{
    public partial class ThemeService : ObservableObject
    {
        private const string LightThemeSource = "Themes/LightTheme.xaml";
        private const string DarkThemeSource = "Themes/DarkTheme.xaml";

        public static ThemeService Current { get; } = new();

        [ObservableProperty]
        private bool isDarkMode;

        public string ThemeIcon => IsDarkMode ? "\uE706" : "\uE708";
        public string ThemeToggleToolTip => IsDarkMode ? "Switch to light mode" : "Switch to dark mode";

        private ThemeService()
        {
        }

        public void Initialize()
        {
            DarkModeControlService.Initialize();

            // Every new application session starts in the original light design.
            // Dark mode remains available through the top-bar toggle.
            ApplyTheme(false);
        }

        [RelayCommand]
        private void ToggleTheme()
        {
            ApplyTheme(!IsDarkMode);
        }

        private void ApplyTheme(bool useDarkTheme)
        {
            var application = Application.Current;
            if (application is null)
                return;

            var replacement = new ResourceDictionary
            {
                Source = new Uri(useDarkTheme ? DarkThemeSource : LightThemeSource, UriKind.Relative)
            };

            var dictionaries = application.Resources.MergedDictionaries;
            var themeIndex = -1;

            for (var index = 0; index < dictionaries.Count; index++)
            {
                var source = dictionaries[index].Source?.OriginalString ?? string.Empty;
                if (source.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
                    source.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
                {
                    themeIndex = index;
                    break;
                }
            }

            if (themeIndex >= 0)
                dictionaries[themeIndex] = replacement;
            else
                dictionaries.Insert(0, replacement);

            IsDarkMode = useDarkTheme;
            DarkModeControlService.ApplyToOpenWindows(useDarkTheme);
        }

        partial void OnIsDarkModeChanged(bool value)
        {
            OnPropertyChanged(nameof(ThemeIcon));
            OnPropertyChanged(nameof(ThemeToggleToolTip));
        }
    }
}
