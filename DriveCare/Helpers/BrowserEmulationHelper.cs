using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Controls;

namespace DriveCare.Helpers
{
    /// <summary>
    /// WebBrowser (IE) — включаем современный движок для Яндекс.Карт.
    /// </summary>
    internal static class BrowserEmulationHelper
    {
        const string FeatureControl = @"Software\Microsoft\Internet Explorer\Main\FeatureControl\FEATURE_BROWSER_EMULATION";

        public static void EnsureLatestEmulationForCurrentProcess()
        {
            try
            {
                var exeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "DriveCare.exe");
                using (var key = Registry.CurrentUser.CreateSubKey(FeatureControl))
                {
                    if (key == null)
                        return;
                    // 11001 = IE11 edge mode
                    key.SetValue(exeName, 11001, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        /// <summary>WPF WebBrowser: скрыть диалоги ошибок JavaScript (как ScriptErrorsSuppressed в WinForms).</summary>
        public static void SuppressScriptErrors(WebBrowser browser, bool suppress = true)
        {
            if (browser == null)
                return;

            browser.Navigated += (_, __) =>
            {
                try
                {
                    var field = typeof(WebBrowser).GetField("_axIWebBrowser2", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field == null)
                        return;

                    var ax = field.GetValue(browser);
                    if (ax == null)
                        return;

                    ax.GetType().InvokeMember(
                        "Silent",
                        BindingFlags.SetProperty,
                        null,
                        ax,
                        new object[] { suppress });
                }
                catch
                {
                }
            };
        }
    }
}
