using DriveCareCore.Data.BD;
using DriveCarePro;
using System.Linq;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminDashboardPage : Page
    {
        public AdminDashboardPage()
        {
            InitializeComponent();
            Loaded += (_, __) => LoadCounts();
        }

        private void LoadCounts()
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;
            try
            {
                var db = AppConnect.model1;
                CntUsers.Text = db.Users.Count().ToString("N0");
                CntEmployees.Text = db.Employees.Count().ToString("N0");
                CntCarSales.Text = db.CarSales.Count().ToString("N0");
                CntParts.Text = db.Parts.Count().ToString("N0");
                CntCompanies.Text = db.Companies.Count().ToString("N0");
                CntCars.Text = db.Cars.Count().ToString("N0");
            }
            catch
            {
                CntUsers.Text = CntEmployees.Text = CntCarSales.Text = CntParts.Text = CntCompanies.Text = CntCars.Text = "—";
            }
        }
    }
}
