using ControlzEx.Theming;
using DriveCarePro;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace DriveCarePro.Services
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

        /// <summary>Тема при запуске и на экране входа (общий файл на этом ПК).</summary>
        public static void Initialize()
        {
            Apply(AppearanceStore.Load(), persist: false);
        }

        /// <summary>Применить сохранённую тему текущего сотрудника.</summary>
        public static void LoadForCurrentEmployee()
        {
            if (AppState.CurrentEmployee == null)
            {
                Apply(AppearanceStore.Load(), persist: false);
                return;
            }

            var saved = EmployeeSettingsStore.Load(AppState.CurrentEmployee.RowId);
            Apply(Parse(saved.UiTheme), persist: false);
            AppearanceStore.Save(Current);
        }

        public static void Apply(AppUiTheme theme, bool persist)
        {
            Current = theme;
            var app = Application.Current;
            if (app == null)
                return;

            var mahTheme = theme == AppUiTheme.Dark ? "Dark.Blue" : "Light.Steel";
            ThemeManager.Current.ChangeTheme(app, mahTheme);
            SwitchPaletteMergedDictionary(app);

            if (persist)
            {
                AppearanceStore.Save(theme);
                if (AppState.CurrentEmployee != null)
                {
                    var s = EmployeeSettingsStore.Load(AppState.CurrentEmployee.RowId);
                    s.UiTheme = theme.ToString();
                    EmployeeSettingsStore.Save(AppState.CurrentEmployee.RowId, s);
                }
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

            var path = Current == AppUiTheme.Dark ? "Themes/Dark.xaml" : "Themes/Light.xaml";
            var uri = new Uri($"/DriveCarePro;component/{path}", UriKind.Relative);
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
                    d.Source.ToString().IndexOf("/DriveCarePro;component/Themes/", StringComparison.OrdinalIgnoreCase) >= 0);
            if (old != null)
                merged.Remove(old);
        }
    }
}
