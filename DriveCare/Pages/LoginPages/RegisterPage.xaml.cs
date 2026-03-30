using DriveCare.Services;
using DriveCareCore;
using DriveCareCore.Data.BD;
using DriveCareCore.Dialogs;
using System;
using System.ComponentModel;
using System.Linq;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DriveCare.Pages.LoginPages
{
    public partial class RegisterPage : Page, INotifyPropertyChanged
    {
        /// <summary>Латиница/цифры, один @, зона не короче 2 букв; плюс проверка MailAddress.</summary>
        private static readonly Regex StrictEmailRegex = new Regex(
            @"^[a-zA-Z0-9]([a-zA-Z0-9._%+-]*[a-zA-Z0-9])?@[a-zA-Z0-9]([a-zA-Z0-9.-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled);

        private string _login = string.Empty;
        private string _email = string.Empty;
        private string _phone = string.Empty;
        private string _verificationCode = string.Empty;
        private DateTime? _birthDate;
        private bool _showFormStep = true;
        private bool _showVerificationStep;
        private string _pendingEmailCode = string.Empty;

        public RegisterPage()
        {
            InitializeComponent();
            DataContext = this;

            Loaded += RegisterPage_Loaded;

            BackCommand = new DelegateCommand(delegate (object o) { BackExecute(); });
            SendCodeCommand = new DelegateCommand(delegate (object o) { SendCodeExecute(); });
            CompleteRegistrationCommand = new DelegateCommand(delegate (object o) { CompleteRegistrationExecute(); });
        }

        public string UserLogin
        {
            get { return _login; }
            set
            {
                _login = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string Email
        {
            get { return _email; }
            set
            {
                _email = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string Phone
        {
            get { return _phone; }
            set
            {
                _phone = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string VerificationCode
        {
            get { return _verificationCode; }
            set
            {
                _verificationCode = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public DateTime? BirthDate
        {
            get { return _birthDate; }
            set
            {
                _birthDate = value;
                OnPropertyChanged();
            }
        }

        public bool ShowFormStep
        {
            get { return _showFormStep; }
            set
            {
                _showFormStep = value;
                OnPropertyChanged();
                OnPropertyChanged("FormStepVisibility");
            }
        }

        public bool ShowVerificationStep
        {
            get { return _showVerificationStep; }
            set
            {
                _showVerificationStep = value;
                OnPropertyChanged();
                OnPropertyChanged("VerificationStepVisibility");
            }
        }

        public Visibility FormStepVisibility
        {
            get { return _showFormStep ? Visibility.Visible : Visibility.Collapsed; }
        }

        public Visibility VerificationStepVisibility
        {
            get { return _showVerificationStep ? Visibility.Visible : Visibility.Collapsed; }
        }

        public DelegateCommand BackCommand { get; private set; }
        public DelegateCommand SendCodeCommand { get; private set; }
        public DelegateCommand CompleteRegistrationCommand { get; private set; }

        private void RegisterPage_Loaded(object sender, RoutedEventArgs e)
        {
            var dp = FindName("BirthDatePicker") as DatePicker;
            if (dp != null)
            {
                dp.DisplayDateEnd = DateTime.Today.AddYears(-18);
                dp.DisplayDateStart = DateTime.Today.AddYears(-100);
            }
        }

        private void BackExecute()
        {
            if (ShowVerificationStep && !ShowFormStep)
            {
                ShowVerificationStep = false;
                ShowFormStep = true;
                VerificationCode = string.Empty;
                _pendingEmailCode = string.Empty;
                return;
            }

            if (AppState.MainFrame != null && AppState.MainFrame.CanGoBack)
                AppState.MainFrame.GoBack();
            else
                AppState.MainFrame.Navigate(new LoginPage());
        }

        private static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            var t = email.Trim();
            if (t.Length > 254)
                return false;
            var at = t.IndexOf('@');
            if (at <= 0 || at != t.LastIndexOf('@'))
                return false;
            if (at > 64)
                return false;
            if (t.Contains(".."))
                return false;
            if (!StrictEmailRegex.IsMatch(t))
                return false;
            try
            {
                new MailAddress(t);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            var digits = new StringBuilder(20);
            for (var i = 0; i < phone.Length; i++)
            {
                if (char.IsDigit(phone[i]))
                    digits.Append(phone[i]);
            }

            if (digits.Length < 10)
                return false;

            var d = digits.ToString();

            if (d.Length == 10 && d[0] == '9')
                return true;

            if (d.Length == 11 && (d[0] == '7' || d[0] == '8') && d[1] == '9')
                return true;

            if (d.Length >= 10 && d.Length <= 15 && d[0] != '0')
                return true;

            return false;
        }

        private static bool IsAtLeastYearsOld(DateTime birthDate, int years)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age))
                age--;
            return age >= years;
        }

        private static string GetPassword(PasswordBox box)
        {
            return box != null ? box.Password : string.Empty;
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsDigit(s[i]))
                    return false;
            }
            return true;
        }

        private void SendCodeExecute()
        {
            var pwd = GetPassword(PasswordInput);
            var pwd2 = GetPassword(PasswordConfirmInput);

            if (string.IsNullOrWhiteSpace(UserLogin) || string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Phone) || string.IsNullOrWhiteSpace(pwd))
            {
                AppMessageBox.Show("Заполните все поля.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!IsValidEmail(Email))
            {
                AppMessageBox.Show("Укажите корректный email (латиница, имя@домен, без пробелов).", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidPhone(Phone))
            {
                AppMessageBox.Show("Укажите корректный номер телефона (для РФ: +7, 8 или 10 цифр с 9; либо 10–15 цифр международного номера).", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pwd != pwd2)
            {
                AppMessageBox.Show("Пароли не совпадают.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (pwd.Length < 4)
            {
                AppMessageBox.Show("Пароль слишком короткий.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!BirthDate.HasValue)
            {
                AppMessageBox.Show("Укажите дату рождения.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var birth = BirthDate.Value.Date;
            if (birth > DateTime.Today)
            {
                AppMessageBox.Show("Дата рождения не может быть в будущем.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (birth < DateTime.Today.AddYears(-120))
            {
                AppMessageBox.Show("Проверьте дату рождения.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsAtLeastYearsOld(birth, 18))
            {
                AppMessageBox.Show("Регистрация доступна с 18 лет.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var loginTrim = UserLogin.Trim();
            var emailTrim = Email.Trim();

            if (AppConnect.model1.Users.Any(u => u.Login == loginTrim))
            {
                AppMessageBox.Show("Пользователь с таким логином уже существует.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var code = new Random().Next(10000, 100000).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);
            _pendingEmailCode = code;

            Mouse.OverrideCursor = Cursors.Wait;
            Task.Factory.StartNew(() => RegistrationMailHelper.TrySendVerificationCodeAsyncSafe(emailTrim, code))
                .ContinueWith(
                    t =>
                    {
                        Dispatcher.BeginInvoke(
                            new Action(
                                delegate
                                {
                                    Mouse.OverrideCursor = null;

                                    if (t.IsFaulted)
                                    {
                                        _pendingEmailCode = string.Empty;
                                        var ex = t.Exception != null ? t.Exception.GetBaseException() : null;
                                        var msg = ex != null ? ex.Message : "Неизвестная ошибка";
                                        AppMessageBox.Show("Ошибка при отправке: " + msg, "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }

                                    var r = t.Result;
                                    if (r.Outcome == RegistrationMailHelper.SendOutcome.Sent)
                                    {
                                        AppMessageBox.Show("Код отправлен на почту.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                                    }
                                    else
                                    {
                                        var err = string.IsNullOrEmpty(r.ErrorMessage) ? "ошибка" : r.ErrorMessage;
                                        AppMessageBox.Show("Не удалось отправить письмо: " + err, "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
                                        _pendingEmailCode = string.Empty;
                                        return;
                                    }

                                    ShowFormStep = false;
                                    ShowVerificationStep = true;
                                    VerificationCode = string.Empty;
                                }));
                    },
                    TaskScheduler.Default);
        }

        private void CompleteRegistrationExecute()
        {
            if (string.IsNullOrEmpty(_pendingEmailCode))
            {
                AppMessageBox.Show("Сначала запросите код на почту.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var entered = (VerificationCode ?? string.Empty).Trim();
            if (entered.Length != 5 || !IsAllDigits(entered))
            {
                AppMessageBox.Show("Введите 5-значный код из письма.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.Equals(entered, _pendingEmailCode, StringComparison.Ordinal))
            {
                AppMessageBox.Show("Неверный код.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pwd = GetPassword(PasswordInput);
            var pwd2 = GetPassword(PasswordConfirmInput);
            if (pwd != pwd2 || string.IsNullOrEmpty(pwd))
            {
                AppMessageBox.Show("Пароли не совпадают или пусты. Вернитесь назад и заполните форму снова.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var loginTrim = UserLogin.Trim();
            var emailTrim = Email.Trim();

            if (AppConnect.model1.Users.Any(u => u.Login == loginTrim))
            {
                AppMessageBox.Show("Пользователь с таким логином уже существует. Войдите в приложение.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!BirthDate.HasValue)
            {
                AppMessageBox.Show("Нет даты рождения. Вернитесь к форме.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidEmail(Email) || !IsValidPhone(Phone))
            {
                AppMessageBox.Show("Проверьте email и телефон и повторите отправку кода.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var birthDate = BirthDate.Value.Date;
            if (!IsAtLeastYearsOld(birthDate, 18))
            {
                AppMessageBox.Show("Регистрация доступна с 18 лет.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var phoneTrim = Phone.Trim();

            var user = new Users
            {
                RowId = Guid.NewGuid(),
                Login = loginTrim,
                Password = pwd,
                Email = emailTrim,
                Phone = phoneTrim,
                BirthDate = birthDate,
                CreatedAt = DateTime.Now,
                Description = null
            };

            try
            {
                AppConnect.model1.Users.Add(user);
                AppConnect.model1.SaveChanges();
            }
            catch (Exception ex)
            {
                AppMessageBox.Show("Ошибка сохранения: " + ex.Message, "DriveCare", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _pendingEmailCode = string.Empty;
            AppMessageBox.Show("Регистрация завершена. Теперь можно войти.", "DriveCare", MessageBoxButton.OK, MessageBoxImage.Information);
            AppState.MainFrame.Navigate(new LoginPage());
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var h = PropertyChanged;
            if (h != null)
                h(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
