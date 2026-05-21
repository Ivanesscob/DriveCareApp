using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class WorkshopPaintColorEditWindow : Window
    {
        public string ColorName { get; private set; }

        public WorkshopPaintColorEditWindow()
        {
            InitializeComponent();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            ColorName = (ColorNameBox.Text ?? string.Empty).Trim();
            if (ColorName.Length == 0)
            {
                MessageBox.Show("Введите название цвета.", "Цвет",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
