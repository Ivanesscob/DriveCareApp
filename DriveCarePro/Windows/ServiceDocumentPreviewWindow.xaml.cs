using DriveCarePro.Services.ServiceDocuments;
using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class ServiceDocumentPreviewWindow : Window
    {
        public ServiceDocumentPreviewWindow(ServiceDocumentPreview preview)
        {
            InitializeComponent();
            var info = preview?.Info;
            TitleText.Text = info?.Title ?? "Заказ-наряд";
            Title = "Заказ-наряд — " + (info?.Title ?? "просмотр");
            StatusText.Text = info?.StatusDisplay ?? string.Empty;
            ServicesGrid.ItemsSource = preview?.Services;
            PartsGrid.ItemsSource = preview?.Parts;
            ReportTextBox.Text = preview?.ReportText ?? string.Empty;
            RefreshActiveGrid();
        }

        void RefreshActiveGrid()
        {
            if (PreviewTabs?.SelectedItem is System.Windows.Controls.TabItem tab)
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

        public static void Show(Window owner, ServiceDocumentPreview preview)
        {
            if (preview == null)
                return;

            var win = new ServiceDocumentPreviewWindow(preview) { Owner = owner };
            win.ShowDialog();
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
