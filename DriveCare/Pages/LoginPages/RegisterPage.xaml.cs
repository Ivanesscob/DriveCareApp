using DriveCareCore.Dialogs;
using DriveCareCore;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Pages.LoginPages
{
    public partial class RegisterPage : Page, INotifyPropertyChanged
    {
        private string _login = string.Empty;
        private string _email = string.Empty;

        public RegisterPage()
        {
            InitializeComponent();
            DataContext = this;

            BackCommand = new DelegateCommand(_ => BackExecute());
            RegisterCommand = new DelegateCommand(_ => RegisterExecute());
        }

        public string UserLogin
        {
            get => _login;
            set { _login = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value ?? string.Empty; OnPropertyChanged(); }
        }

        public DelegateCommand BackCommand { get; }
        public DelegateCommand RegisterCommand { get; }

        private void BackExecute()
        {
            if (AppState.MainFrame?.CanGoBack == true)
                AppState.MainFrame.GoBack();
            else
                AppState.MainFrame.Navigate(new LoginPage());
        }

        private void RegisterExecute()
        {
            var pwd = PasswordInput?.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(UserLogin) || string.IsNullOrWhiteSpace(pwd))
            {
                AppMessageBox.Show("Заполните все поля.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            AppMessageBox.Show("Регистрация будет подключена к базе данных.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
