using DriveCareCore.Data.BD;
using DriveCarePro;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminTableBrowserPage : Page
    {
        private static readonly string[] AllowedTables =
        {
            "Users", "Employees", "Companies", "Workshops", "Cars", "CarSales", "Parts",
            "Roles", "Notifications", "UserRoles", "EmployeeRolesMap", "UserCars",
            "Addresses", "Brands", "Models", "CarTypes", "Countries", "UserCarSales",
            "CarSalePrices", "RepairHistory", "Tasks", "CarColors", "Colors", "FuelTypes",
            "PartManufacturers", "WarehouseManagers", "RepairCategories", "Statuses",
            "UserNotifications"
        };

        public AdminTableBrowserPage()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                    return;
                TablePicker.ItemsSource = AllowedTables.OrderBy(s => s).ToList();
                if (TablePicker.Items.Count > 0)
                    TablePicker.SelectedIndex = 0;
            };
        }

        private void BackHome_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;
            var name = TablePicker.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(name))
            {
                StatusText.Text = "Выберите таблицу.";
                return;
            }

            if (!AllowedTables.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                StatusText.Text = "Таблица не разрешена.";
                return;
            }

            var safe = name.Trim();
            try
            {
                var dt = new DataTable();
                var db = AppConnect.model1;
                var conn = db.Database.Connection;
                var wasOpen = conn.State == ConnectionState.Open;
                if (!wasOpen)
                    conn.Open();
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"SELECT TOP 300 * FROM dbo.[{safe}]";
                        cmd.CommandTimeout = 60;
                        using (var reader = cmd.ExecuteReader())
                            dt.Load(reader);
                    }
                }
                finally
                {
                    if (!wasOpen)
                        conn.Close();
                }

                Grid.ItemsSource = dt.DefaultView;
                StatusText.Text = $"Загружено строк: {dt.Rows.Count} · {safe}";
            }
            catch (Exception ex)
            {
                Grid.ItemsSource = null;
                StatusText.Text = "Ошибка: " + ex.Message;
            }
        }
    }
}
