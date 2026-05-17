using MahApps.Metro.Controls;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro
{
    public partial class MainWindow : MetroWindow
    {
        public Frame MainFrame { get; set; } = AppState.MainFrame;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            DataContext = this;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;
            if (await AppState.TryRestoreSessionAsync().ConfigureAwait(true))
                AppState.SetFrame<Pages.ProHomePage>();
            else
                AppState.SetFrame<Pages.LoginPages.LoginPage>();
        }
    }
}
