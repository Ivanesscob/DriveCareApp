using DriveCareCore.ServiceVisits;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace DriveCare.Windows
{
    public partial class UserWorkOrderPreviewWindow : Window
    {
        public UserWorkOrderPreviewWindow(UserWorkOrderPreview preview)
        {
            InitializeComponent();
            var p = preview ?? new UserWorkOrderPreview();
            TitleText.Text = p.Title ?? "Заказ-наряд";
            Title = "Заказ-наряд — " + (p.WorkshopName ?? "просмотр");
            WorkshopText.Text = string.IsNullOrWhiteSpace(p.WorkshopName) ? string.Empty : "Мастерская: " + p.WorkshopName;
            ReasonText.Text = string.IsNullOrWhiteSpace(p.VisitReason)
                ? string.Empty
                : "Причина обращения: " + p.VisitReason.Trim();

            var mileage = p.MileageKm.HasValue
                ? p.MileageKm.Value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " км"
                : "не указан";
            var kind = string.IsNullOrWhiteSpace(p.ServiceKind) ? string.Empty : p.ServiceKind.Trim() + " · ";
            MetaText.Text = kind + p.StatusLabel + " · Пробег: " + mileage;

            ServicesGrid.ItemsSource = p.Services;
            PartsGrid.ItemsSource = p.Parts;
            ReportTextBox.Text = p.ReportText ?? string.Empty;
            RefreshActiveGrid();
        }

        void RefreshActiveGrid()
        {
            if (PreviewTabs?.SelectedItem is TabItem tab)
            {
                if (tab.Header as string == "Запчасти")
                    PartsGrid?.Items.Refresh();
                else if (tab.Header as string == "Услуги")
                    ServicesGrid?.Items.Refresh();
            }
        }

        private void PreviewTabs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshActiveGrid();
        }

        public static void Show(Window owner, UserWorkOrderPreview preview)
        {
            if (preview == null)
                return;
            var win = new UserWorkOrderPreviewWindow(preview) { Owner = owner };
            win.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
