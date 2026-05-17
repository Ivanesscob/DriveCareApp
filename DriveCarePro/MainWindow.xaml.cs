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
            if (!AppState.TryRestoreSession())
                AppState.SetFrame<Pages.LoginPages.LoginPage>();
            else
                AppState.SetFrame<Pages.ProHomePage>();
            DataContext = this;
        }
    }
}
