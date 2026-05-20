using DriveCareCore.Data.BD;
using DriveCarePro;
using DriveCarePro.Services;
using System;
using System.Linq;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class PermissionEditWindow : Window
    {
        private readonly Guid _permissionId;

        private PermissionEditWindow(Guid permissionId)
        {
            _permissionId = permissionId;
            InitializeComponent();
            Loaded += (_, __) => LoadData();
        }

        public static bool? ShowEdit(Window owner, Guid permissionId)
        {
            var dlg = new PermissionEditWindow(permissionId) { Owner = owner };
            return dlg.ShowDialog();
        }

        private void LoadData()
        {
            try
            {
                var db = AppConnect.model1;
                var permission = db.Permissions.AsNoTracking()
                    .FirstOrDefault(p => p.RowId == _permissionId);
                if (permission == null)
                {
                    MessageBox.Show("Разрешение не найдено.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    Close();
                    return;
                }

                var groups = db.PermissionGroups.AsNoTracking().OrderBy(g => g.Name).ToList();
                GroupPicker.ItemsSource = groups;
                if (permission.PermissionGroupId.HasValue)
                    GroupPicker.SelectedItem = groups.FirstOrDefault(g => g.RowId == permission.PermissionGroupId.Value);
                if (GroupPicker.SelectedItem == null && GroupPicker.Items.Count > 0)
                    GroupPicker.SelectedIndex = 0;

                CodeBox.Text = permission.Code ?? string.Empty;
                NameBox.Text = permission.Name ?? string.Empty;
                DescriptionBox.Text = permission.Description ?? string.Empty;
                HeaderText.Text = "Изменить: " + (permission.Code ?? "—");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var code = (CodeBox.Text ?? string.Empty).Trim();
            var name = (NameBox.Text ?? string.Empty).Trim();
            var description = (DescriptionBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Укажите код и название.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var group = GroupPicker.SelectedItem as PermissionGroup;
            if (group == null)
            {
                MessageBox.Show("Выберите группу.", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var db = AppConnect.model1;
                var error = PermissionAdminService.TryUpdate(
                    db, _permissionId, code, name, description, group.RowId);
                if (error != null)
                {
                    MessageBox.Show(error, Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null)
                    root = root.InnerException;
                MessageBox.Show("Ошибка сохранения: " + root.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
