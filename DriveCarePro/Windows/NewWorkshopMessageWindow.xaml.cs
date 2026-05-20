using DriveCareCore.Messaging;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace DriveCarePro.Windows
{
    public partial class NewWorkshopMessageWindow : Window
    {
        private readonly Guid _workshopId;

        public Guid? CreatedConversationId { get; private set; }

        public NewWorkshopMessageWindow(Guid workshopId, IList<VisitorPickItem> visitors)
        {
            InitializeComponent();
            _workshopId = workshopId;
            VisitorCombo.ItemsSource = visitors ?? new List<VisitorPickItem>();
            if (visitors != null && visitors.Count > 0)
                VisitorCombo.SelectedIndex = 0;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private async void MessageText_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Return)
                return;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                return;

            e.Handled = true;
            await SendAsync().ConfigureAwait(true);
        }

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendAsync().ConfigureAwait(true);

        private async System.Threading.Tasks.Task SendAsync()
        {
            ErrorText.Visibility = Visibility.Collapsed;
            if (!(VisitorCombo.SelectedItem is VisitorPickItem visitor))
            {
                ShowError("Выберите клиента.");
                return;
            }

            var emp = AppState.CurrentEmployee;
            if (emp == null)
            {
                ShowError("Сотрудник не авторизован.");
                return;
            }

            var (ok, error, convId) = await WorkshopMessagingService.StartConversationAsync(
                _workshopId,
                visitor.UserId,
                emp.RowId,
                MessageText.Text).ConfigureAwait(true);

            if (!ok)
            {
                ShowError(error ?? "Не удалось отправить.");
                return;
            }

            CreatedConversationId = convId;
            WorkshopChatRealtimeClient.NotifyNewMessage(
                convId.Value,
                _workshopId,
                visitor.UserId,
                MessageSenderKind.Employee);
            DialogResult = true;
            Close();
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
