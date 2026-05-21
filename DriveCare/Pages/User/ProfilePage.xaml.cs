using DriveCare.Pages.LoginPages;
using DriveCare.Services;
using DriveCare.Windows;
using DriveCareCore.Data.BD;
using DriveCareCore.Dialogs;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using SysTask = System.Threading.Tasks.Task;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DriveCare.Pages.User
{
    public partial class ProfilePage : Page
    {
        private bool _suppressThemeRadios;
        private string _originalEmail = string.Empty;
        private string _pendingEmailCode = string.Empty;
        private string _pendingEmailTarget = string.Empty;
        private readonly ObservableCollection<UserGarageCarItem> _garageCars = new ObservableCollection<UserGarageCarItem>();

        public ProfilePage()
        {
            InitializeComponent();
            DataContext = this;
            CarsList.ItemsSource = _garageCars;
            ShowTab(Tab.MainInfo);
            Loaded += ProfilePage_Loaded;
        }

        public DriveCareCore.Data.BD.User CurrentUser => AppState.CurrentUser;

        private enum Tab
        {
            MainInfo,
            Cars,
            Settings
        }

        private void ProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            EditBirthDatePicker.DisplayDateEnd = DateTime.Today.AddYears(-18);
            EditBirthDatePicker.DisplayDateStart = DateTime.Today.AddYears(-100);
            BindProfileForm();
            SyncThemeRadios();
            LoadGarageCars();
            RefreshHero();
        }

        private void BindProfileForm()
        {
            var user = CurrentUser;
            if (user == null)
                return;

            EditLoginBox.Text = user.Login ?? string.Empty;
            EditPhoneBox.Text = user.Phone ?? string.Empty;
            EditEmailBox.Text = user.Email ?? string.Empty;
            EditDescriptionBox.Text = user.Description ?? string.Empty;
            _originalEmail = (user.Email ?? string.Empty).Trim();

            if (user.BirthDate.HasValue)
                EditBirthDatePicker.SelectedDate = user.BirthDate.Value;
            else
                EditBirthDatePicker.SelectedDate = DateTime.Today.AddYears(-18);

            RegisteredAtText.Text = user.CreatedAt != default
                ? "Зарегистрирован: " + user.CreatedAt.ToString("dd.MM.yyyy")
                : string.Empty;

            EditNewPasswordBox.Password = string.Empty;
            EditConfirmPasswordBox.Password = string.Empty;
            EditEmailCodeBox.Text = string.Empty;
            _pendingEmailCode = string.Empty;
            _pendingEmailTarget = string.Empty;
            EmailVerifyPanel.Visibility = Visibility.Collapsed;
        }

        private void RefreshHero()
        {
            var user = CurrentUser;
            if (user == null)
            {
                HeroInitials.Text = "?";
                HeroLogin.Text = "Гость";
                HeroEmail.Text = string.Empty;
                HeroMemberSince.Text = string.Empty;
                HeroCarsCount.Text = "0";
                return;
            }

            var login = (user.Login ?? string.Empty).Trim();
            HeroLogin.Text = string.IsNullOrEmpty(login) ? "Профиль" : login;
            HeroInitials.Text = BuildInitials(login);
            HeroEmail.Text = string.IsNullOrWhiteSpace(user.Email)
                ? "Email не указан"
                : user.Email.Trim();
            HeroMemberSince.Text = user.CreatedAt != default
                ? "В DriveCare с " + user.CreatedAt.ToString("dd.MM.yyyy")
                : string.Empty;
            HeroCarsCount.Text = _garageCars.Count.ToString();
        }

        private static string BuildInitials(string login)
        {
            if (string.IsNullOrWhiteSpace(login))
                return "DC";
            var t = login.Trim();
            return t.Length >= 2 ? t.Substring(0, 2).ToUpperInvariant() : t.Substring(0, 1).ToUpperInvariant();
        }

        private void LoadGarageCars()
        {
            _garageCars.Clear();
            if (AppState.CurrentUserId == Guid.Empty)
            {
                UpdateCarsEmptyState();
                HeroCarsCount.Text = "0";
                return;
            }

            foreach (var item in UserGarageService.LoadForUser(AppState.CurrentUserId))
                _garageCars.Add(item);

            HeroCarsCount.Text = _garageCars.Count.ToString();
            UpdateCarsEmptyState();
        }

        private void UpdateCarsEmptyState()
        {
            var empty = _garageCars.Count == 0;
            CarsEmptyPanel.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            CarsList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ShowTab(Tab tab)
        {
            TabMainInfo.Visibility = tab == Tab.MainInfo ? Visibility.Visible : Visibility.Collapsed;
            TabCars.Visibility = tab == Tab.Cars ? Visibility.Visible : Visibility.Collapsed;
            TabSettings.Visibility = tab == Tab.Settings ? Visibility.Visible : Visibility.Collapsed;

            SetMenuState(BtnMainInfo, tab == Tab.MainInfo);
            SetMenuState(BtnCars, tab == Tab.Cars);
            SetMenuState(BtnSettings, tab == Tab.Settings);

            if (tab == Tab.Cars)
                LoadGarageCars();
            if (tab == Tab.MainInfo)
                BindProfileForm();
            if (tab == Tab.Settings)
                SyncThemeRadios();
        }

        private static void SetMenuState(Button button, bool isActive) =>
            button.Tag = isActive ? "Active" : null;

        private void BtnSendEmailCode_Click(object sender, RoutedEventArgs e)
        {
            var email = (EditEmailBox.Text ?? string.Empty).Trim();
            if (!UserInputValidators.IsValidEmail(email))
            {
                AppMessageBox.Show("Укажите корректный email.", "Профиль",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.Equals(email, _originalEmail, StringComparison.OrdinalIgnoreCase))
            {
                AppMessageBox.Show("Email не изменился — код не нужен.", "Профиль",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var code = UserProfileService.GenerateVerificationCode();
            _pendingEmailCode = code;
            _pendingEmailTarget = email;

            Mouse.OverrideCursor = Cursors.Wait;
            SysTask.Factory.StartNew(() => UserProfileService.SendEmailVerificationCode(email, code))
                .ContinueWith(t =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Mouse.OverrideCursor = null;
                        if (t.IsFaulted)
                        {
                            _pendingEmailCode = string.Empty;
                            _pendingEmailTarget = string.Empty;
                            AppMessageBox.Show("Ошибка отправки.", "Профиль",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        var r = t.Result;
                        if (r.Outcome != RegistrationMailHelper.SendOutcome.Sent)
                        {
                            _pendingEmailCode = string.Empty;
                            _pendingEmailTarget = string.Empty;
                            var err = string.IsNullOrEmpty(r.ErrorMessage) ? "не удалось отправить" : r.ErrorMessage;
                            AppMessageBox.Show("Не удалось отправить письмо: " + err, "Профиль",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }

                        EmailVerifyPanel.Visibility = Visibility.Visible;
                        EmailVerifyHint.Text = "Код отправлен на " + email + ". Введите его и нажмите «Сохранить профиль».";
                        AppMessageBox.Show("Код отправлен на почту.", "Профиль",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }));
                }, System.Threading.Tasks.TaskScheduler.Default);
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.CurrentUserId == Guid.Empty)
                return;

            var newPwd = EditNewPasswordBox.Password ?? string.Empty;
            var confirm = EditConfirmPasswordBox.Password ?? string.Empty;
            if (!string.IsNullOrEmpty(newPwd) && newPwd != confirm)
            {
                AppMessageBox.Show("Пароли не совпадают.", "Профиль",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newEmail = (EditEmailBox.Text ?? string.Empty).Trim();
            var emailChanged = !string.Equals(newEmail, _originalEmail, StringComparison.OrdinalIgnoreCase);
            if (emailChanged && !string.Equals(newEmail, _pendingEmailTarget, StringComparison.OrdinalIgnoreCase))
            {
                AppMessageBox.Show("Email изменён — сначала отправьте код на новую почту.", "Профиль",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (ok, error) = UserProfileService.SaveProfile(
                AppState.CurrentUserId,
                EditLoginBox.Text,
                EditPhoneBox.Text,
                EditBirthDatePicker.SelectedDate,
                EditDescriptionBox.Text,
                string.IsNullOrEmpty(newPwd) ? null : newPwd,
                newEmail,
                _originalEmail,
                emailChanged ? _pendingEmailCode : null,
                EditEmailCodeBox.Text);

            if (!ok)
            {
                AppMessageBox.Show(error ?? "Не удалось сохранить.", "Профиль",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _pendingEmailCode = string.Empty;
            _pendingEmailTarget = string.Empty;
            EmailVerifyPanel.Visibility = Visibility.Collapsed;

            var refreshed = AppConnect.model1.Users.FirstOrDefault(u => u.RowId == AppState.CurrentUserId);
            if (refreshed != null)
                AppState.CurrentUser = refreshed;

            _originalEmail = (AppState.CurrentUser?.Email ?? string.Empty).Trim();
            BindProfileForm();
            RefreshHero();
            AppMessageBox.Show("Профиль сохранён.", "DriveCare",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAddCar_Click(object sender, RoutedEventArgs e)
        {
            if (AppState.CurrentUserId == Guid.Empty)
                return;

            var win = new ProfileCarEditWindow(AppState.CurrentUserId) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                LoadGarageCars();
        }

        private void EditGarageCar_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is Guid userCarId) || userCarId == Guid.Empty)
                return;

            var item = _garageCars.FirstOrDefault(c => c.UserCarId == userCarId);
            if (item == null)
                return;

            var win = new ProfileCarEditWindow(AppState.CurrentUserId, item) { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true)
                LoadGarageCars();
        }

        private void DeleteGarageCar_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is Guid userCarId) || userCarId == Guid.Empty)
                return;

            if (AppMessageBox.Show("Удалить автомобиль из гаража?", "Мои машины",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var (ok, error) = UserGarageService.Delete(userCarId, AppState.CurrentUserId);
            if (!ok)
            {
                AppMessageBox.Show(error ?? "Не удалось удалить.", "Мои машины",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadGarageCars();
        }

        private void SyncThemeRadios()
        {
            _suppressThemeRadios = true;
            if (ThemeService.Current == AppUiTheme.Dark)
                RbThemeDark.IsChecked = true;
            else
                RbThemeLight.IsChecked = true;
            _suppressThemeRadios = false;
            UpdateThemeCardsVisual();
        }

        private void UpdateThemeCardsVisual()
        {
            var dark = ThemeService.Current == AppUiTheme.Dark;
            var accent = Application.Current?.TryFindResource("App.Brush.Accent") as Brush ?? Brushes.DodgerBlue;
            var border = Application.Current?.TryFindResource("App.Brush.Border") as Brush ?? Brushes.Gray;

            ThemeDarkCard.BorderBrush = dark ? accent : border;
            ThemeDarkCard.BorderThickness = new Thickness(dark ? 2 : 1);
            ThemeDarkCheck.Visibility = dark ? Visibility.Visible : Visibility.Collapsed;

            ThemeLightCard.BorderBrush = dark ? border : accent;
            ThemeLightCard.BorderThickness = new Thickness(dark ? 1 : 2);
            ThemeLightCheck.Visibility = dark ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ThemeDarkCard_Click(object sender, MouseButtonEventArgs e) => RbThemeDark.IsChecked = true;

        private void ThemeLightCard_Click(object sender, MouseButtonEventArgs e) => RbThemeLight.IsChecked = true;

        private void RbThemeDark_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _suppressThemeRadios || RbThemeDark.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Dark);
            UpdateThemeCardsVisual();
        }

        private void RbThemeLight_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded || _suppressThemeRadios || RbThemeLight.IsChecked != true)
                return;
            ThemeService.Apply(AppUiTheme.Light);
            UpdateThemeCardsVisual();
        }

        private void BtnMainInfo_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.MainInfo);
        private void BtnCars_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Cars);
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => ShowTab(Tab.Settings);

        private void BtnSignOut_Click(object sender, RoutedEventArgs e)
        {
            if (AppMessageBox.Show("Выйти из аккаунта?", "DriveCare",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            AppState.SignOut();
            AppState.SetFrame<LoginPage>();
        }

        private void BackToHome_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<UserHomePage>();
    }
}
