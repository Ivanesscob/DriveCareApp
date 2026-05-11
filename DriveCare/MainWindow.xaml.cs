using DriveCare.Pages.LoginPages;
using MahApps.Metro.Controls;
using System.Windows.Controls;

namespace DriveCare
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public Frame MainFrame { get; set; } = AppState.MainFrame;
        public MainWindow()
        {
            InitializeComponent();
            AppState.SetFrame<LoginPage>();
            DataContext = this;
        }
    }
}
