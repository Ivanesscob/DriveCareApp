using DriveCareCore;
using DriveCareCore.Data.BD;
using System;
using System.Linq;

namespace DriveCare.Services
{
    public static class UserProfileService
    {
        public static string GenerateVerificationCode() =>
            new Random().Next(10000, 100000).ToString("D5", System.Globalization.CultureInfo.InvariantCulture);

        public static RegistrationMailHelper.MailSendResult SendEmailVerificationCode(string email, string code) =>
            RegistrationMailHelper.TrySendVerificationCodeAsyncSafe(email, code);

        public static (bool ok, string error) SaveProfile(
            Guid userId,
            string login,
            string phone,
            DateTime? birthDate,
            string description,
            string newPassword,
            string newEmail,
            string originalEmail,
            string pendingEmailCode,
            string enteredEmailCode)
        {
            if (userId == Guid.Empty)
                return (false, "Пользователь не найден.");

            var loginTrim = (login ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(loginTrim))
                return (false, "Укажите логин.");

            if (!UserInputValidators.IsValidPhone(phone))
                return (false, "Укажите корректный телефон.");

            if (!birthDate.HasValue)
                return (false, "Укажите дату рождения.");

            var birth = birthDate.Value.Date;
            if (birth > DateTime.Today)
                return (false, "Дата рождения не может быть в будущем.");
            if (!UserInputValidators.IsAtLeastYearsOld(birth, 18))
                return (false, "Использование сервиса доступно с 18 лет.");

            var emailTrim = (newEmail ?? string.Empty).Trim();
            if (!UserInputValidators.IsValidEmail(emailTrim))
                return (false, "Укажите корректный email.");

            var orig = (originalEmail ?? string.Empty).Trim();
            var emailChanged = !string.Equals(emailTrim, orig, StringComparison.OrdinalIgnoreCase);
            if (emailChanged)
            {
                if (string.IsNullOrEmpty(pendingEmailCode))
                    return (false, "Сначала отправьте код подтверждения на новую почту.");
                var entered = (enteredEmailCode ?? string.Empty).Trim();
                if (entered.Length != 5 || !UserInputValidators.IsAllDigits(entered))
                    return (false, "Введите 5-значный код из письма.");
                if (!string.Equals(entered, pendingEmailCode, StringComparison.Ordinal))
                    return (false, "Неверный код подтверждения email.");
            }

            if (!string.IsNullOrEmpty(newPassword) && newPassword.Length < 4)
                return (false, "Новый пароль слишком короткий (минимум 4 символа).");

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    var user = db.Users.FirstOrDefault(u => u.RowId == userId);
                    if (user == null)
                        return (false, "Пользователь не найден.");

                    if (db.Users.Any(u => u.RowId != userId && u.Login == loginTrim))
                        return (false, "Этот логин уже занят.");

                    if (db.Users.Any(u => u.RowId != userId && u.Email == emailTrim))
                        return (false, "Этот email уже используется.");

                    user.Login = loginTrim;
                    user.Phone = phone.Trim();
                    user.BirthDate = birth;
                    user.Email = emailTrim;
                    user.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

                    if (!string.IsNullOrEmpty(newPassword))
                        user.Password = newPassword;

                    db.SaveChanges();
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
