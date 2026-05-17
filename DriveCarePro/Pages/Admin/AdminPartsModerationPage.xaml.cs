using DriveCareCore.Data.BD;
using DriveCarePro;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminPartsModerationPage : Page
    {
        public AdminPartsModerationPage()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                {
                    ProNavigation.GoHome();
                    return;
                }
                LoadGrid();
            };
        }

        private void BackHome_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private void Refresh_Click(object sender, RoutedEventArgs e) => LoadGrid();

        private void LoadGrid()
        {
            try
            {
                var db = AppConnect.model1;
                var rows = db.Parts
                    .OrderByDescending(p => p.Name)
                    .Take(200)
                    .Select(p => new
                    {
                        p.RowId,
                        p.Name,
                        p.Article,
                        Статус = p.Status != null ? p.Status.Name : "",
                        p.Description
                    })
                    .ToList();
                PartsGrid.ItemsSource = rows;
            }
            catch
            {
                PartsGrid.ItemsSource = null;
            }
        }
    }
}
