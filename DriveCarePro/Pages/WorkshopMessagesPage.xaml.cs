using DriveCareCore.Messaging;
using DriveCarePro.Services;
using DriveCarePro.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DriveCarePro.Pages
{
    public partial class WorkshopMessagesPage : Page
    {
        private readonly List<Guid> _workshopIds = new List<Guid>();
        private Guid _primaryWorkshopId = Guid.Empty;
        private Guid _selectedConversationId = Guid.Empty;
        private bool _suppressSelectionChanged;

        public WorkshopMessagesPage()
        {
            InitializeComponent();
            Loaded += async (_, __) =>
            {
                WorkshopChatRealtimeClient.MessageReceived -= OnChatPush;
                WorkshopChatRealtimeClient.MessageReceived += OnChatPush;
                await InitializeAsync().ConfigureAwait(true);
            };
            Unloaded += (_, __) =>
            {
                WorkshopChatRealtimeClient.MessageReceived -= OnChatPush;
                WorkshopChatRealtimeClient.Stop();
                if (_workshopIds.Count > 0)
                    WorkshopChatRealtimeClient.StartForWorkshops(_workshopIds);
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

        private void Back_Click(object sender, RoutedEventArgs e) => ProNavigation.GoHome();

        private async Task InitializeAsync()
        {
            if (!AppState.CanAccessEmployeeWorkspace)
            {
                MessageBox.Show(
                    "Сообщения клиентам доступны сотрудникам организации, не платформенному администратору.",
                    "Сообщения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                ProNavigation.GoHome();
                return;
            }

            _workshopIds.Clear();
            _primaryWorkshopId = Guid.Empty;

            if (OwnerOrganizationScope.TryResolve(out var scope, out _))
            {
                _workshopIds.AddRange(scope.WorkshopIds);
                ScopeHintText.Text = "Компания: " + scope.CompanyName;
            }
            else if (AppState.CurrentEmployee?.WorkshopId is Guid ws && ws != Guid.Empty)
            {
                _workshopIds.Add(ws);
                ScopeHintText.Text = "Ваша мастерская";
            }
            else
            {
                ScopeHintText.Text = "Мастерская не назначена";
            }

            _primaryWorkshopId = _workshopIds.FirstOrDefault();
            await LoadConversationsAsync().ConfigureAwait(true);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadConversationsAsync().ConfigureAwait(true);
            if (_selectedConversationId != Guid.Empty)
                await LoadMessagesOnlyAsync().ConfigureAwait(true);
        }

        private async Task LoadConversationsAsync()
        {
            if (!WorkshopMessagingService.TablesExist())
            {
                ChatGrid.Visibility = Visibility.Collapsed;
                SetupHintText.Visibility = Visibility.Visible;
                SetupHintText.Text =
                    "Таблицы сообщений не найдены. Выполните SQL WorkshopMessaging_Tables.sql на сервере БД.";
                return;
            }

            SetupHintText.Visibility = Visibility.Collapsed;
            ChatGrid.Visibility = Visibility.Visible;

            if (_workshopIds.Count == 0)
            {
                EmptyDialogsText.Visibility = Visibility.Visible;
                ConversationsList.Visibility = Visibility.Collapsed;
                return;
            }

            var list = await WorkshopMessagingService.ListForWorkshopsAsync(_workshopIds).ConfigureAwait(true);
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
            ThreadTitleText.Text = item.VisitorDisplayName + " · " + item.WorkshopName;
            await LoadMessagesOnlyAsync().ConfigureAwait(true);
        }

        private async Task LoadMessagesOnlyAsync()
        {
            var emp = AppState.CurrentEmployee;
            if (emp == null || _selectedConversationId == Guid.Empty)
                return;

            try
            {
                var messages = await WorkshopMessagingService.LoadMessagesAsync(
                    _selectedConversationId,
                    forUserSide: false,
                    Guid.Empty,
                    emp.RowId).ConfigureAwait(true);

                MessagesList.ItemsSource = messages ?? new List<ChatMessageItem>();
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
            void Scroll()
            {
                if (MessagesScroll == null)
                    return;
                MessagesScroll.UpdateLayout();
                MessagesScroll.ScrollToEnd();
            }

            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(Scroll));
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(Scroll));
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
            var emp = AppState.CurrentEmployee;
            if (emp == null)
                return;

            if (_selectedConversationId == Guid.Empty)
            {
                MessageBox.Show("Выберите диалог или создайте новый.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var (ok, error) = await WorkshopMessagingService.SendFromEmployeeAsync(
                emp.RowId,
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
                    MessageSenderKind.Employee);
            }
        }

        private async void NewMessage_Click(object sender, RoutedEventArgs e)
        {
            if (_primaryWorkshopId == Guid.Empty)
            {
                MessageBox.Show("Не назначена мастерская для отправки сообщений.", "Сообщения",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var visitors = await WorkshopMessagingService.ListVisitorsForWorkshopAsync(_primaryWorkshopId)
                .ConfigureAwait(true);
            if (visitors.Count == 0)
            {
                MessageBox.Show(
                    "Нет клиентов с учётной записью DriveCare.\n\nЗапишите посетителя на сервис с привязкой к пользователю или дождитесь регистрации клиента.",
                    "Сообщения", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new NewWorkshopMessageWindow(_primaryWorkshopId, visitors)
            {
                Owner = Window.GetWindow(this)
            };
            if (win.ShowDialog() != true || !win.CreatedConversationId.HasValue)
                return;

            _selectedConversationId = win.CreatedConversationId.Value;
            await LoadConversationsAsync().ConfigureAwait(true);

            _suppressSelectionChanged = true;
            try
            {
                if (ConversationsList.ItemsSource is IEnumerable<ConversationListItem> items)
                {
                    var item = items.FirstOrDefault(c => c.ConversationId == _selectedConversationId);
                    if (item != null)
                        ConversationsList.SelectedItem = item;
                }
            }
            finally
            {
                _suppressSelectionChanged = false;
            }

            await LoadMessagesOnlyAsync().ConfigureAwait(true);

            if (ConversationsList.SelectedItem is ConversationListItem created)
            {
                WorkshopChatRealtimeClient.NotifyNewMessage(
                    _selectedConversationId,
                    created.WorkshopId,
                    created.UserId,
                    MessageSenderKind.Employee);
            }
        }
    }
}
