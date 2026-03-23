using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DriveCare.Pages.User
{
    public partial class ProfilePage : Page
    {
        public ProfilePage()
        {
            InitializeComponent();
            DataContext = this;
            ShowTab(Tab.MainInfo);
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
        }

        private static void SetMenuState(Button button, bool isActive)
        {
            button.Background = isActive
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A2250"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A28"));
            button.BorderBrush = isActive
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7C6CF5"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E2E44"));
        }

        private void BtnMainInfo_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.MainInfo);
        private void BtnCars_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Cars);
        private void BtnEdit_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Edit);
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Settings);

        private void BackToHome_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<UserHomePage>();
    }
}

