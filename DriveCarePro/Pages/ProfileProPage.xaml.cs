using DriveCarePro;
using DriveCarePro.Services;
using System.Windows;
using System.Windows.Controls;

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
            EmployeeLoginText.Text = e == null ? "—" : $"Логин: {e.Login ?? "—"}";
            EmployeeEmailText.Text = e == null || string.IsNullOrWhiteSpace(e.Email)
                ? "Email не указан"
                : $"Email: {e.Email.Trim()}";

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
        }

        private void ThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressThemeChange || RadioThemeDark.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Dark, persist: true);
        }

        private void ThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressThemeChange || RadioThemeLight.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Light, persist: true);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<ProHomePage>();
        }

        private void SignOut_Click(object sender, RoutedEventArgs e)
        {
            AppState.SignOutToLogin();
        }
    }
}
