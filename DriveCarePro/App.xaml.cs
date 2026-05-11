using DriveCarePro.Services;
using System.Windows;

namespace DriveCarePro
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeService.Initialize();
            base.OnStartup(e);
        }
    }
}
