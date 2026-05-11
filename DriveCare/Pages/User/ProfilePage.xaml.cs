using DriveCare.Pages.LoginPages;
using DriveCare.Services;
using DriveCareCore.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveCare.Pages.User
{
    public partial class ProfilePage : Page
    {
        private bool _suppressThemeRadios;

        public ProfilePage()
        {
            InitializeComponent();
            DataContext = this;
            ShowTab(Tab.MainInfo);
            Loaded += (_, __) => SyncThemeRadios();
        }

        public DriveCareCore.Data.BD.Users CurrentUser => AppState.CurrentUser;

        private enum Tab
        {
            MainInfo,
            Cars,
            Edit,
            Settings
        }

        private void ShowTab(Tab tab)
        {
            TabMainInfo.Visibility = tab == Tab.MainInfo ? Visibility.Visible : Visibility.Collapsed;
            TabCars.Visibility = tab == Tab.Cars ? Visibility.Visible : Visibility.Collapsed;
            TabEdit.Visibility = tab == Tab.Edit ? Visibility.Visible : Visibility.Collapsed;
            TabSettings.Visibility = tab == Tab.Settings ? Visibility.Visible : Visibility.Collapsed;

            SetMenuState(BtnMainInfo, tab == Tab.MainInfo);
            SetMenuState(BtnCars, tab == Tab.Cars);
            SetMenuState(BtnEdit, tab == Tab.Edit);
            SetMenuState(BtnSettings, tab == Tab.Settings);

            if (tab == Tab.Settings)
                SyncThemeRadios();
        }

        private static void SetMenuState(Button button, bool isActive)
        {
            var app = Application.Current;
            Brush activeBg = app?.TryFindResource("App.Brush.MenuBgActive") as Brush;
            Brush inactiveBg = app?.TryFindResource("App.Brush.Surface2") as Brush;
            Brush accent = app?.TryFindResource("App.Brush.Accent") as Brush;
            Brush border = app?.TryFindResource("App.Brush.Border") as Brush;

            button.Background = isActive ? (activeBg ?? inactiveBg) : inactiveBg;
            button.BorderBrush = isActive ? (accent ?? border) : border;
        }

        private void SyncThemeRadios()
        {
            _suppressThemeRadios = true;
            if (ThemeService.Current == AppUiTheme.Dark)
                RbThemeDark.IsChecked = true;
            else
                RbThemeLight.IsChecked = true;
            _suppressThemeRadios = false;
        }

        private void RbThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _suppressThemeRadios || RbThemeDark.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Dark);
        }

        private void RbThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _suppressThemeRadios || RbThemeLight.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Light);
        }

        private void BtnMainInfo_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.MainInfo);
        private void BtnCars_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Cars);
        private void BtnEdit_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Edit);
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Settings);

        private void BtnSignOut_Click(object sender, RoutedEventArgs e)
        {
            var result = AppMessageBox.Show(
                "Выйти из аккаунта и вернуться на экран входа?",
                "DriveCare",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            AppState.SignOut();
            AppState.SetFrame<LoginPage>();
        }

        private void BackToHome_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<UserHomePage>();
    }
}

