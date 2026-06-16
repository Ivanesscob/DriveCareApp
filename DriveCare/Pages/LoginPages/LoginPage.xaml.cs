using DriveCareCore;
using DriveCareCore.Data.BD;
using DriveCareCore.Dialogs;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DriveCare.Pages.User;
using AppState = DriveCare.AppState;

namespace DriveCare.Pages.LoginPages
{
    public partial class LoginPage : Page, INotifyPropertyChanged
    {
        private string _username = string.Empty;
        private string _password = string.Empty;

        public LoginPage()
        {
            InitializeComponent();
            DataContext = this;

            LoginCommand = new DelegateCommand(_ => LoginExecute());
            RegisterCommand = new DelegateCommand(_ => RegisterExecute());
        }

        public string Username
        {
            get => _username;
            set { _username = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string Password
        {
            get => _password;
            set { _password = value ?? string.Empty; OnPropertyChanged(); }
        }

        public DelegateCommand LoginCommand { get; }
        public DelegateCommand RegisterCommand { get; }

        private void LoginExecute()
        {
            var password = PasswordInput?.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                AppMessageBox.Show("Введите логин и пароль.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            
                var user = AppConnect.model1.Users.FirstOrDefault(u =>
                    u.Login == Username && u.Password == password);

                if (user != null)
                {
                    AppState.SignInUser(user);
                    DriveCareCore.Analytics.ActivityTracker.TrackUser(
                        DriveCareCore.Analytics.ActivityEventCodes.UserLogin, user.RowId);
                    AppState.SetFrame<UserHomePage>();
                }
                else
                {
                    AppMessageBox.Show("Неверный логин или пароль.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
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
