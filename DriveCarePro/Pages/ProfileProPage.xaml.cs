using DriveCareCore.Data.BD;
using DriveCarePro;
using DriveCarePro.Services;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DriveCarePro.Pages
{
    public partial class ProfileProPage : Page
    {
        private bool _suppressThemeChange;

        public ProfileProPage()
        {
            InitializeComponent();
            Loaded += (_, __) => BindEmployee();
        }

        private void BindEmployee()
        {
            var e = AppState.CurrentEmployee;
            EmployeeNameText.Text = AppState.FormatEmployeeDisplayName(e);
            HeroInitials.Text = BuildInitials(e);
            RoleBadgeText.Text = BuildRoleBadge();
            EmployeeLoginText.Text = e == null ? "—" : "@" + (e.Login ?? "—");
            EmployeeEmailText.Text = e == null || string.IsNullOrWhiteSpace(e.Email)
                ? "email не указан"
                : e.Email.Trim();
            EmployeePhoneText.Text = e == null || string.IsNullOrWhiteSpace(e.Phone)
                ? "Телефон не указан"
                : e.Phone.Trim();
            WorkshopText.Text = ResolveWorkshopLabel(e);

            _suppressThemeChange = true;
            try
            {
                if (ThemeService.Current == AppUiTheme.Light)
                    RadioThemeLight.IsChecked = true;
                else
                    RadioThemeDark.IsChecked = true;
            }
            finally
            {
                _suppressThemeChange = false;
            }

            UpdateThemeCardsVisual();
        }

        private static string BuildInitials(Employee e)
        {
            if (e == null)
                return "PR";
            var f = (e.FirstName ?? string.Empty).Trim();
            var l = (e.LastName ?? string.Empty).Trim();
            if (f.Length > 0 && l.Length > 0)
                return char.ToUpperInvariant(f[0]).ToString() + char.ToUpperInvariant(l[0]);
            if (f.Length > 0)
                return char.ToUpperInvariant(f[0]).ToString();
            if (l.Length > 0)
                return char.ToUpperInvariant(l[0]).ToString();
            var login = (e.Login ?? string.Empty).Trim();
            if (login.Length >= 2)
                return login.Substring(0, 2).ToUpperInvariant();
            return "PR";
        }

        private string BuildRoleBadge()
        {
            if (AppState.IsCurrentEmployeeProAdmin)
                return "Администратор платформы";
            if (AppState.IsCurrentEmployeeOwner)
                return "Владелец организации";
            if (AppState.IsCurrentEmployeeDealershipHead)
                return "Руководитель автосалона";
            if (AppState.IsCurrentEmployeeServiceWorker)
                return "Сотрудник сервиса";
            if (AppState.IsCurrentEmployeePurchaser)
                return "Закупщик";
            return "Сотрудник DriveCare Pro";
        }

        private static string ResolveWorkshopLabel(Employee e)
        {
            if (e?.WorkshopId == null || e.WorkshopId == Guid.Empty)
                return "Мастерская не привязана";

            try
            {
                var name = AppConnect.model1.Workshops
                    .Where(w => w.RowId == e.WorkshopId.Value)
                    .Select(w => w.Name)
                    .FirstOrDefault();
                return string.IsNullOrWhiteSpace(name) ? "Мастерская" : name.Trim();
            }
            catch
            {
                return "Мастерская";
            }
        }

        private void UpdateThemeCardsVisual()
        {
            var dark = ThemeService.Current == AppUiTheme.Dark;
            var accent = Application.Current?.TryFindResource("App.Brush.Accent") as Brush
                         ?? Brushes.MediumSlateBlue;
            var border = Application.Current?.TryFindResource("App.Brush.Border") as Brush
                         ?? Brushes.Gray;

            ThemeDarkCard.BorderBrush = dark ? accent : border;
            ThemeDarkCard.BorderThickness = new Thickness(dark ? 2 : 1);
            ThemeDarkCheck.Visibility = dark ? Visibility.Visible : Visibility.Collapsed;

            ThemeLightCard.BorderBrush = dark ? border : accent;
            ThemeLightCard.BorderThickness = new Thickness(dark ? 1 : 2);
            ThemeLightCheck.Visibility = dark ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ThemeDarkCard_Click(object sender, MouseButtonEventArgs e) => RadioThemeDark.IsChecked = true;

        private void ThemeLightCard_Click(object sender, MouseButtonEventArgs e) => RadioThemeLight.IsChecked = true;

        private void ThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressThemeChange || RadioThemeDark.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Dark, persist: true);
            UpdateThemeCardsVisual();
        }

        private void ThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressThemeChange || RadioThemeLight.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Light, persist: true);
            UpdateThemeCardsVisual();
        }

        private void Back_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<ProHomePage>();

        private void SignOut_Click(object sender, RoutedEventArgs e) => AppState.SignOutToLogin();
    }
}
