using DriveCareCore.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DriveCare.Pages.User
{
    public partial class MessagesPage : Page
    {
        private Guid _selectedConversationId = Guid.Empty;
        private bool _tablesReady;
        private bool _suppressSelectionChanged;

        public MessagesPage()
        {
            InitializeComponent();
            Loaded += async (_, __) =>
            {
                WorkshopChatRealtimeClient.MessageReceived -= OnChatPush;
                WorkshopChatRealtimeClient.MessageReceived += OnChatPush;
                if (AppState.CurrentUserId != Guid.Empty)
                    WorkshopChatRealtimeClient.StartForUser(AppState.CurrentUserId);
                await LoadConversationsAsync().ConfigureAwait(true);
                if (AppState.PendingOpenConversationId.HasValue)
                {
                    _selectedConversationId = AppState.PendingOpenConversationId.Value;
                    AppState.PendingOpenConversationId = null;
                    if (ConversationsList.ItemsSource is IEnumerable<ConversationListItem> items)
                    {
                        var item = items.FirstOrDefault(c => c.ConversationId == _selectedConversationId);
                        if (item != null)
                        {
                            _suppressSelectionChanged = true;
                            try { ConversationsList.SelectedItem = item; }
                            finally { _suppressSelectionChanged = false; }
                            await LoadMessagesOnlyAsync().ConfigureAwait(true);
                        }
                    }
                }
            };
            Unloaded += (_, __) =>
            {
                WorkshopChatRealtimeClient.MessageReceived -= OnChatPush;
                WorkshopChatRealtimeClient.Stop();
                if (AppState.CurrentUserId != Guid.Empty)
                    WorkshopChatRealtimeClient.StartForUser(AppState.CurrentUserId);
            };
        }

        private async void OnChatPush(ChatPushEventArgs e)
        {
            if (e == null)
                return;
            await Dispatcher.InvokeAsync(async () =>
            {
                await LoadConversationsAsync().ConfigureAwait(true);
                if (_selectedConversationId != Guid.Empty && e.ConversationId == _selectedConversationId)
                    await LoadMessagesOnlyAsync().ConfigureAwait(true);
            });
        }

        private void Back_Click(object sender, RoutedEventArgs e) => AppState.SetFrame<UserHomePage>();

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadConversationsAsync().ConfigureAwait(true);
            if (_selectedConversationId != Guid.Empty)
                await LoadMessagesOnlyAsync().ConfigureAwait(true);
        }

        private async Task LoadConversationsAsync()
        {
            _tablesReady = WorkshopMessagingService.TablesExist();
            if (!_tablesReady)
            {
                ChatGrid.Visibility = Visibility.Collapsed;
                SetupHintText.Visibility = Visibility.Visible;
                SetupHintText.Text =
                    "Раздел сообщений не настроен в базе данных.\n\nВыполните на сервере скрипт:\nDriveCareCore/Data/BD/Sql/WorkshopMessaging_Tables.sql";
                return;
            }

            SetupHintText.Visibility = Visibility.Collapsed;
            ChatGrid.Visibility = Visibility.Visible;

            if (AppState.CurrentUserId == Guid.Empty)
            {
                EmptyDialogsText.Visibility = Visibility.Visible;
                ConversationsList.Visibility = Visibility.Collapsed;
                return;
            }

            var list = await WorkshopMessagingService.ListForUserAsync(AppState.CurrentUserId).ConfigureAwait(true);
            var keepId = _selectedConversationId;

            _suppressSelectionChanged = true;
            try
            {
                ConversationsList.ItemsSource = list;
                EmptyDialogsText.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                ConversationsList.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

                if (keepId != Guid.Empty)
                {
                    var item = list.FirstOrDefault(c => c.ConversationId == keepId);
                    if (item != null)
                        ConversationsList.SelectedItem = item;
                }
            }
            finally
            {
                _suppressSelectionChanged = false;
            }
        }

        private async void ConversationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChanged)
                return;

            if (!(ConversationsList.SelectedItem is ConversationListItem item))
            {
                if (e.RemovedItems.Count > 0 && e.AddedItems.Count == 0)
                    return;
                _selectedConversationId = Guid.Empty;
                ThreadTitleText.Text = "Выберите диалог";
                MessagesList.ItemsSource = null;
                return;
            }

            _selectedConversationId = item.ConversationId;
            ThreadTitleText.Text = item.ListTitle +
                                   (string.IsNullOrWhiteSpace(item.CompanyName) ? "" : " · " + item.CompanyName);
            await LoadMessagesOnlyAsync().ConfigureAwait(true);
        }

        private async Task LoadMessagesOnlyAsync()
        {
            if (_selectedConversationId == Guid.Empty || AppState.CurrentUserId == Guid.Empty)
                return;

            try
            {
                var messages = await WorkshopMessagingService.LoadMessagesAsync(
                    _selectedConversationId,
                    forUserSide: true,
                    AppState.CurrentUserId,
                    Guid.Empty).ConfigureAwait(true);

                MessagesList.ItemsSource = new ObservableCollection<ChatMessageItem>(
                    messages ?? new List<ChatMessageItem>());
                ScheduleScrollToEnd();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить сообщения: " + ex.Message, "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ScheduleScrollToEnd()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MessagesScroll.ScrollToEnd();
            }), DispatcherPriority.Loaded);
        }

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendMessageAsync().ConfigureAwait(true);

        private async void MessageInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!ChatInputKeyHelper.IsEnterKey(e))
                return;

            if (ChatInputKeyHelper.IsShiftHeld)
            {
                e.Handled = true;
                ChatInputKeyHelper.InsertNewLine(MessageInput);
                return;
            }

            e.Handled = true;
            await SendMessageAsync().ConfigureAwait(true);
        }

        private async Task SendMessageAsync()
        {
            if (_selectedConversationId == Guid.Empty)
            {
                MessageBox.Show("Выберите диалог слева.", "Сообщения", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (ok, error, _) = await WorkshopMessagingService.SendFromUserAsync(
                AppState.CurrentUserId,
                _selectedConversationId,
                MessageInput.Text).ConfigureAwait(true);

            if (!ok)
            {
                MessageBox.Show(error ?? "Не удалось отправить.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageInput.Clear();
            await LoadMessagesOnlyAsync().ConfigureAwait(true);
            await LoadConversationsAsync().ConfigureAwait(true);

            if (ConversationsList.SelectedItem is ConversationListItem conv)
            {
                WorkshopChatRealtimeClient.NotifyNewMessage(
                    _selectedConversationId,
                    conv.WorkshopId,
                    conv.UserId,
                    MessageSenderKind.User);
            }
        }
    }
}
