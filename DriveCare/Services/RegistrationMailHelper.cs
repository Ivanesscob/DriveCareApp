using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;

namespace DriveCare.Services
{
    public static class RegistrationMailHelper
    {
        public enum SendOutcome
        {
            Sent,
            SmtpNotConfigured,
            Failed
        }

        public sealed class MailSendResult
        {
            public SendOutcome Outcome { get; set; }
            public string ErrorMessage { get; set; }
        }

        public static MailSendResult TrySendVerificationCodeAsyncSafe(string toEmail, string code)
        {
            string err = null;
            var o = TrySendVerificationCode(toEmail, code, out err);
            return new MailSendResult { Outcome = o, ErrorMessage = err };
        }

        public static SendOutcome TrySendVerificationCode(string toEmail, string code, out string errorMessage)
        {
            errorMessage = null;
            var host = ConfigurationManager.AppSettings["RegistrationSmtpHost"];
            if (string.IsNullOrWhiteSpace(host))
                return SendOutcome.SmtpNotConfigured;

            var from = ConfigurationManager.AppSettings["RegistrationMailFrom"];
            if (string.IsNullOrWhiteSpace(from))
                from = ConfigurationManager.AppSettings["RegistrationSmtpUser"];

            if (string.IsNullOrWhiteSpace(from))
            {
                errorMessage = "Укажите RegistrationMailFrom или RegistrationSmtpUser в App.config.";
                return SendOutcome.Failed;
            }

            int port = 587;
            int p;
            if (int.TryParse(ConfigurationManager.AppSettings["RegistrationSmtpPort"], out p))
                port = p;

            var user = ConfigurationManager.AppSettings["RegistrationSmtpUser"] ?? string.Empty;
            var password = ConfigurationManager.AppSettings["RegistrationSmtpPassword"] ?? string.Empty;
            bool s;
            bool ssl = !bool.TryParse(ConfigurationManager.AppSettings["RegistrationSmtpSsl"], out s) || s;

            int timeoutMs = 25000;
            int timeoutCfg;
            if (int.TryParse(ConfigurationManager.AppSettings["RegistrationSmtpTimeoutMs"], out timeoutCfg) &&
                timeoutCfg >= 5000 && timeoutCfg <= 120000)
                timeoutMs = timeoutCfg;

            try
            {
                using (var msg = new MailMessage())
                {
                    msg.From = new MailAddress(from);
                    msg.To.Add(toEmail);
                    msg.Subject = "DriveCare — код подтверждения";
                    msg.Body = string.Format(
                        "Ваш код подтверждения: {0}\r\n\r\nЕсли вы не регистрировались в DriveCare, проигнорируйте это письмо.",
                        code);
                    msg.IsBodyHtml = false;

                    using (var client = new SmtpClient(host.Trim(), port))
                    {
                        client.Timeout = timeoutMs;
                        client.EnableSsl = ssl;
                        if (!string.IsNullOrEmpty(user))
                            client.Credentials = new NetworkCredential(user, password);
                        else
                            client.UseDefaultCredentials = false;

                        client.Send(msg);
                    }
                }

                return SendOutcome.Sent;
            }
            catch (Exception ex)
            {
                errorMessage = FormatSmtpException(ex, host);
                return SendOutcome.Failed;
            }
        }

        private static string FormatSmtpException(Exception ex, string host)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
                msg = msg + " (" + ex.InnerException.Message + ")";
            if (host.IndexOf("stmp.", StringComparison.OrdinalIgnoreCase) >= 0)
                msg = msg + " Проверьте хост SMTP: часто пишут stmp вместо smtp.";
            return msg;
        }
    }
}
