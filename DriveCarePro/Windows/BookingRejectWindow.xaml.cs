using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class BookingRejectWindow : Window
    {
        public string RejectReason { get; private set; }

        public BookingRejectWindow(string summary)
        {
            InitializeComponent();
            SummaryText.Text = summary ?? string.Empty;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Reject_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var text = (ReasonBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                ErrorText.Text = "Укажите причину отклонения.";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            RejectReason = text;
            DialogResult = true;
            Close();
        }
    }
}
