using DriveCareCore.Bookings;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Windows
{
    public partial class WorkshopOnlineBookingWindow : Window
    {
        readonly Guid _workshopId;
        readonly string _workshopName;

        public WorkshopOnlineBookingWindow(Guid workshopId, string workshopName)
        {
            _workshopId = workshopId;
            _workshopName = workshopName ?? "Автосервис";
            InitializeComponent();
            WorkshopTitleText.Text = "Запись: " + _workshopName;
            IssueCategoryCombo.ItemsSource = WorkshopBookingIssueCategories.All;
            if (IssueCategoryCombo.Items.Count > 0)
                IssueCategoryCombo.SelectedIndex = 0;
            Loaded += async (_, __) =>
            {
                await LoadCarsAsync().ConfigureAwait(true);
                await LoadAvailableDatesAsync().ConfigureAwait(true);
            };
        }

        async System.Threading.Tasks.Task LoadAvailableDatesAsync()
        {
            if (!WorkshopOnlineBookingService.TablesExist())
            {
                ShowError("Онлайн-запись недоступна. Обратитесь к автосервису.");
                return;
            }

            var dates = await WorkshopOnlineBookingCapacity.GetAvailableDatesAsync(_workshopId)
                .ConfigureAwait(true);

            VisitDateCombo.ItemsSource = dates;
            if (dates.Count == 0)
            {
                ShowError("Нет свободных дней для записи. Выберите другой сервис или попробуйте позже.");
                VisitDateCombo.IsEnabled = false;
                return;
            }

            VisitDateCombo.SelectedIndex = 0;
            VisitDateCombo.IsEnabled = true;
        }

        async System.Threading.Tasks.Task LoadCarsAsync()
        {
            if (AppState.CurrentUserId == Guid.Empty)
            {
                ShowError("Войдите в аккаунт DriveCare.");
                return;
            }

            var cars = await WorkshopOnlineBookingService.LoadUserCarsAsync(AppState.CurrentUserId)
                .ConfigureAwait(true);

            CarCombo.ItemsSource = cars;
            if (cars.Count == 0)
            {
                ShowError("В гараже нет автомобилей. Добавьте машину на главной странице.");
                return;
            }

            CarCombo.SelectedIndex = 0;
        }

        static string ResolveClientPhone()
        {
            return AppState.CurrentUser?.Phone?.Trim() ?? string.Empty;
        }

        private void IssueCategoryCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var isOther = IssueCategoryCombo.SelectedValue as string == WorkshopBookingIssueCategories.OtherCode
                          || (IssueCategoryCombo.SelectedItem as WorkshopBookingIssueCategoryItem)?.RequiresDetails == true;
            OtherDetailsLabel.Visibility = isOther ? Visibility.Visible : Visibility.Collapsed;
            OtherDetailsBox.Visibility = isOther ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void Send_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;

            if (AppState.CurrentUserId == Guid.Empty)
            {
                ShowError("Войдите в аккаунт DriveCare.");
                return;
            }

            if (!(CarCombo.SelectedValue is Guid userCarId) || userCarId == Guid.Empty)
            {
                ShowError("Выберите автомобиль из списка.");
                return;
            }

            var categoryCode = IssueCategoryCombo.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(categoryCode))
            {
                ShowError("Выберите категорию неисправности.");
                return;
            }

            var commentParts = new System.Collections.Generic.List<string>();
            var isOther = categoryCode == WorkshopBookingIssueCategories.OtherCode;
            if (isOther)
            {
                var other = (OtherDetailsBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(other))
                {
                    ShowError("Опишите проблему в поле «Другое».");
                    return;
                }
                commentParts.Add(other);
            }

            var extra = (CommentBox.Text ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(extra))
                commentParts.Add(extra);

            var fullComment = string.Join("\n", commentParts);

            if (!(VisitDateCombo.SelectedItem is WorkshopBookingDateOption dateOption) || dateOption.Date == default)
            {
                ShowError("Выберите день визита из списка.");
                return;
            }

            var visitDate = dateOption.Date.Date;

            var (ok, error, _) = await WorkshopOnlineBookingService.CreateBookingAsync(
                AppState.CurrentUserId,
                _workshopId,
                userCarId,
                categoryCode,
                ResolveClientPhone(),
                fullComment,
                preferredDate: visitDate).ConfigureAwait(true);

            if (!ok)
            {
                ShowError(error ?? "Не удалось отправить заявку.");
                return;
            }

            DriveCareCore.Analytics.ActivityTracker.TrackUser(
                DriveCareCore.Analytics.ActivityEventCodes.WorkshopOnlineBookingCreate,
                AppState.CurrentUserId,
                workshopId: _workshopId,
                entityType: "WorkshopOnlineBooking");

            MessageBox.Show(
                "Заявка отправлена на " + visitDate.ToString("dd.MM.yyyy") + ".\n\nАвтосервис подтвердит запись в DriveCare Pro. Точное время согласуете после подтверждения.",
                "Онлайн-запись",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
