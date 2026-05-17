using DriveCareCore.Data.BD;
using DriveCarePro;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DriveCarePro.Pages
{
    public partial class EmployeeTaskCardPage : Page
    {
        private readonly Guid _taskId;

        public EmployeeTaskCardPage(Guid taskId)
        {
            _taskId = taskId;
            InitializeComponent();
            Loaded += (_, __) => LoadTask();
        }

        private void Back_Click(object sender, RoutedEventArgs e) =>
            AppState.Navigate(new ProHomePage());

        private void Shop_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Здесь позже появится учёт покупок по заданию: выбранные позиции будут автоматически добавляться в поле отчёта.",
                "Магазин",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void LoadTask()
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                AppState.Navigate(new ProHomePage());
                return;
            }

            if (!AppState.CanAccessEmployeeTasks)
            {
                MessageBox.Show("Карточки заданий доступны работникам сервиса и главе автосалона.", "Задание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                AppState.Navigate(new ProHomePage());
                return;
            }

            var db = AppConnect.model1;
            var task = db.Tasks.FirstOrDefault(t => t.RowId == _taskId && t.EmployeeId == emp.RowId);
            if (task == null)
            {
                MessageBox.Show("Задание не найдено или назначено другому сотруднику.", "Задание", MessageBoxButton.OK, MessageBoxImage.Warning);
                AppState.Navigate(new ProHomePage());
                return;
            }

            TitleText.Text = string.IsNullOrWhiteSpace(task.Title) ? "Задание" : task.Title.Trim();
            TaskDescriptionText.Text = string.IsNullOrWhiteSpace(task.Description)
                ? "Описание не указано."
                : task.Description.Trim();

            CarInfoText.Text = BuildCarSummary(db, task.CarId);
            ClientInfoText.Text = BuildClientSummary(db, task.ClientUserId);

            ReportTextBox.Text = task.ReportText ?? string.Empty;
            ReportTextBox.IsReadOnly = task.IsCompleted;

            HoursCombo.ItemsSource = Enumerable.Range(0, 25).ToList();
            if (task.WorkHours.HasValue)
            {
                var h = (int)Math.Round(task.WorkHours.Value, MidpointRounding.AwayFromZero);
                if (h >= 0 && h <= 24)
                    HoursCombo.SelectedItem = h;
            }
            else
                HoursCombo.SelectedItem = 0;

            HoursCombo.IsEnabled = !task.IsCompleted;
            ShopButton.IsEnabled = !task.IsCompleted;
            CompleteButton.IsEnabled = !task.IsCompleted;
        }

        private static string BuildCarSummary(DriveCareDBEntities db, Guid? carId)
        {
            if (!carId.HasValue)
                return "Не указано.";
            var row = (from c in db.Cars
                       where c.RowId == carId.Value
                       join m in db.Models on c.ModelId equals m.RowId
                       join b in db.Brands on m.BrandId equals b.RowId
                       select new { Brand = b.Name, Model = m.Name, c.Year, c.Description }).FirstOrDefault();
            if (row == null)
                return "Автомобиль не найден в базе.";
            var year = row.Year.HasValue ? $" · {row.Year.Value} г." : string.Empty;
            var desc = string.IsNullOrWhiteSpace(row.Description) ? string.Empty : $"\n{row.Description.Trim()}";
            return $"{(row.Brand ?? "").Trim()} {(row.Model ?? "").Trim()}{year}{desc}";
        }

        private static string BuildClientSummary(DriveCareDBEntities db, Guid? userId)
        {
            if (!userId.HasValue)
                return "Не указано.";
            var u = db.Users.FirstOrDefault(x => x.RowId == userId.Value);
            if (u == null)
                return "Клиент не найден в базе.";
            var login = string.IsNullOrWhiteSpace(u.Login) ? "—" : u.Login.Trim();
            var phone = string.IsNullOrWhiteSpace(u.Phone) ? "—" : u.Phone.Trim();
            var email = string.IsNullOrWhiteSpace(u.Email) ? "—" : u.Email.Trim();
            return $"Логин: {login}\nТелефон: {phone}\nEmail: {email}";
        }

        private void Complete_Click(object sender, RoutedEventArgs e)
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null || !AppState.CanAccessEmployeeTasks)
                return;

            var db = AppConnect.model1;
            var task = db.Tasks.FirstOrDefault(t => t.RowId == _taskId && t.EmployeeId == emp.RowId);
            if (task == null || task.IsCompleted)
                return;

            if (!(HoursCombo.SelectedItem is int hours) || hours < 0 || hours > 24)
            {
                MessageBox.Show("Выберите количество часов (0–24).", "Задание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            task.ReportText = (ReportTextBox.Text ?? string.Empty).Trim();
            task.WorkHours = hours;
            task.IsCompleted = true;
            task.EndDate = DateTime.Now;
            try
            {
                db.SaveChanges();
                MessageBox.Show("Задание отмечено как выполненное.", "Задание", MessageBoxButton.OK, MessageBoxImage.Information);
                AppState.Navigate(new ProHomePage());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить: " + ex.Message, "Задание", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
