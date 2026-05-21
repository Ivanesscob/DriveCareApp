using System;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace DriveCare.Services
{
    internal static class UserInputValidators
    {
        private static readonly Regex StrictEmailRegex = new Regex(
            @"^[a-zA-Z0-9]([a-zA-Z0-9._%+-]*[a-zA-Z0-9])?@[a-zA-Z0-9]([a-zA-Z0-9.-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,}$",
            RegexOptions.Compiled);

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;
            var t = email.Trim();
            if (t.Length > 254)
                return false;
            var at = t.IndexOf('@');
            if (at <= 0 || at != t.LastIndexOf('@'))
                return false;
            if (at > 64 || t.Contains(".."))
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

        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return false;

            var digits = new StringBuilder(20);
            foreach (var ch in phone)
            {
                if (char.IsDigit(ch))
                    digits.Append(ch);
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

        public static bool IsAtLeastYearsOld(DateTime birthDate, int years)
        {
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate > today.AddYears(-age))
                age--;
            return age >= years;
        }

        public static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            foreach (var ch in s)
            {
                if (!char.IsDigit(ch))
                    return false;
            }
            return true;
        }
    }
}
