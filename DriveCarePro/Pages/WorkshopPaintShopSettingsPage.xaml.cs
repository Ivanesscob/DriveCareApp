using DriveCareCore.Maps;
using DriveCareCore.Painting;
using DriveCarePro.Services;
using DriveCarePro.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopPaintShopSettingsPage : Page
    {
        private Guid _workshopId;

        public WorkshopPaintShopSettingsPage()
        {
            InitializeComponent();
            Loaded += WorkshopPaintShopSettingsPage_Loaded;
        }

        private async void WorkshopPaintShopSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WorkshopPaintShopSettingsPage_Loaded;
            await LoadAsync().ConfigureAwait(true);
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private async void SaveTypes_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
                return;

            var emp = AppState.CurrentEmployee;
            if (emp == null)
                return;

            var ids = CollectSelectedTypeIds();
            bool ok;
            string error;
            string successMessage;

            if (AppState.IsCurrentEmployeeProAdmin)
            {
                var apply = WorkshopBusinessTypeModerationService.ApplyDirectly(_workshopId, ids);
                ok = apply.ok;
                error = apply.error;
                successMessage = "Типы мастерской сохранены. На карте DriveCare точка отобразится с выбранными фильтрами.";
            }
            else if (WorkshopBusinessTypeModerationService.RequiresOwnerModeration())
            {
                var submit = WorkshopBusinessTypeModerationService.SubmitChangeRequest(
                    _workshopId, emp.RowId, ids);
                ok = submit.ok;
                error = submit.error;
                successMessage = "Заявка отправлена администратору на согласование. До одобрения на карте остаются текущие типы.";
            }
            else
            {
                var apply = WorkshopBusinessTypesHelper.SetTypeIdsForWorkshop(_workshopId, ids);
                ok = apply.ok;
                error = apply.error;
                successMessage = "Типы мастерской сохранены.";
            }

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось сохранить.", "Типы мастерской",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show(successMessage, "Типы мастерской",
                MessageBoxButton.OK, MessageBoxImage.Information);
            LoadTypes();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            if (!CanManage())
            {
                MessageBox.Show("Нет прав на настройку покраски.", "Покраска",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                AppState.Navigate(new ProHomePage());
                return;
            }

            _workshopId = await ResolveWorkshopIdAsync(emp).ConfigureAwait(true);
            if (_workshopId == Guid.Empty)
            {
                HintText.Text = "Не удалось определить мастерскую.";
                return;
            }

            if (!WorkshopPaintCatalogService.TablesExist())
            {
                HintText.Text = "Выполните SQL: DriveCareCore/Data/BD/Sql/WorkshopPaintServices_Tables.sql и WorkshopBusinessTypes_Tables.sql";
            }
            else
            {
                HintText.Text = "Настройте типы на карте, цвета и услуги покраски для вашей мастерской.";
            }

            if (!WorkshopBusinessTypesHelper.JunctionTableExists())
            {
                HintText.Text += " Также: WorkshopBusinessTypes_Tables.sql — для нескольких типов в одной точке.";
            }

            LoadTypes();
            UpdateTypesSaveButton();
            await ReloadCatalogAsync().ConfigureAwait(true);
        }

        private void LoadTypes()
        {
            var approved = WorkshopBusinessTypesHelper.GetTypeIdsForWorkshop(_workshopId);
            var pending = WorkshopBusinessTypeModerationService.GetPendingForWorkshop(_workshopId);
            var displayIds = pending?.RequestedTypeIds ?? approved;

            ApplyTypeIdsToCheckboxes(displayIds);

            if (TypesStatusText != null)
            {
                if (pending != null)
                {
                    TypesStatusText.Text =
                        "На согласовании у администратора: " + pending.RequestedTypesLabel + ". "
                        + "Сейчас на карте: " + WorkshopServiceKinds.BuildKindsLabel(approved) + ".";
                    TypesStatusText.Visibility = Visibility.Visible;
                }
                else if (WorkshopBusinessTypeModerationService.RequiresOwnerModeration()
                         && !AppState.IsCurrentEmployeeProAdmin)
                {
                    TypesStatusText.Text =
                        "Изменение типов (добавить шиномонтаж, покраску и т.д.) отправляется администратору на согласование.";
                    TypesStatusText.Visibility = Visibility.Visible;
                }
                else
                {
                    TypesStatusText.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void UpdateTypesSaveButton()
        {
            if (BtnSaveTypes == null)
                return;

            if (AppState.IsCurrentEmployeeProAdmin)
                BtnSaveTypes.Content = "Сохранить типы";
            else if (WorkshopBusinessTypeModerationService.GetPendingForWorkshop(_workshopId) != null)
                BtnSaveTypes.Content = "Обновить заявку";
            else
                BtnSaveTypes.Content = "Отправить на согласование";
        }

        private List<Guid> CollectSelectedTypeIds()
        {
            var ids = new List<Guid>();
            if (ChkAutoService.IsChecked == true)
                ids.Add(WorkshopServiceKinds.AutoServiceId);
            if (ChkPainting.IsChecked == true)
                ids.Add(WorkshopServiceKinds.PaintingId);
            if (ChkTireService.IsChecked == true)
                ids.Add(WorkshopServiceKinds.TireServiceId);
            return ids;
        }

        private void ApplyTypeIdsToCheckboxes(IReadOnlyList<Guid> ids)
        {
            var list = ids ?? new List<Guid>();
            ChkAutoService.IsChecked = list.Contains(WorkshopServiceKinds.AutoServiceId);
            ChkPainting.IsChecked = list.Contains(WorkshopServiceKinds.PaintingId);
            ChkTireService.IsChecked = list.Contains(WorkshopServiceKinds.TireServiceId);

            if (list.Count == 0)
            {
                ChkAutoService.IsChecked = true;
                ChkPainting.IsChecked = false;
                ChkTireService.IsChecked = false;
            }
        }

        private async System.Threading.Tasks.Task ReloadCatalogAsync()
        {
            if (!WorkshopPaintCatalogService.TablesExist())
            {
                ColorsGrid.ItemsSource = null;
                ServicesGrid.ItemsSource = null;
                return;
            }

            ColorsGrid.ItemsSource = WorkshopPaintCatalogService.LoadManageColorsForWorkshop(_workshopId);
            ServicesGrid.ItemsSource = WorkshopPaintCatalogService.LoadManageServicesForWorkshop(_workshopId)
                .Select(s => new PaintServiceGridRow(s))
                .ToList();
        }

        private void AddColor_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
                return;

            var win = new WorkshopPaintColorEditWindow { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true || string.IsNullOrWhiteSpace(win.ColorName))
                return;

            var (ok, error) = WorkshopPaintCatalogService.AddColor(_workshopId, win.ColorName);
            if (!ok)
                MessageBox.Show(error ?? "Ошибка.", "Цвет", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                _ = ReloadCatalogAsync();
        }

        private async void DeleteColor_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is Guid rowId) || rowId == Guid.Empty)
                return;

            if (MessageBox.Show("Удалить цвет из каталога мастерской?", "Цвет",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var (ok, error) = WorkshopPaintCatalogService.DeactivateColor(rowId);
            if (!ok)
                MessageBox.Show(error ?? "Ошибка.", "Цвет", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                await ReloadCatalogAsync().ConfigureAwait(true);
        }

        private void AddService_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
                return;

            var win = new WorkshopPaintServiceEditWindow { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() != true)
                return;

            var (ok, error) = WorkshopPaintCatalogService.AddService(
                _workshopId, win.PaintKind, win.ServiceName, win.Description, win.PriceFrom);
            if (!ok)
                MessageBox.Show(error ?? "Ошибка.", "Услуга", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                _ = ReloadCatalogAsync();
        }

        private async void DeleteService_Click(object sender, RoutedEventArgs e)
        {
            if (!((sender as FrameworkElement)?.Tag is Guid rowId) || rowId == Guid.Empty)
                return;

            if (MessageBox.Show("Удалить услугу из каталога?", "Услуга",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var (ok, error) = WorkshopPaintCatalogService.DeactivateService(rowId);
            if (!ok)
                MessageBox.Show(error ?? "Ошибка.", "Услуга", MessageBoxButton.OK, MessageBoxImage.Warning);
            else
                await ReloadCatalogAsync().ConfigureAwait(true);
        }

        private static bool CanManage() =>
            AppState.IsCurrentEmployeeOwner ||
            AppState.HasAnyPermission(ProPermissions.CreateRepairs, ProPermissions.EditRepairs);

        private static async System.Threading.Tasks.Task<Guid> ResolveWorkshopIdAsync(DriveCareCore.Data.BD.Employee emp)
        {
            if (emp.WorkshopId.HasValue)
                return emp.WorkshopId.Value;

            var scope = await OwnerOrganizationScope.TryResolveAsync().ConfigureAwait(false);
            if (scope.ok && scope.scope.WorkshopIds.Count > 0)
                return scope.scope.WorkshopIds[0];

            return Guid.Empty;
        }

        private sealed class PaintServiceGridRow
        {
            public PaintServiceGridRow(WorkshopPaintServiceOffer s)
            {
                RowId = s.RowId;
                Name = s.Name;
                KindTitle = CarPaintService.GetKindTitle(s.PaintKind);
                PriceDisplay = s.PriceDisplay ?? "—";
            }

            public Guid RowId { get; }
            public string Name { get; }
            public string KindTitle { get; }
            public string PriceDisplay { get; }
        }
    }
}
