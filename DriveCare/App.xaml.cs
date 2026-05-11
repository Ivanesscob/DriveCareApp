using DriveCare.Services;
using System.Windows;

namespace DriveCare
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ThemeService.Initialize();
            base.OnStartup(e);
        }
    }
}
