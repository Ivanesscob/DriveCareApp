using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using ControlzEx.Theming;
using DriveCare.Properties;

namespace DriveCare.Services
{
    public enum AppUiTheme
    {
        Dark,
        Light
    }

    public static class ThemeService
    {
        private static ResourceDictionary _paletteDictionary;

        public static AppUiTheme Current { get; private set; } = AppUiTheme.Dark;

        public static event EventHandler ThemeChanged;

        public static void Initialize()
        {
            var parsed = Parse(Settings.Default.UiTheme);
            Apply(parsed, persist: false);
        }

        public static void Apply(AppUiTheme theme, bool persist = true)
        {
            Current = theme;
            var app = Application.Current;
            if (app == null)
                return;

            // Светлая: Steel — нейтральные рамки/заголовки без «фиолетового» MahApps.Blue. Тёмная: Blue — фирменный акцент.
            var mahTheme = theme == AppUiTheme.Dark ? "Dark.Blue" : "Light.Steel";
            ThemeManager.Current.ChangeTheme(app, mahTheme);

            SwitchPaletteMergedDictionary(app);

            ThemeChanged?.Invoke(null, EventArgs.Empty);

            if (persist)
            {
                Settings.Default.UiTheme = theme.ToString();
                Settings.Default.Save();
            }
        }

        public static AppUiTheme Parse(string value)
        {
            if (string.Equals(value, nameof(AppUiTheme.Light), StringComparison.OrdinalIgnoreCase))
                return AppUiTheme.Light;
            return AppUiTheme.Dark;
        }

        private static void SwitchPaletteMergedDictionary(Application app)
        {
            var md = app.Resources.MergedDictionaries;
            RemovePalette(md);

            var path = Current == AppUiTheme.Dark
                ? "Themes/Dark.xaml"
                : "Themes/Light.xaml";
            var uri = new Uri($"/DriveCare;component/{path}", UriKind.Relative);
            _paletteDictionary = new ResourceDictionary { Source = uri };

            md.Add(_paletteDictionary);
        }

        private static void RemovePalette(Collection<ResourceDictionary> merged)
        {
            if (_paletteDictionary != null)
            {
                if (merged.Contains(_paletteDictionary))
                    merged.Remove(_paletteDictionary);
                _paletteDictionary = null;
                return;
            }

            var old = merged.OfType<ResourceDictionary>()
                .FirstOrDefault(d =>
                    d.Source != null &&
                    d.Source.ToString().IndexOf("/DriveCare;component/Themes/", StringComparison.OrdinalIgnoreCase) >= 0);
            if (old != null)
                merged.Remove(old);
        }
    }
}
