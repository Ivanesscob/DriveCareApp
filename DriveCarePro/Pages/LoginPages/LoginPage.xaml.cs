using DriveCareCore;
using DriveCareCore.Data.BD;
using DriveCareCore.Dialogs;
using DriveCarePro;
using DriveCarePro.Pages;
using DriveCarePro.Services;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.LoginPages
{
    public partial class LoginPage : Page, INotifyPropertyChanged
    {
        private string _username = string.Empty;

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

        public DelegateCommand LoginCommand { get; }
        public DelegateCommand RegisterCommand { get; }

        private void LoginExecute()
        {
            var password = PasswordInput?.Password ?? string.Empty;
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            {
                AppMessageBox.Show("Введите логин и пароль.", "DriveCare Pro", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var user = AppConnect.model1.Employees.FirstOrDefault(u =>
                u.Login == Username && u.Password == password);

            if (user != null)
            {
                AppState.CurrentUserId = user.RowId;
                AppState.CurrentEmployee = user;
                AppState.UserRoles = AppConnect.model1.EmployeeRolesMap
                    .Where(er => er.EmployeeId == user.RowId)
                    .Select(er => er.Roles)
                    .ToList();
                ThemeService.LoadForCurrentEmployee();
                AppState.SetFrame<ProHomePage>();
            }
            else
            {
                AppMessageBox.Show("Неверный логин или пароль.", "DriveCare Pro", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void RegisterExecute()
        {
            AppState.MainFrame.Navigate(new RegisterPage());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
