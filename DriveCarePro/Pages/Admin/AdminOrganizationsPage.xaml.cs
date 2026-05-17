using DriveCareCore.Data.BD;
using DriveCarePro;
using DriveCarePro.Windows;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminOrganizationsPage : Page
    {
        public AdminOrganizationsPage()
        {
            InitializeComponent();
            Loaded += (_, __) => LoadGrids();
        }

        private void BackHome_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadGrids();

        private void CreateCompany_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;

            var owner = Window.GetWindow(this);
            var dlg = new CreateCompanyWindow();
            if (owner != null)
                dlg.Owner = owner;
            if (dlg.ShowDialog() == true)
                LoadGrids();
        }

        private void LoadGrids()
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;
            try
            {
                var db = AppConnect.model1;
                var companies = db.Companies
                    .Select(c => new
                    {
                        c.RowId,
                        c.Name,
                        c.Description,
                        Цехов = c.Workshops.Count()
                    })
                    .OrderBy(c => c.Name)
                    .ToList();
                CompaniesGrid.ItemsSource = companies;

                var workshops = db.Workshops
                    .Select(w => new
                    {
                        w.RowId,
                        w.Name,
                        w.CompanyId,
                        Компания = w.Company != null ? w.Company.Name : "",
                        w.Description
                    })
                    .OrderBy(w => w.Компания)
                    .ThenBy(w => w.Name)
                    .ToList();
                WorkshopsGrid.ItemsSource = workshops;
            }
            catch
            {
                CompaniesGrid.ItemsSource = null;
                WorkshopsGrid.ItemsSource = null;
            }
        }
    }
}
