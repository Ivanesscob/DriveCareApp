using System;

namespace DriveCareCore.Messaging
{
    public enum MessageSenderKind : byte
    {
        User = 0,
        Employee = 1
    }

    public sealed class ConversationListItem
    {
        public Guid ConversationId { get; set; }
        public Guid WorkshopId { get; set; }
        public Guid UserId { get; set; }
        public string WorkshopName { get; set; }
        public string CompanyName { get; set; }
        public string VisitorDisplayName { get; set; }
        public string LastMessagePreview { get; set; }
        public DateTime LastMessageAt { get; set; }
        public int UnreadCount { get; set; }
        public string LastMessageAtLabel => ChatTimeFormat.Format(LastMessageAt);
        public string ListTitle => string.IsNullOrWhiteSpace(WorkshopName) ? CompanyName : WorkshopName;
        public bool HasUnread => UnreadCount > 0;
    }

    public sealed class ChatMessageItem
    {
        public Guid MessageId { get; set; }
        public MessageSenderKind SenderKind { get; set; }
        public string SenderName { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsMine { get; set; }
        public string TimeLabel => ChatTimeFormat.Format(CreatedAt);
        public bool IsFromWorkshop => SenderKind == MessageSenderKind.Employee;
    }

    /// <summary>Время из SQL datetime без пояса — показываем как записано (локальное время ПК при отправке).</summary>
    internal static class ChatTimeFormat
    {
        public static string Format(DateTime value)
        {
            if (value <= new DateTime(1900, 1, 1))
                return string.Empty;
            if (value.Kind == DateTimeKind.Utc)
                value = value.ToLocalTime();
            return value.ToString("dd.MM.yyyy HH:mm");
        }
    }
}
