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
            AppState.SetFrame<Pages.LoginPages.LoginPage>();
            DataContext = this;
        }
    }
}
