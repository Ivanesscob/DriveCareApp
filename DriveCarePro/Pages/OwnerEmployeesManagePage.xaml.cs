using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class OwnerEmployeesManagePage : Page
    {
        private Guid? _companyId;
        private List<Guid> _allowedWorkshopIds = new List<Guid>();
        private Guid? _selectedEmployeeId;

        public OwnerEmployeesManagePage()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                if (!AppState.CanManageOrganizationEmployees)
                {
                    MessageBox.Show("Управление сотрудниками доступно только владельцу.", "Доступ",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    AppState.Navigate(new ProHomePage());
                    return;
                }
                if (!TryResolveOwnerScope())
                    return;
                Refresh();
            };
        }

        private bool TryResolveOwnerScope()
        {
            var owner = AppState.CurrentEmployee;
            if (owner == null || !owner.WorkshopId.HasValue)
            {
                HintText.Text = "У вас не указана мастерская. Назначьте WorkshopId владельцу в БД.";
                Grid.ItemsSource = null;
                return false;
            }

            var db = AppConnect.model1;
            var ownerWorkshop = db.Workshops.FirstOrDefault(w => w.RowId == owner.WorkshopId.Value);
            if (ownerWorkshop == null)
            {
                HintText.Text = "Мастерская владельца не найдена в базе.";
                return false;
            }

            _companyId = ownerWorkshop.CompanyId;
            _allowedWorkshopIds = db.Workshops
                .Where(w => w.CompanyId == _companyId)
                .Select(w => w.RowId)
                .ToList();

            var workshops = db.Workshops
                .Where(w => w.CompanyId == _companyId)
                .OrderBy(w => w.Name)
                .Select(w => new { w.RowId, w.Name })
                .ToList()
                .Select(w => new WorkshopItem
                {
                    RowId = w.RowId,
                    Name = string.IsNullOrWhiteSpace(w.Name) ? "—" : w.Name.Trim()
                })
                .ToList();
            WorkshopCombo.ItemsSource = workshops;

            var companyName = db.Companies.Where(c => c.RowId == _companyId).Select(c => c.Name).FirstOrDefault() ?? "—";
            HintText.Text = $"Организация: {companyName.Trim()}. Сотрудники мастерских вашей компании.";
            return true;
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

        private void Refresh()
        {
            if (_allowedWorkshopIds.Count == 0)
                return;

            try
            {
                var db = AppConnect.model1;
                var workshopNames = db.Workshops
                    .Where(w => _allowedWorkshopIds.Contains(w.RowId))
                    .ToDictionary(w => w.RowId, w => (w.Name ?? string.Empty).Trim());
                var roleMaps = db.EmployeeRolesMaps.ToList();
                var roles = db.Roles.ToDictionary(r => r.RowId, r => (r.Name ?? string.Empty).Trim());

                var employees = db.Employees
                    .Where(e => e.WorkshopId.HasValue && _allowedWorkshopIds.Contains(e.WorkshopId.Value))
                    .OrderBy(e => e.LastName).ThenBy(e => e.FirstName)
                    .ToList();

                var list = employees.Select(e =>
                {
                    var roleIds = roleMaps.Where(m => m.EmployeeId == e.RowId).Select(m => m.RoleId).ToList();
                    var roleText = roleIds.Count == 0
                        ? "—"
                        : string.Join(", ", roleIds.Select(id => roles.TryGetValue(id, out var n) ? n : "—").Where(s => s != "—"));
                    var ws = e.WorkshopId.HasValue && workshopNames.TryGetValue(e.WorkshopId.Value, out var wn) ? wn : "—";

                    return new EmployeeRowVm
                    {
                        EmployeeId = e.RowId,
                        FullName = AppState.FormatEmployeeDisplayName(e),
                        Login = string.IsNullOrWhiteSpace(e.Login) ? "—" : e.Login.Trim(),
                        WorkshopName = string.IsNullOrWhiteSpace(ws) ? "—" : ws,
                        RolesDisplay = string.IsNullOrWhiteSpace(roleText) ? "—" : roleText,
                        ActiveDisplay = e.IsActive == false ? "Нет" : "Да"
                    };
                }).ToList();

                Grid.ItemsSource = list;
                if (list.Count == 0)
                    HintText.Text += " Сотрудников не найдено.";
            }
            catch (Exception ex)
            {
                Grid.ItemsSource = null;
                HintText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private void Grid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(Grid.SelectedItem is EmployeeRowVm row))
            {
                EditPanel.Visibility = Visibility.Collapsed;
                _selectedEmployeeId = null;
                return;
            }

            _selectedEmployeeId = row.EmployeeId;
            var db = AppConnect.model1;
            var emp = db.Employees.FirstOrDefault(x => x.RowId == row.EmployeeId);
            if (emp == null)
            {
                EditPanel.Visibility = Visibility.Collapsed;
                return;
            }

            LastNameBox.Text = emp.LastName ?? string.Empty;
            FirstNameBox.Text = emp.FirstName ?? string.Empty;
            MidNameBox.Text = emp.MidName ?? string.Empty;
            LoginBox.Text = emp.Login ?? string.Empty;
            EmailBox.Text = emp.Email ?? string.Empty;
            PhoneBox.Text = emp.Phone ?? string.Empty;
            IsActiveCheck.IsChecked = emp.IsActive != false;
            WorkshopCombo.SelectedValue = emp.WorkshopId;

            EditPanel.Visibility = Visibility.Visible;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedEmployeeId.HasValue)
                return;

            var db = AppConnect.model1;
            var emp = db.Employees.FirstOrDefault(x => x.RowId == _selectedEmployeeId.Value);
            if (emp == null)
                return;

            if (!(WorkshopCombo.SelectedValue is Guid wsId) || !_allowedWorkshopIds.Contains(wsId))
            {
                MessageBox.Show("Выберите мастерскую вашей организации.", "Сохранение",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            emp.LastName = (LastNameBox.Text ?? string.Empty).Trim();
            emp.FirstName = (FirstNameBox.Text ?? string.Empty).Trim();
            emp.MidName = (MidNameBox.Text ?? string.Empty).Trim();
            emp.Login = (LoginBox.Text ?? string.Empty).Trim();
            emp.Email = (EmailBox.Text ?? string.Empty).Trim();
            emp.Phone = (PhoneBox.Text ?? string.Empty).Trim();
            emp.WorkshopId = wsId;
            emp.IsActive = IsActiveCheck.IsChecked == true;

            try
            {
                db.SaveChanges();
                MessageBox.Show("Данные сотрудника сохранены.", "Управление сотрудниками",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private sealed class WorkshopItem
        {
            public Guid RowId { get; set; }
            public string Name { get; set; }
        }

        private sealed class EmployeeRowVm
        {
            public Guid EmployeeId { get; set; }
            public string FullName { get; set; }
            public string Login { get; set; }
            public string WorkshopName { get; set; }
            public string RolesDisplay { get; set; }
            public string ActiveDisplay { get; set; }
        }
    }
}
