using DriveCareCore;
using DriveCareCore.Data.BD;
using DriveCareCore.Dialogs;
using DriveCarePro;
using DriveCarePro.Pages;
using DriveCarePro.Services;
using System.ComponentModel;
using System.Data.Entity;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DriveCarePro.Pages.LoginPages
{
    public partial class LoginPage : Page, INotifyPropertyChanged
    {
        private string _username = string.Empty;

        public LoginPage()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += LoginPage_Loaded;

            LoginCommand = new DelegateCommand(async _ => await LoginExecuteAsync());
            RegisterCommand = new DelegateCommand(_ => RegisterExecute());
        }

        private void LoginPage_Loaded(object sender, RoutedEventArgs e)
        {
            var dbError = AppState.TakeConnectionErrorMessage();
            if (string.IsNullOrWhiteSpace(dbError))
            {
                ConnectionErrorBanner.Visibility = Visibility.Collapsed;
                return;
            }

            ConnectionErrorText.Text = dbError;
            ConnectionErrorBanner.Visibility = Visibility.Visible;
        }

        public string Username
        {
            get => _username;
            set { _username = value ?? string.Empty; OnPropertyChanged(); }
        }

        public DelegateCommand LoginCommand { get; }
        public DelegateCommand RegisterCommand { get; }

        private async System.Threading.Tasks.Task LoginExecuteAsync()
        {
            var password = PasswordInput?.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                AppMessageBox.Show("Введите логин и пароль.", "DriveCare Pro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var login = Username.Trim();
            IsBusy = true;
            try
            {
                var user = await DatabaseExecutor.WithDbAsync(db =>
                    db.Employees.FirstOrDefaultAsync(u =>
                        u.Login == login && u.Password == password)).ConfigureAwait(true);

                if (user != null)
                {
                    await AppState.SignInEmployeeAsync(user).ConfigureAwait(true);
                    DriveCareCore.Analytics.ActivityTracker.TrackEmployee(
                        DriveCareCore.Analytics.ActivityEventCodes.ProEmployeeLogin,
                        user.RowId,
                        user.WorkshopId);
                    AppState.SetFrame<ProHomePage>();
                }
                else
                {
                    AppMessageBox.Show("Неверный логин или пароль.", "DriveCare Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (System.Exception ex) when (AppState.IsDatabaseConnectionError(ex))
            {
                ConnectionErrorText.Text = AppState.BuildConnectionErrorMessage(ex);
                ConnectionErrorBanner.Visibility = Visibility.Visible;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;
                _isBusy = value;
                OnPropertyChanged();
            }
        }

        private void RegisterExecute()
        {
            AppState.MainFrame.Navigate(new RegisterPage());
        }

        private void Page_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            TryLoginFromEnter();
            e.Handled = true;
        }

        private void PasswordInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;
            TryLoginFromEnter();
            e.Handled = true;
        }

        void TryLoginFromEnter()
        {
            if (IsBusy)
                return;
            if (LoginCommand?.CanExecute(null) == true)
                LoginCommand.Execute(null);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
