// E-KALINGA DARK NAVIGATION CONTROLS V8
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace WpfApp3.Services
{
    internal static class DarkModeControlService
    {
        private sealed class OriginalValues
        {
            public Dictionary<DependencyProperty, object> Values { get; } = new();
        }

        private sealed class ContentWatchState
        {
        }

        private static readonly ConditionalWeakTable<DependencyObject, OriginalValues> Originals = new();
        private static readonly ConditionalWeakTable<ContentControl, ContentWatchState> WatchedContentControls = new();
        private static readonly DependencyPropertyDescriptor? ContentDescriptor =
            DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ContentControl));

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            // handledEventsToo=true ensures dynamically inserted controls are observed
            // even when a local control marks Loaded as handled.
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnElementLoaded),
                true);
        }

        public static void ApplyToOpenWindows(bool darkMode)
        {
            var application = Application.Current;
            if (application is null)
                return;

            foreach (Window window in application.Windows)
            {
                if (IsLoginWindow(window))
                    continue;

                ApplyTree(window, darkMode);
                QueueApplyTree(window);
            }
        }

        private static void OnElementLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || IsInsideLoginWindow(element))
                return;

            if (element is ContentControl contentControl)
                EnsureContentWatcher(contentControl);

            ApplyElement(element, ThemeService.Current.IsDarkMode);

            // Page navigation inserts a new UserControl under the CurrentView
            // ContentControl. Rewalk the completed visual tree after templates,
            // local styles, and generated controls have finished loading.
            if (element is Window ||
                element is UserControl ||
                element is ContentControl ||
                element is ContentPresenter)
            {
                QueueApplyTree(element);
            }
        }

        private static void EnsureContentWatcher(ContentControl control)
        {
            if (WatchedContentControls.TryGetValue(control, out _))
                return;

            WatchedContentControls.Add(control, new ContentWatchState());
            ContentDescriptor?.AddValueChanged(control, OnContentChanged);
        }

        private static void OnContentChanged(object? sender, EventArgs e)
        {
            if (sender is not ContentControl control || IsInsideLoginWindow(control))
                return;

            // CurrentView is replaced during sidebar navigation. Apply after the new
            // page has built its visual tree and again at ContextIdle for templates.
            QueueApplyTree(control);

            if (control.Content is FrameworkElement content)
                QueueApplyTree(content);
        }

        private static void QueueApplyTree(FrameworkElement root)
        {
            void ApplyCurrentTheme()
            {
                if (IsInsideLoginWindow(root))
                    return;

                if (root is not Window && !root.IsLoaded)
                    return;

                ApplyTree(root, ThemeService.Current.IsDarkMode);
            }

            root.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(ApplyCurrentTheme));

            root.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(ApplyCurrentTheme));
        }

        private static void ApplyTree(DependencyObject root, bool darkMode)
        {
            if (IsInsideLoginWindow(root))
                return;

            ApplyElement(root, darkMode);

            var childCount = VisualTreeHelper.GetChildrenCount(root);
            for (var index = 0; index < childCount; index++)
                ApplyTree(VisualTreeHelper.GetChild(root, index), darkMode);
        }

        private static void ApplyElement(DependencyObject element, bool darkMode)
        {
            if (element is ContentControl contentControl)
                EnsureContentWatcher(contentControl);

            if (!darkMode)
            {
                Restore(element);
                return;
            }

            switch (element)
            {
                case ComboBox comboBox:
                    ApplyComboBox(comboBox);
                    break;

                case DatePicker datePicker:
                    ApplyDatePicker(datePicker);
                    break;

                case PasswordBox passwordBox:
                    ApplyPasswordBox(passwordBox);
                    break;

                case TextBox textBox:
                    ApplyTextBox(textBox);
                    break;
            }
        }

        private static void ApplyComboBox(ComboBox comboBox)
        {
            SetResource(comboBox, Control.TemplateProperty, "DarkComboBoxTemplate");
            SetResource(comboBox, ItemsControl.ItemContainerStyleProperty, "DarkComboBoxItemStyle");
            SetResource(comboBox, Control.ForegroundProperty, "ThemeTextPrimaryBrush");
            SetResource(comboBox, Control.BackgroundProperty, "ThemeInputBrush");
            SetResource(comboBox, Control.BorderBrushProperty, "ThemeBorderBrush");
        }

        private static void ApplyDatePicker(DatePicker datePicker)
        {
            SetResource(datePicker, Control.ForegroundProperty, "ThemeTextPrimaryBrush");
            SetResource(datePicker, Control.BackgroundProperty, "ThemeInputBrush");
            SetResource(datePicker, Control.BorderBrushProperty, "ThemeBorderBrush");
            SetResource(datePicker, DatePicker.CalendarStyleProperty, "DarkCalendarStyle");
        }

        private static void ApplyTextBox(TextBox textBox)
        {
            if (NeedsLightForeground(textBox.Foreground))
                SetResource(textBox, Control.ForegroundProperty, "ThemeTextPrimaryBrush");

            if (NeedsDarkBackground(textBox.Background))
                SetResource(textBox, Control.BackgroundProperty, "ThemeInputBrush");

            if (NeedsDarkBorder(textBox.BorderBrush))
                SetResource(textBox, Control.BorderBrushProperty, "ThemeBorderBrush");

            if (NeedsLightForeground(textBox.CaretBrush))
                SetResource(textBox, TextBoxBase.CaretBrushProperty, "ThemePrimaryTextBrush");
        }

        private static void ApplyPasswordBox(PasswordBox passwordBox)
        {
            if (NeedsLightForeground(passwordBox.Foreground))
                SetResource(passwordBox, Control.ForegroundProperty, "ThemeTextPrimaryBrush");

            if (NeedsDarkBackground(passwordBox.Background))
                SetResource(passwordBox, Control.BackgroundProperty, "ThemeInputBrush");

            if (NeedsDarkBorder(passwordBox.BorderBrush))
                SetResource(passwordBox, Control.BorderBrushProperty, "ThemeBorderBrush");
        }

        private static bool NeedsLightForeground(Brush brush)
        {
            return brush is SolidColorBrush solid &&
                   solid.Color.A > 0 &&
                   GetLuminance(solid.Color) < 0.58;
        }

        private static bool NeedsDarkBackground(Brush brush)
        {
            return brush is SolidColorBrush solid &&
                   solid.Color.A > 24 &&
                   GetLuminance(solid.Color) > 0.62;
        }

        private static bool NeedsDarkBorder(Brush brush)
        {
            return brush is SolidColorBrush solid &&
                   solid.Color.A > 24 &&
                   GetLuminance(solid.Color) > 0.56;
        }

        private static double GetLuminance(Color color)
        {
            return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255d;
        }

        private static void SetResource(FrameworkElement element, DependencyProperty property, object resourceKey)
        {
            SaveOriginalValue(element, property);
            element.SetResourceReference(property, resourceKey);
        }

        private static void SaveOriginalValue(DependencyObject element, DependencyProperty property)
        {
            var state = Originals.GetValue(element, _ => new OriginalValues());
            if (!state.Values.ContainsKey(property))
                state.Values[property] = element.ReadLocalValue(property);
        }

        private static void Restore(DependencyObject element)
        {
            if (!Originals.TryGetValue(element, out var state))
                return;

            foreach (var pair in state.Values)
            {
                if (pair.Value == DependencyProperty.UnsetValue)
                    element.ClearValue(pair.Key);
                else
                    element.SetValue(pair.Key, pair.Value);
            }

            Originals.Remove(element);
        }

        private static bool IsLoginWindow(Window window)
        {
            return string.Equals(
                window.GetType().FullName,
                "WpfApp3.Views.Login.LoginWindow",
                StringComparison.Ordinal);
        }

        private static bool IsInsideLoginWindow(DependencyObject element)
        {
            if (element is Window window)
                return IsLoginWindow(window);

            var owner = Window.GetWindow(element);
            return owner is not null && IsLoginWindow(owner);
        }
    }
}
