using DriveCare;
using DriveCare.Pages.User;
using DriveCare.Windows;
using DriveCareCore.Data.BD;
using DriveCareCore.Reviews;
using DriveCareCore.ServiceVisits;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Task = System.Threading.Tasks.Task;

namespace DriveCare.Pages.User.ActionPages
{
    public partial class ServiceVisitsPage : Page
    {
        Guid? _filterUserCarId;
        bool _loading;

        public ServiceVisitsPage()
        {
            InitializeComponent();
            Loaded += async (_, __) => await LoadCarsAndVisitsAsync().ConfigureAwait(true);
        }

        public ServiceVisitsPage(Guid userCarRowId) : this()
        {
            _filterUserCarId = userCarRowId == Guid.Empty ? (Guid?)null : userCarRowId;
        }

        private void Back_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<ServiceCarPage>();

        private async void Refresh_Click(object sender, RoutedEventArgs e) =>
            await LoadVisitsAsync().ConfigureAwait(true);

        private async void CarCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _loading)
                return;
            await LoadVisitsAsync().ConfigureAwait(true);
        }

        async Task LoadCarsAndVisitsAsync()
        {
            SetLoading(true);
            await Task.Yield();

            CarCombo.ItemsSource = null;
            var cars = new List<CarDisplayItem> { new CarDisplayItem { Name = "Все автомобили", UserCarId = Guid.Empty } };

            if (AppState.CurrentUserId != Guid.Empty)
            {
                try
                {
                    var uid = AppState.CurrentUserId;
                    var rows = await AppConnect.model1.UserCars
                        .Include("Car")
                        .Include("Car.Model")
                        .Include("Car.Model.Brand")
                        .Where(uc => uc.UserId == uid)
                        .ToListAsync()
                        .ConfigureAwait(true);

                    var seen = new HashSet<Guid>();
                    foreach (var uc in rows.Where(r => r.Car != null))
                    {
                        if (seen.Contains(uc.CarId))
                            continue;
                        seen.Add(uc.CarId);

                        var brand = uc.Car.Model?.Brand?.Name?.Trim();
                        var model = uc.Car.Model?.Name?.Trim();
                        var name = !string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(model)
                            ? $"{brand} {model}"
                            : model ?? brand ?? "Автомобиль";

                        cars.Add(new CarDisplayItem
                        {
                            Name = name,
                            CarId = uc.CarId,
                            UserCarId = uc.RowId
                        });
                    }
                }
                catch
                {
                }
            }

            CarCombo.ItemsSource = cars;
            if (_filterUserCarId.HasValue && _filterUserCarId.Value != Guid.Empty)
            {
                var pick = cars.FirstOrDefault(c => c.UserCarId == _filterUserCarId.Value);
                CarCombo.SelectedItem = pick ?? cars[0];
            }
            else
                CarCombo.SelectedIndex = 0;

            await LoadVisitsAsync().ConfigureAwait(true);
            SetLoading(false);
        }

        async Task LoadVisitsAsync()
        {
            if (AppState.CurrentUserId == Guid.Empty)
            {
                StatusText.Text = "Войдите в аккаунт.";
                VisitsList.ItemsSource = null;
                return;
            }

            _loading = true;
            SetLoading(true);
            StatusText.Text = "Загрузка…";
            Guid? userCarId = null;
            if (CarCombo.SelectedItem is CarDisplayItem car && car.UserCarId != Guid.Empty)
                userCarId = car.UserCarId;

            try
            {
                var visits = await UserServiceVisitService.LoadVisitsAsync(AppState.CurrentUserId, userCarId)
                    .ConfigureAwait(true);
                VisitsList.ItemsSource = visits;
                StatusText.Text = visits.Count > 0
                    ? $"Записей: {visits.Count}. Заказ-наряд и оценка доступны после завершения ремонта."
                    : "Пока нет визитов в сервис с привязкой к вашему аккаунту.";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Ошибка: " + ex.Message;
                VisitsList.ItemsSource = null;
            }
            finally
            {
                _loading = false;
                SetLoading(false);
            }
        }

        void SetLoading(bool on) => LoadingOverlay.Visibility = on ? Visibility.Visible : Visibility.Collapsed;

        private async void ViewWorkOrder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is UserServiceVisitItem item)
                await OpenWorkOrderAsync(item).ConfigureAwait(true);
        }

        private async void RateService_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn && btn.DataContext is UserServiceVisitItem item))
                return;
            if (!item.CanOpenWorkOrder)
            {
                MessageBox.Show("Оценить можно после завершения ремонта.", "Оценка сервиса",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var request = new WorkshopReviewRequest
            {
                DocumentId = item.DocumentId,
                WorkshopId = item.WorkshopId,
                RepairHistoryId = item.RepairHistoryId,
                WorkshopName = item.WorkshopName
            };
            if (WorkshopReviewWindow.TryShow(Window.GetWindow(this), AppState.CurrentUserId, request))
                await LoadVisitsAsync().ConfigureAwait(true);
        }

        async Task OpenWorkOrderAsync(UserServiceVisitItem item)
        {
            if (item == null || item.DocumentId == Guid.Empty || AppState.CurrentUserId == Guid.Empty)
                return;

            if (!item.CanOpenWorkOrder)
            {
                MessageBox.Show("Заказ-наряд будет доступен после завершения ремонта в мастерской.",
                    "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            SetLoading(true);
            try
            {
                var (ok, error) = await UserWorkOrderDocxService.TryGenerateAndOpenAsync(
                    AppState.CurrentUserId, item.DocumentId).ConfigureAwait(true);
                if (!ok)
                {
                    MessageBox.Show(error ?? "Не удалось открыть заказ-наряд.",
                        "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Заказ-наряд", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                SetLoading(false);
            }
        }
    }
}
