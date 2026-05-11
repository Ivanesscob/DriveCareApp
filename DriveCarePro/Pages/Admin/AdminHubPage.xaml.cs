using DriveCarePro;
using DriveCarePro.Pages;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminHubPage : Page
    {
        public AdminHubPage()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                {
                    AppState.SetFrame<ProHomePage>();
                    return;
                }
                if (AdminContent.Content == null)
                    AdminContent.Navigate(new AdminDashboardPage());
            };
        }

        private void Home_Click(object sender, RoutedEventArgs e)
        {
            AppState.SetFrame<ProHomePage>();
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e) =>
            AdminContent.Navigate(new AdminDashboardPage());

        private void NavTables_Click(object sender, RoutedEventArgs e) =>
            AdminContent.Navigate(new AdminTableBrowserPage());

        private void NavOrgs_Click(object sender, RoutedEventArgs e) =>
            AdminContent.Navigate(new AdminOrganizationsPage());

        private void NavNotify_Click(object sender, RoutedEventArgs e) =>
            AdminContent.Navigate(new AdminBroadcastPage());
    }
}
