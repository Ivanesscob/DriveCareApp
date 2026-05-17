using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopRepairCarsPage : Page
    {
        public WorkshopRepairCarsPage()
        {
            InitializeComponent();
            Loaded += (_, __) => Refresh();
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

        private void Refresh()
        {
            try
            {
                var db = AppConnect.model1;
                var statusNames = db.Statuses.ToDictionary(s => s.RowId, s => (s.Name ?? string.Empty).Trim());

                var rows = (from rh in db.RepairHistories
                            where rh.EndDate == null
                            join c in db.Cars on rh.CarId equals c.RowId
                            join m in db.Models on c.ModelId equals m.RowId
                            join b in db.Brands on m.BrandId equals b.RowId
                            select new { rh, c, b, m }).ToList();

                var list = new List<RepairCarRowVm>();
                foreach (var x in rows.OrderByDescending(r => r.rh.RepairDate).Take(200))
                {
                    var empName = "—";
                    if (x.rh.EmployeeId.HasValue)
                    {
                        var emp = db.Employees.FirstOrDefault(e => e.RowId == x.rh.EmployeeId.Value);
                        if (emp != null)
                            empName = AppState.FormatEmployeeDisplayName(emp);
                    }

                    var year = x.c.Year.HasValue ? $" {x.c.Year}" : string.Empty;
                    var status = x.rh.StatusId.HasValue && statusNames.TryGetValue(x.rh.StatusId.Value, out var sn)
                        ? sn : "—";

                    list.Add(new RepairCarRowVm
                    {
                        CarDisplay = $"{(x.b.Name ?? "").Trim()} {(x.m.Name ?? "").Trim()}{year}".Trim(),
                        Title = string.IsNullOrWhiteSpace(x.rh.Title) ? "—" : x.rh.Title.Trim(),
                        StatusDisplay = status,
                        RepairDateDisplay = x.rh.RepairDate.ToString("dd.MM.yyyy"),
                        EmployeeDisplay = empName
                    });
                }

                Grid.ItemsSource = list;
                HintText.Text = list.Count == 0
                    ? "Сейчас нет записей ремонта без даты окончания (авто не в активном ремонте по данным БД)."
                    : $"В ремонте (без даты окончания): {list.Count}. Показаны последние по дате начала.";
            }
            catch (Exception ex)
            {
                Grid.ItemsSource = null;
                HintText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private sealed class RepairCarRowVm
        {
            public string CarDisplay { get; set; }
            public string Title { get; set; }
            public string StatusDisplay { get; set; }
            public string RepairDateDisplay { get; set; }
            public string EmployeeDisplay { get; set; }
        }
    }
}
