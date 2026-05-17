using DriveCareCore.Dialogs;

using DriveCarePro.Services;

using System;

using System.Collections.Generic;

using System.Linq;

using System.Windows;

using System.Windows.Controls;

using System.ComponentModel;

using System.Windows.Data;

using System.Windows.Input;

using System.Windows.Media;



namespace DriveCarePro.Windows

{

    public partial class RoleEditWindow : Window

    {

        private readonly bool _systemRolesMode;

        private readonly OwnerOrganizationScope _scope;

        private readonly RoleEditorService.RoleEditModel _model;

        private readonly bool _readOnly;

        private List<RoleEditorService.PermissionItem> _allPermissions = new List<RoleEditorService.PermissionItem>();



        private RoleEditWindow(bool systemRolesMode, OwnerOrganizationScope scope, RoleEditorService.RoleEditModel model, bool readOnly)

        {

            _systemRolesMode = systemRolesMode;

            _scope = scope;

            _model = model ?? new RoleEditorService.RoleEditModel();

            _readOnly = readOnly;

            InitializeComponent();

            Loaded += RoleEditWindow_Loaded;

        }



        public static bool? ShowCreate(Window owner, bool systemRolesMode, OwnerOrganizationScope scope)

        {

            var dlg = new RoleEditWindow(systemRolesMode, scope, new RoleEditorService.RoleEditModel(), readOnly: false)

            {

                Owner = owner

            };

            return dlg.ShowDialog();

        }



        public static bool? ShowEdit(Window owner, bool systemRolesMode, OwnerOrganizationScope scope, Guid roleId, bool readOnly = false)

        {

            var model = RoleEditorService.LoadRole(roleId, systemRolesMode, scope);

            if (model == null)

            {

                AppMessageBox.Show("Роль не найдена или недоступна для редактирования.", "Роль",

                    MessageBoxButton.OK, MessageBoxImage.Information);

                return false;

            }



            var dlg = new RoleEditWindow(systemRolesMode, scope, model, readOnly)

            {

                Owner = owner

            };

            return dlg.ShowDialog();

        }



        private void RoleEditWindow_Loaded(object sender, RoutedEventArgs e)

        {

            _allPermissions = RoleEditorService.LoadPermissions(_systemRolesMode);
            foreach (var p in _allPermissions)
            {
                if (string.IsNullOrWhiteSpace(p.GroupName))
                    p.GroupName = "Прочие";
            }

            var isNew = !_model.RoleId.HasValue;

            HeaderText.Text = isNew

                ? (_systemRolesMode ? "Новая системная роль" : "Новая роль")

                : (_systemRolesMode ? "Редактирование системной роли" : "Редактирование роли");



            SubHeaderText.Text = _systemRolesMode

                ? "Глобальная роль платформы (без привязки к компании и салону)."

                : _scope != null

                    ? $"Компания «{_scope.CompanyName}»: роль на всю организацию или на один салон. Разрешения админ-панели недоступны."

                    : "Роль организации.";



            NameBox.Text = _model.Name ?? string.Empty;

            DescriptionBox.Text = _model.Description ?? string.Empty;

            IsActiveCheck.IsChecked = _model.IsActive;



            if (_systemRolesMode)

                ScopePanel.Visibility = Visibility.Collapsed;

            else

            {

                ScopePanel.Visibility = Visibility.Visible;

                WorkshopCombo.ItemsSource = RoleEditorService.LoadWorkshopsForOwner(_scope);



                if (_model.ScopeKind == RoleScopeKind.Company)

                    ScopeCompanyRadio.IsChecked = true;

                else

                    ScopeWorkshopRadio.IsChecked = true;



                if (_model.WorkshopId.HasValue)

                    WorkshopCombo.SelectedValue = _model.WorkshopId.Value;

                else if (AppState.CurrentEmployee?.WorkshopId is Guid ws)

                    WorkshopCombo.SelectedValue = ws;



                UpdateScopeUi();

            }



            DeleteButton.Visibility = isNew || _readOnly ? Visibility.Collapsed : Visibility.Visible;



            if (_readOnly)

            {

                NameBox.IsReadOnly = true;

                DescriptionBox.IsReadOnly = true;

                IsActiveCheck.IsEnabled = false;

                ScopeCompanyRadio.IsEnabled = false;

                ScopeWorkshopRadio.IsEnabled = false;

                WorkshopCombo.IsEnabled = false;

                PermissionSearchBox.IsReadOnly = true;

                PermissionPickerList.IsEnabled = false;

                SaveButton.IsEnabled = false;

                DeleteButton.Visibility = Visibility.Collapsed;

            }



            ApplyPermissionSearch();

        }



        private void ScopeRadio_Changed(object sender, RoutedEventArgs e) => UpdateScopeUi();



        private void UpdateScopeUi()

        {

            var workshopScope = ScopeWorkshopRadio.IsChecked == true;

            WorkshopCombo.IsEnabled = !_readOnly && workshopScope;

            WorkshopCombo.Opacity = workshopScope ? 1.0 : 0.45;

        }



        private void PermissionSearchBox_TextChanged(object sender, TextChangedEventArgs e) =>

            ApplyPermissionSearch();



        private void ApplyPermissionSearch()
        {
            var q = (PermissionSearchBox.Text ?? string.Empty).Trim();
            var available = _allPermissions
                .Where(p => !_model.PermissionIds.Contains(p.PermissionId))
                .ToList();

            IEnumerable<RoleEditorService.PermissionItem> filtered = available;
            if (q.Length > 0)
            {
                filtered = available.Where(p => PermissionMatchesQuery(p, q));
            }

            var list = filtered
                .OrderBy(p => p.GroupName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
                .Take(q.Length > 0 ? 120 : 500)
                .ToList();

            if (q.Length == 0)
            {
                var view = new ListCollectionView(list);
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RoleEditorService.PermissionItem.GroupName)));
                PermissionPickerList.ItemsSource = view;
            }
            else
            {
                PermissionPickerList.ItemsSource = list;
            }

            var shown = list.Count;
            var totalAvailable = available.Count;
            if (q.Length > 0)
            {
                PermissionSearchStatus.Text = shown == 0
                    ? "Ничего не найдено — измените запрос."
                    : $"Найдено: {shown}" + (shown < totalAvailable ? $" (из {totalAvailable} доступных)" : string.Empty);
                PermissionSearchHint.Visibility = Visibility.Collapsed;
            }
            else
            {
                PermissionSearchStatus.Text = totalAvailable > 0
                    ? $"Доступно для добавления: {totalAvailable}"
                    : "Все разрешения уже назначены.";
                PermissionSearchHint.Visibility = Visibility.Visible;
            }

            RebuildPermissionChips(q);
        }

        private static bool PermissionMatchesQuery(RoleEditorService.PermissionItem p, string q)
        {
            return PermissionTextContains(p.Name, q)
                || PermissionTextContains(p.Code, q)
                || PermissionTextContains(p.Description, q)
                || PermissionTextContains(p.GroupName, q)
                || PermissionTextContains(p.GroupCode, q)
                || PermissionTextContains(p.Title, q)
                || PermissionTextContains(p.Subtitle, q);
        }

        private static bool PermissionTextContains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }



        private void PermissionPickerList_MouseDoubleClick(object sender, MouseButtonEventArgs e)

        {

            if (_readOnly)

                return;

            if (PermissionPickerList.SelectedItem is RoleEditorService.PermissionItem item)

                AddPermission(item.PermissionId);

        }



        private void AddPermission(Guid permissionId)

        {

            if (_readOnly || !_model.PermissionIds.Add(permissionId))

                return;

            ApplyPermissionSearch();

        }



        private void RemovePermission(Guid permissionId)

        {

            if (_readOnly)

                return;

            _model.PermissionIds.Remove(permissionId);

            ApplyPermissionSearch();

        }



        private void RebuildPermissionChips(string searchQuery = null)

        {

            PermissionChipsPanel.Children.Clear();

            var lookup = _allPermissions.ToDictionary(p => p.PermissionId);

            var assigned = _model.PermissionIds
                .Where(id => lookup.ContainsKey(id))
                .Select(id => lookup[id])
                .ToList();

            var q = (searchQuery ?? string.Empty).Trim();
            if (q.Length > 0)
                assigned = assigned.Where(p => PermissionMatchesQuery(p, q)).ToList();

            if (_model.PermissionIds.Count == 0)

            {

                PermissionChipsPanel.Children.Add(new TextBlock

                {

                    Text = "Разрешений пока нет — найдите и добавьте ниже.",

                    FontSize = 12,

                    Foreground = (Brush)FindResource("App.Brush.Muted"),

                    Margin = new Thickness(2, 6, 2, 6)

                });

                return;

            }

            if (assigned.Count == 0 && q.Length > 0)
            {
                PermissionChipsPanel.Children.Add(new TextBlock
                {
                    Text = "Среди назначенных нет совпадений с поиском.",
                    FontSize = 12,
                    Foreground = (Brush)FindResource("App.Brush.Muted"),
                    Margin = new Thickness(2, 6, 2, 6)
                });
                return;
            }

            foreach (var p in assigned.OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase))
            {
                var id = p.PermissionId;

                {
                    var chip = new Border

                    {

                        CornerRadius = new CornerRadius(8),

                        Background = (Brush)FindResource("App.Brush.Accent"),

                        Padding = new Thickness(10, 6, 6, 6),

                        Margin = new Thickness(0, 0, 8, 8)

                    };



                    var panel = new StackPanel { Orientation = Orientation.Horizontal };

                    panel.Children.Add(new TextBlock

                    {

                        Text = p.Title,

                        Foreground = (Brush)FindResource("App.Brush.TextOnAccent"),

                        FontSize = 12,

                        VerticalAlignment = VerticalAlignment.Center,

                        Margin = new Thickness(0, 0, 6, 0)

                    });



                    if (!_readOnly)

                    {

                        var removeBtn = new Button

                        {

                            Content = "×",

                            Width = 22,

                            Height = 22,

                            Padding = new Thickness(0),

                            FontSize = 14,

                            FontWeight = FontWeights.Bold,

                            Background = Brushes.Transparent,

                            BorderThickness = new Thickness(0),

                            Foreground = (Brush)FindResource("App.Brush.TextOnAccent"),

                            Cursor = Cursors.Hand,

                            Tag = id

                        };

                        removeBtn.Click += (_, __) =>

                        {

                            if (removeBtn.Tag is Guid g)

                                RemovePermission(g);

                        };

                        panel.Children.Add(removeBtn);

                    }



                    chip.Child = panel;

                    PermissionChipsPanel.Children.Add(chip);

                }

            }
        }



        private void Save_Click(object sender, RoutedEventArgs e)

        {

            _model.Name = NameBox.Text;

            _model.Description = DescriptionBox.Text;

            _model.IsActive = IsActiveCheck.IsChecked == true;



            if (!_systemRolesMode)

            {

                if (ScopeCompanyRadio.IsChecked == true)

                {

                    _model.ScopeKind = RoleScopeKind.Company;

                    _model.CompanyId = _scope?.CompanyId;

                    _model.WorkshopId = null;

                }

                else

                {

                    _model.ScopeKind = RoleScopeKind.Workshop;

                    _model.CompanyId = null;

                    if (WorkshopCombo.SelectedValue is Guid ws)

                        _model.WorkshopId = ws;

                    else if (WorkshopCombo.SelectedItem is RoleEditorService.WorkshopItem item)

                        _model.WorkshopId = item.RowId;

                    else

                        _model.WorkshopId = null;

                }

            }

            else

            {

                _model.WorkshopId = null;

                _model.CompanyId = null;

            }



            var result = RoleEditorService.Save(_model, _systemRolesMode, _scope);

            if (!result.Success)

            {

                AppMessageBox.Show(result.ErrorMessage, "Сохранение", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;

            }



            DialogResult = true;

            Close();

        }



        private void Delete_Click(object sender, RoutedEventArgs e)

        {

            if (!_model.RoleId.HasValue)

                return;



            if (MessageBox.Show("Удалить эту роль?", "Роль", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)

                return;



            var err = RoleEditorService.TryDelete(_model.RoleId.Value, _systemRolesMode, _scope);

            if (err != null)

            {

                AppMessageBox.Show(err, "Удаление", MessageBoxButton.OK, MessageBoxImage.Warning);

                return;

            }



            DialogResult = true;

            Close();

        }



        private void Cancel_Click(object sender, RoutedEventArgs e)

        {

            DialogResult = false;

            Close();

        }

    }

}

