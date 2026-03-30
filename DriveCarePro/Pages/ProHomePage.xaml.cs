using System.Windows;
using System.Windows.Controls;
using DriveCarePro.Pages.LoginPages;

namespace DriveCarePro.Pages
{
    public partial class ProHomePage : Page
    {
        public ProHomePage()
        {
            InitializeComponent();
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            AppState.CurrentUserId = System.Guid.Empty;
            AppState.CurrentUser = null;
            AppState.CurrentEmployee = null;
            AppState.UserRoles = null;
            AppState.SetFrame<LoginPage>();
        }
    }
}
