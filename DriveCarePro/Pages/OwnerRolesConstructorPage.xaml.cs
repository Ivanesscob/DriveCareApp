using DriveCareCore.Data.BD;

using DriveCarePro.Services;

using DriveCarePro.Windows;

using System;

using System.Linq;

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;



namespace DriveCarePro.Pages

{

    public partial class OwnerRolesConstructorPage : Page

    {

        private readonly bool _systemRolesMode;

        private OwnerOrganizationScope _scope;



        public OwnerRolesConstructorPage() : this(systemRolesMode: false)

        {

        }



        public OwnerRolesConstructorPage(bool systemRolesMode)

        {

            _systemRolesMode = systemRolesMode;

            InitializeComponent();

            Loaded += OwnerRolesConstructorPage_Loaded;

        }



        private void OwnerRolesConstructorPage_Loaded(object sender, RoutedEventArgs e)

        {

            if (_systemRolesMode)

            {

                if (!AppState.IsCurrentEmployeeProAdmin)

                {

                    MessageBox.Show("Системные роли доступны только администратору.", "Доступ",

                        MessageBoxButton.OK, MessageBoxImage.Information);

                    AppState.Navigate(new ProHomePage());

                    return;

                }



                Title = "Системные роли";

                HintText.Text = "Глобальные роли платформы (без CompanyId и WorkshopId).";

                Refresh();

                return;

            }



            if (!AppState.CanManageOrganizationEmployees)

            {

                MessageBox.Show("Конструктор ролей доступен только владельцу.", "Доступ",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                AppState.Navigate(new ProHomePage());

                return;

            }



            if (!OwnerOrganizationScope.TryResolve(out _scope, out var scopeError))

            {

                RolesGrid.ItemsSource = null;

                HintText.Text = scopeError;

                return;

            }



            HintText.Text = $"Организация: {_scope.CompanyName}. Роли на всю компанию или на отдельный салон.";

            Refresh();

        }



        private void Back_Click(object sender, RoutedEventArgs e) =>

            AppState.Navigate(new ProHomePage());



        private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();



        private void CreateRole_Click(object sender, RoutedEventArgs e)

        {

            var owner = Window.GetWindow(this);

            if (RoleEditWindow.ShowCreate(owner, _systemRolesMode, _scope) == true)

                Refresh();

        }



        private void EditRole_Click(object sender, RoutedEventArgs e) => OpenSelectedRole();

        private void DeleteRole_Click(object sender, RoutedEventArgs e)
        {
            if (!(RolesGrid.SelectedItem is RoleRowVm row))
            {
                MessageBox.Show("Выберите роль в списке.", "Конструктор ролей",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var db = AppConnect.model1;
            var role = db.Roles.FirstOrDefault(r => r.RowId == row.RoleId);
            if (role == null)
                return;

            if (!_systemRolesMode && _scope != null && _scope.IsSystemGlobalRole(role))
            {
                MessageBox.Show("Глобальную системную роль владелец удалить не может.",
                    "Конструктор ролей", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Удалить роль «{row.Name}»?", "Удаление роли",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var err = RoleEditorService.TryDelete(row.RoleId, _systemRolesMode, _scope);
            if (err != null)
            {
                MessageBox.Show(err, "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Refresh();
        }

        private void RolesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep != RolesGrid)
            {
                if (dep is DataGridRow gridRow)
                {
                    gridRow.IsSelected = true;
                    RolesGrid.SelectedItem = gridRow.Item;
                    break;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }

        private void RolesContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var hasRow = RolesGrid.SelectedItem is RoleRowVm;
            CtxEditItem.IsEnabled = hasRow && CanModifySelectedRole();
            CtxDeleteItem.IsEnabled = hasRow && CanModifySelectedRole();
        }

        private bool CanModifySelectedRole()
        {
            if (!(RolesGrid.SelectedItem is RoleRowVm row))
                return false;
            var role = AppConnect.model1.Roles.FirstOrDefault(r => r.RowId == row.RoleId);
            if (role == null)
                return false;
            if (!_systemRolesMode && _scope != null && _scope.IsSystemGlobalRole(role))
                return false;
            return RoleEditorService.CanEdit(role, _systemRolesMode, _scope);
        }

        private void RolesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
            OpenSelectedRole();



        private void OpenSelectedRole()

        {

            if (!(RolesGrid.SelectedItem is RoleRowVm row))

            {

                MessageBox.Show("Выберите роль в списке.", "Конструктор ролей",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            var db = AppConnect.model1;

            var role = db.Roles.FirstOrDefault(r => r.RowId == row.RoleId);

            if (role == null)

                return;



            if (!_systemRolesMode && _scope != null && _scope.IsSystemGlobalRole(role))

            {

                MessageBox.Show(

                    "Глобальную системную роль владелец изменить не может.",

                    "Конструктор ролей", MessageBoxButton.OK, MessageBoxImage.Information);

                return;

            }



            var owner = Window.GetWindow(this);

            if (RoleEditWindow.ShowEdit(owner, _systemRolesMode, _scope, row.RoleId) == true)

                Refresh();

        }



        private void Refresh()

        {

            try

            {

                var db = AppConnect.model1;

                if (_systemRolesMode)

                {

                    var roles = db.Roles

                        .Where(r => !r.WorkshopId.HasValue && !r.CompanyId.HasValue)

                        .OrderBy(r => r.Name)

                        .ToList();

                    BindRoles(roles, null);

                    return;

                }



                if (_scope == null)

                    return;



                var rolesOrg = _scope.RolesForOrganization(db).OrderBy(r => r.Name).ToList();

                BindRoles(rolesOrg, _scope);

            }

            catch (Exception ex)

            {

                RolesGrid.ItemsSource = null;

                HintText.Text = "Ошибка загрузки: " + ex.Message;

            }

        }



        private void BindRoles(System.Collections.Generic.List<Role> roles, OwnerOrganizationScope scope)

        {

            var db = AppConnect.model1;

            var roleIds = roles.Select(r => r.RowId).ToList();



            var empUsage = db.EmployeeRolesMaps

                .Where(m => roleIds.Contains(m.RoleId))

                .GroupBy(m => m.RoleId)

                .ToDictionary(g => g.Key, g => g.Count());



            var permUsage = db.RolePermissionsMaps

                .Where(m => roleIds.Contains(m.RoleId))

                .GroupBy(m => m.RoleId)

                .ToDictionary(g => g.Key, g => g.Count());



            System.Collections.Generic.Dictionary<Guid, string> workshopNames = null;

            if (scope != null)

            {

                workshopNames = db.Workshops

                    .Where(w => scope.WorkshopIds.Contains(w.RowId))

                    .AsEnumerable()

                    .ToDictionary(w => w.RowId, w => (w.Name ?? string.Empty).Trim());

            }



            var list = roles.Select(r =>

            {

                var pc = permUsage.TryGetValue(r.RowId, out var pCount) ? pCount : 0;

                var ec = empUsage.TryGetValue(r.RowId, out var eCount) ? eCount : 0;

                var name = string.IsNullOrWhiteSpace(r.Name) ? "—" : r.Name.Trim();



                if (scope != null)

                {

                    if (scope.IsCompanyWideRole(r))

                        name += " (вся компания)";

                    else if (r.WorkshopId.HasValue && workshopNames != null &&

                             workshopNames.TryGetValue(r.WorkshopId.Value, out var wn))

                        name += " (" + (string.IsNullOrWhiteSpace(wn) ? "салон" : wn) + ")";

                }



                return new RoleRowVm

                {

                    RoleId = r.RowId,

                    Name = name,

                    ActiveDisplay = r.IsActive == false ? "Нет" : "Да",

                    PermissionsCount = pc.ToString(),

                    EmployeesCount = ec.ToString()

                };

            }).ToList();



            RolesGrid.ItemsSource = list;

        }



        private sealed class RoleRowVm

        {

            public Guid RoleId { get; set; }

            public string Name { get; set; }

            public string ActiveDisplay { get; set; }

            public string PermissionsCount { get; set; }

            public string EmployeesCount { get; set; }

        }

    }

}

