using DriveCareCore.Bookings;
using DriveCareCore.Data.BD;
using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class WorkshopWorkSchedulePage : Page
    {
        readonly ObservableCollection<ScheduleDayVm> _days = new ObservableCollection<ScheduleDayVm>();
        List<WorkshopPickerItem> _workshops = new List<WorkshopPickerItem>();
        Guid _workshopId = Guid.Empty;

        public WorkshopWorkSchedulePage()
        {
            InitializeComponent();
            ScheduleGrid.ItemsSource = _days;
            Loaded += async (_, __) => await InitializeAsync().ConfigureAwait(true);
        }

        async System.Threading.Tasks.Task InitializeAsync()
        {
            if (!WorkshopWorkScheduleService.TablesExist())
            {
                HintText.Text = "Выполните SQL: DriveCareCore/Data/BD/Sql/WorkshopWorkSchedule_Tables.sql";
                ScheduleGrid.IsEnabled = false;
                return;
            }

            if (!CanManageSchedule())
            {
                HintText.Text = "Нет разрешения на настройку расписания (MANAGE_WORKSHOP_SCHEDULE).";
                ScheduleGrid.IsEnabled = false;
                return;
            }

            HintText.Text = "Часы работы по дням недели и лимит машин на онлайн-запись в день. Сохраните изменения.";

            if (!OwnerOrganizationScope.TryResolve(out var scope, out var err))
            {
                HintText.Text = err ?? "Не удалось определить организацию.";
                return;
            }

            _workshops = await LoadWorkshopNamesAsync(scope.WorkshopIds).ConfigureAwait(true);
            if (_workshops.Count == 0)
            {
                HintText.Text = "Нет мастерских в организации.";
                return;
            }

            if (_workshops.Count > 1)
            {
                WorkshopPickerPanel.Visibility = Visibility.Visible;
                WorkshopCombo.ItemsSource = _workshops;
                WorkshopCombo.SelectedIndex = 0;
            }
            else
            {
                WorkshopPickerPanel.Visibility = Visibility.Collapsed;
                _workshopId = _workshops[0].RowId;
                await LoadScheduleAsync().ConfigureAwait(true);
            }

            ShowCapacityPanel();
        }

        void ShowCapacityPanel()
        {
            CapacityPanel.Visibility = Visibility.Visible;
            if (!WorkshopOnlineBookingCapacity.SettingsTableExists())
            {
                CapacityHintText.Text = "Выполните SQL: DriveCareCore/Data/BD/Sql/WorkshopOnlineBooking_Capacity.sql — иначе лимит не сохранится в базе.";
            }
            else
            {
                CapacityHintText.Text = "Лимит записей в DriveCare на один день. При достижении лимита день скрывается из выбора у клиента.";
            }
        }

        static bool CanManageSchedule() =>
            AppState.HasPermission(ProPermissions.ManageWorkshopSchedule) || AppState.IsCurrentEmployeeOwner;

        static async System.Threading.Tasks.Task<List<WorkshopPickerItem>> LoadWorkshopNamesAsync(IReadOnlyList<Guid> ids)
        {
            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                return await db.Workshops
                    .Where(w => ids.Contains(w.RowId))
                    .OrderBy(w => w.Name)
                    .Select(w => new WorkshopPickerItem { RowId = w.RowId, Name = w.Name })
                    .ToListAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        async void WorkshopCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WorkshopCombo.SelectedItem is WorkshopPickerItem item)
            {
                _workshopId = item.RowId;
                await LoadScheduleAsync().ConfigureAwait(true);
            }
        }

        async System.Threading.Tasks.Task LoadScheduleAsync()
        {
            if (_workshopId == Guid.Empty)
                return;

            var schedule = await WorkshopWorkScheduleService.GetScheduleAsync(_workshopId).ConfigureAwait(true);
            _days.Clear();
            foreach (var d in schedule)
            {
                _days.Add(new ScheduleDayVm
                {
                    DayOfWeek = d.DayOfWeek,
                    DayName = d.DayName,
                    IsClosed = d.IsClosed,
                    OpenTimeText = d.OpenTimeText,
                    CloseTimeText = d.CloseTimeText
                });
            }

            if (WorkshopOnlineBookingCapacity.SettingsTableExists())
            {
                var cap = await WorkshopOnlineBookingCapacity.GetSettingsAsync(_workshopId).ConfigureAwait(true);
                MaxBookingsPerDayBox.Text = cap.MaxBookingsPerDay.ToString();
            }
            else
                MaxBookingsPerDayBox.Text = WorkshopOnlineBookingCapacity.DefaultMaxPerDay.ToString();

            StatusText.Text = "Расписание загружено.";
        }

        private void Back_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private void MaxCarsDecrease_Click(object sender, RoutedEventArgs e) =>
            SetMaxCarsPerDay(ReadMaxCarsPerDay() - 1);

        private void MaxCarsIncrease_Click(object sender, RoutedEventArgs e) =>
            SetMaxCarsPerDay(ReadMaxCarsPerDay() + 1);

        private void SetMaxCarsPerDay(int value)
        {
            if (value < 1) value = 1;
            if (value > 999) value = 999;
            MaxBookingsPerDayBox.Text = value.ToString();
        }

        int ReadMaxCarsPerDay()
        {
            return int.TryParse((MaxBookingsPerDayBox.Text ?? string.Empty).Trim(), out var n) && n > 0
                ? n
                : WorkshopOnlineBookingCapacity.DefaultMaxPerDay;
        }

        private async void SaveCapacityOnly_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
            {
                MessageBox.Show("Выберите мастерскую.", "Лимит записей",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!WorkshopOnlineBookingCapacity.SettingsTableExists())
            {
                MessageBox.Show(
                    "Выполните SQL на сервере:\nDriveCareCore/Data/BD/Sql/WorkshopOnlineBooking_Capacity.sql",
                    "Лимит записей", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse((MaxBookingsPerDayBox.Text ?? string.Empty).Trim(), out var maxPerDay)
                || maxPerDay < 1 || maxPerDay > 999)
            {
                MessageBox.Show("Укажите число от 1 до 999.", "Лимит записей",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var capacitySave = await WorkshopOnlineBookingCapacity.SaveSettingsAsync(_workshopId, maxPerDay)
                .ConfigureAwait(true);
            if (!capacitySave.ok)
            {
                MessageBox.Show(capacitySave.error ?? "Не удалось сохранить.", "Лимит записей",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusText.Text = "Лимит машин в день сохранён: " + maxPerDay + ".";
            MessageBox.Show("Сохранено: не более " + maxPerDay + " машин в день для онлайн-записи.",
                "Лимит записей", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ResetDefault_Click(object sender, RoutedEventArgs e)
        {
            _days.Clear();
            for (var day = 1; day <= 7; day++)
            {
                var closed = day >= 6;
                _days.Add(new ScheduleDayVm
                {
                    DayOfWeek = day,
                    DayName = GetDayName(day),
                    IsClosed = closed,
                    OpenTimeText = closed ? string.Empty : "09:00",
                    CloseTimeText = closed ? string.Empty : "18:00"
                });
            }
            SetMaxCarsPerDay(WorkshopOnlineBookingCapacity.DefaultMaxPerDay);
            StatusText.Text = "Шаблон: пн–пт 09:00–18:00, сб–вс выходной. Нажмите «Сохранить».";
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            if (_workshopId == Guid.Empty)
            {
                MessageBox.Show("Выберите мастерскую.", "Расписание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var input = _days.Select(d => new WorkshopWorkScheduleDayInput
            {
                DayOfWeek = d.DayOfWeek,
                IsClosed = d.IsClosed,
                OpenTimeText = d.IsClosed ? null : d.OpenTimeText,
                CloseTimeText = d.IsClosed ? null : d.CloseTimeText
            }).ToList();

            var scheduleSave = await WorkshopWorkScheduleService.SaveScheduleAsync(_workshopId, input)
                .ConfigureAwait(true);

            if (!scheduleSave.ok)
            {
                MessageBox.Show(scheduleSave.error ?? "Не удалось сохранить.", "Расписание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (WorkshopOnlineBookingCapacity.SettingsTableExists())
            {
                if (!int.TryParse((MaxBookingsPerDayBox.Text ?? string.Empty).Trim(), out var maxPerDay))
                {
                    MessageBox.Show("Укажите целое число машин в день (1–999).", "Расписание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var capacitySave = await WorkshopOnlineBookingCapacity.SaveSettingsAsync(_workshopId, maxPerDay)
                    .ConfigureAwait(true);
                if (!capacitySave.ok)
                {
                    MessageBox.Show(capacitySave.error ?? "Не удалось сохранить лимит записей.", "Расписание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            StatusText.Text = "Расписание сохранено.";
            MessageBox.Show("Расписание и лимит онлайн-записей сохранены.", "Расписание",
                MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadScheduleAsync().ConfigureAwait(true);
        }

        static string GetDayName(int day)
        {
            switch (day)
            {
                case 1: return "Понедельник";
                case 2: return "Вторник";
                case 3: return "Среда";
                case 4: return "Четверг";
                case 5: return "Пятница";
                case 6: return "Суббота";
                case 7: return "Воскресенье";
                default: return "День " + day;
            }
        }

        sealed class WorkshopPickerItem
        {
            public Guid RowId { get; set; }
            public string Name { get; set; }
        }

        public sealed class ScheduleDayVm : INotifyPropertyChanged
        {
            int _dayOfWeek;
            string _dayName;
            bool _isClosed;
            string _openTimeText = "09:00";
            string _closeTimeText = "18:00";

            public int DayOfWeek
            {
                get => _dayOfWeek;
                set { _dayOfWeek = value; OnPropertyChanged(); }
            }

            public string DayName
            {
                get => _dayName;
                set { _dayName = value; OnPropertyChanged(); }
            }

            public bool IsClosed
            {
                get => _isClosed;
                set { _isClosed = value; OnPropertyChanged(); }
            }

            public string OpenTimeText
            {
                get => _openTimeText;
                set { _openTimeText = value ?? string.Empty; OnPropertyChanged(); }
            }

            public string CloseTimeText
            {
                get => _closeTimeText;
                set { _closeTimeText = value ?? string.Empty; OnPropertyChanged(); }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
