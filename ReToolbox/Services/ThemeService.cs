using System;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using ReToolbox.Utils;

namespace ReToolbox.Services
{
    // Tracks the app's light/dark theme preference and applies it. Persisted in the
    // registry (HKLM\SOFTWARE\ReToolbox) like the rest of the app's state, since the
    // app runs elevated. The theme is set on the window's root element so all pages
    // and the Mica backdrop follow it.
    public static class ThemeService
    {
        private const string RegistryPath = @"HKLM\SOFTWARE\ReToolbox";
        private const string ThemeValue = "AppTheme";

        // The effective element theme for the current value. Defaults to dark (the app's
        // original look) when no preference is stored yet.
        public static ElementTheme Current
        {
            get
            {
                object? v = RegistryHelper.GetValue(RegistryPath, ThemeValue);
                return v is string s && Enum.TryParse<ElementTheme>(s, out ElementTheme t)
                    ? t
                    : ElementTheme.Dark;
            }
            set => RegistryHelper.SetValue(RegistryPath, ThemeValue, value.ToString(), RegistryValueKind.String);
        }

        // Applies <paramref name="theme"/> to <paramref name="root"/> and persists it.
        public static void Apply(FrameworkElement root, ElementTheme theme)
        {
            root.RequestedTheme = theme;
            Current = theme;
        }

        // Initializes <paramref name="root"/> from the stored preference without
        // writing back. Called once on startup.
        public static void Init(FrameworkElement root)
        {
            root.RequestedTheme = Current;
        }
    }
}
