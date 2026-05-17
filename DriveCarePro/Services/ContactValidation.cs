using System;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace DriveCarePro.Services
{
    internal static class ContactValidation
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
            if (at > 64)
                return false;
            if (t.Contains(".."))
                return false;
            if (!StrictEmailRegex.IsMatch(t))
                return false;

            try
            {
                _ = new MailAddress(t);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>РФ: 10 цифр с 9, или 11 с 7/8; иначе 10–15 цифр.</summary>
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

            return d.Length >= 10 && d.Length <= 15 && d[0] != '0';
        }
    }
}
