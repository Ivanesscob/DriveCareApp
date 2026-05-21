using System.Windows;

namespace DriveCarePro.Windows
{
    public partial class BookingAcceptWindow : Window
    {
        public string VisitWhenText { get; private set; }

        public BookingAcceptWindow(string summary, string defaultVisitWhen)
        {
            InitializeComponent();
            SummaryText.Text = summary ?? string.Empty;
            VisitWhenBox.Text = defaultVisitWhen ?? string.Empty;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Accept_Click(object sender, RoutedEventArgs e)
        {
            ErrorText.Visibility = Visibility.Collapsed;
            var text = (VisitWhenBox.Text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text))
            {
                ErrorText.Text = "Укажите, когда ждать клиента (дата и время).";
                ErrorText.Visibility = Visibility.Visible;
                return;
            }

            VisitWhenText = text;
            DialogResult = true;
            Close();
        }
    }
}
