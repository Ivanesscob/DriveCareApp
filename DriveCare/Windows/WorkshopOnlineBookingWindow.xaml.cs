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
            Loaded += async (_, __) => await LoadCarsAsync().ConfigureAwait(true);
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

            var (ok, error, _) = await WorkshopOnlineBookingService.CreateBookingAsync(
                AppState.CurrentUserId,
                _workshopId,
                userCarId,
                categoryCode,
                ResolveClientPhone(),
                fullComment,
                preferredDate: null).ConfigureAwait(true);

            if (!ok)
            {
                ShowError(error ?? "Не удалось отправить заявку.");
                return;
            }

            MessageBox.Show(
                "Заявка отправлена.\n\nАвтосервис подтвердит запись в DriveCare Pro. Время визита согласуете после подтверждения.",
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
