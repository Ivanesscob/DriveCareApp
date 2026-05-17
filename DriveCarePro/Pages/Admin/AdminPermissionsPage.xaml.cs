using DriveCareCore.Data.BD;
using DriveCarePro;
using DriveCarePro.Services;
using DriveCarePro.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DriveCarePro.Pages.Admin
{
    public partial class AdminPermissionsPage : Page
    {
        private readonly bool _embeddedInHub;
        private List<PermissionRowVm> _allRows = new List<PermissionRowVm>();

        public AdminPermissionsPage() : this(embeddedInHub: false, focusAddForm: false)
        {
        }

        public AdminPermissionsPage(bool embeddedInHub, bool focusAddForm = false)
        {
            _embeddedInHub = embeddedInHub;
            InitializeComponent();
            if (_embeddedInHub)
                BackHomeButton.Visibility = Visibility.Collapsed;

            Loaded += (_, __) =>
            {
                if (!AppState.IsCurrentEmployeeProAdmin)
                    return;
                LoadGroups();
                LoadGrid();
                if (focusAddForm)
                    CodeBox.Focus();
            };
        }

        private void BackHome_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadGroups();
            LoadGrid();
        }

        private void LoadGroups()
        {
            try
            {
                var db = AppConnect.model1;
                var groups = db.PermissionGroups.AsNoTracking()
                    .OrderBy(g => g.Name)
                    .ToList();
                GroupPicker.ItemsSource = groups;
                if (groups.Count > 0)
                    GroupPicker.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                StatusText.Text = "Не удалось загрузить группы: " + ex.Message;
            }
        }

        private void LoadGrid()
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;

            try
            {
                var db = AppConnect.model1;
                _allRows = db.Permissions.AsNoTracking()
                    .Select(p => new PermissionRowVm
                    {
                        RowId = p.RowId,
                        Code = p.Code,
                        Name = p.Name,
                        Description = p.Description,
                        GroupName = p.PermissionGroup != null ? p.PermissionGroup.Name : string.Empty,
                        PermissionGroupId = p.PermissionGroupId
                    })
                    .OrderBy(p => p.GroupName)
                    .ThenBy(p => p.Code)
                    .ToList();

                ApplySearch();
            }
            catch (Exception ex)
            {
                PermissionsGrid.ItemsSource = null;
                _allRows = new List<PermissionRowVm>();
                StatusText.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private void ApplySearch()
        {
            var q = (SearchBox?.Text ?? string.Empty).Trim();
            IEnumerable<PermissionRowVm> rows = _allRows;
            if (!string.IsNullOrEmpty(q))
            {
                var lower = q.ToLowerInvariant();
                rows = _allRows.Where(r =>
                    (r.Code ?? string.Empty).ToLowerInvariant().Contains(lower) ||
                    (r.Name ?? string.Empty).ToLowerInvariant().Contains(lower) ||
                    (r.GroupName ?? string.Empty).ToLowerInvariant().Contains(lower) ||
                    (r.Description ?? string.Empty).ToLowerInvariant().Contains(lower));
            }

            var list = rows.ToList();
            PermissionsGrid.ItemsSource = list;
            StatusText.Text = string.IsNullOrEmpty(q)
                ? $"Записей: {list.Count}"
                : $"Найдено: {list.Count} из {_allRows.Count}";
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

        private PermissionRowVm GetSelectedRow() => PermissionsGrid.SelectedItem as PermissionRowVm;

        private void PermissionsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep != PermissionsGrid)
            {
                if (dep is DataGridRow gridRow)
                {
                    gridRow.IsSelected = true;
                    PermissionsGrid.SelectedItem = gridRow.Item;
                    break;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }

        private void PermissionsContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            var hasRow = GetSelectedRow() != null;
            CtxOpenItem.IsEnabled = hasRow;
            CtxEditItem.IsEnabled = hasRow;
            CtxDeleteItem.IsEnabled = hasRow;
        }

        private void PermissionsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) =>
            EditPermission_Click(sender, e);

        private void EditPermission_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;

            var row = GetSelectedRow();
            if (row == null)
            {
                MessageBox.Show("Выберите разрешение в списке.", "Разрешения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var owner = Window.GetWindow(this);
            if (PermissionEditWindow.ShowEdit(owner, row.RowId) == true)
            {
                LoadGrid();
                StatusText.Text = "Разрешение обновлено.";
            }
        }

        private void DeletePermission_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;

            var row = GetSelectedRow();
            if (row == null)
            {
                MessageBox.Show("Выберите разрешение в списке.", "Разрешения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var label = string.IsNullOrWhiteSpace(row.Code) ? row.Name : row.Code;
            if (MessageBox.Show($"Удалить разрешение «{label}»?\nСвязи с ролями тоже будут удалены.",
                    "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                var db = AppConnect.model1;
                var error = PermissionAdminService.TryDelete(db, row.RowId);
                if (error != null)
                {
                    MessageBox.Show(error, "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                LoadGrid();
                StatusText.Text = "Разрешение удалено.";
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null)
                    root = root.InnerException;
                MessageBox.Show("Ошибка удаления: " + root.Message, "Удаление",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SavePermission_Click(object sender, RoutedEventArgs e)
        {
            if (!AppState.IsCurrentEmployeeProAdmin)
                return;

            var code = (CodeBox.Text ?? "").Trim();
            var name = (NameBox.Text ?? "").Trim();
            var description = (DescriptionBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
            {
                StatusText.Text = "Укажите код и название.";
                return;
            }

            var group = GroupPicker.SelectedItem as PermissionGroup;
            if (group == null)
            {
                StatusText.Text = "Выберите группу разрешений.";
                return;
            }

            try
            {
                var db = AppConnect.model1;
                var (ok, error, ownerGrants) = PermissionAdminService.TryCreate(
                    db, code, name, description, group.RowId);

                if (!ok)
                {
                    StatusText.Text = error ?? "Не удалось сохранить разрешение.";
                    return;
                }

                CodeBox.Clear();
                NameBox.Clear();
                DescriptionBox.Clear();
                LoadGrid();
                StatusText.Text = ownerGrants > 0
                    ? $"Разрешение добавлено. Автоматически выдано ролям владельца: {ownerGrants}."
                    : PermissionAdminService.IsServicePermissionGroup(group)
                        ? "Разрешение добавлено. Роли владельца в базе не найдены."
                        : "Разрешение добавлено.";
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null)
                    root = root.InnerException;
                StatusText.Text = "Ошибка сохранения: " + root.Message;
            }
        }

        private sealed class PermissionRowVm
        {
            public Guid RowId { get; set; }
            public string Code { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public string GroupName { get; set; }
            public Guid? PermissionGroupId { get; set; }
        }
    }
}
