using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;

namespace DriveCareCore.Data.Services
{
    /// <summary>Уведомления сотрудникам Pro о заданиях (поручения, закупки).</summary>
    public static class EmployeeTaskNotifier
    {
        public const string KindDelegateComplete = "TaskDelegateComplete";
        public const string KindPurchaseFulfilled = "TaskPurchaseFulfilled";

        public static void NotifyDelegateChildCompleted(
            DriveCareDBEntities db,
            Guid completedChildTaskId,
            string completedByDisplayName)
        {
            if (db == null || !TableExists(db))
                return;

            Guid? parentTaskId;
            Guid? parentEmployeeId;
            string childTitle;

            try
            {
                var link = db.Database.SqlQuery<ParentLinkRow>(
                    "SELECT ParentTaskId FROM Tasks WHERE RowId = @p0", completedChildTaskId).FirstOrDefault();
                if (link?.ParentTaskId == null || link.ParentTaskId == Guid.Empty)
                    return;

                parentTaskId = link.ParentTaskId;
                var parent = db.Tasks.AsNoTracking().FirstOrDefault(t => t.RowId == parentTaskId.Value);
                if (parent == null)
                    return;

                var parentLinks = db.Database.SqlQuery<DelegateLinkRow>(
                    "SELECT DelegateTaskId FROM Tasks WHERE RowId = @p0", parentTaskId.Value).FirstOrDefault();
                if (parentLinks?.DelegateTaskId == null || parentLinks.DelegateTaskId != completedChildTaskId)
                    return;

                parentEmployeeId = parent.EmployeeId;
                childTitle = db.Tasks.AsNoTracking()
                    .Where(t => t.RowId == completedChildTaskId)
                    .Select(t => t.Title)
                    .FirstOrDefault() ?? "Поручение";
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return;
            }

            var who = string.IsNullOrWhiteSpace(completedByDisplayName) ? "Сотрудник" : completedByDisplayName.Trim();
            var title = "Поручение выполнено";
            var message = who + " завершил(а) переданное задание «" + TrimTitle(childTitle) +
                          "». Откройте своё задание и при необходимости завершите его.";

            Insert(db, parentEmployeeId.Value, parentTaskId.Value, KindDelegateComplete, title, message);
        }

        public static void NotifyPurchaseFulfilled(
            DriveCareDBEntities db,
            Guid sourceTaskId,
            Guid requesterEmployeeId,
            string purchaserDisplayName)
        {
            if (db == null || !TableExists(db) || sourceTaskId == Guid.Empty || requesterEmployeeId == Guid.Empty)
                return;

            string taskTitle;
            try
            {
                taskTitle = db.Tasks.AsNoTracking()
                    .Where(t => t.RowId == sourceTaskId)
                    .Select(t => t.Title)
                    .FirstOrDefault() ?? "Задание";
            }
            catch
            {
                taskTitle = "Задание";
            }

            var who = string.IsNullOrWhiteSpace(purchaserDisplayName) ? "Закупщик" : purchaserDisplayName.Trim();
            var title = "Закупка выполнена";
            var message = who + " завершил(а) закупку по заданию «" + TrimTitle(taskTitle) +
                          "». Детали добавлены в отчёт — откройте задание.";

            Insert(db, requesterEmployeeId, sourceTaskId, KindPurchaseFulfilled, title, message);
        }

        private static void Insert(
            DriveCareDBEntities db,
            Guid employeeId,
            Guid taskId,
            string kind,
            string title,
            string message)
        {
            var notificationId = Guid.NewGuid();
            var rowId = Guid.NewGuid();
            var now = DateTime.Now;

            var notification = new Notification
            {
                RowId = notificationId,
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Description = kind ?? string.Empty,
                CreatedAt = now,
                IsViewed = false
            };

            try
            {
                db.Notifications.Add(notification);
                db.SaveChanges();

                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO EmployeeNotifications (RowId, EmployeeId, NotificationId, TaskId, IsRead, CreatedAt)
                      VALUES (@p0, @p1, @p2, @p3, 0, @p4)",
                    rowId, employeeId, notificationId, taskId, now);
            }
            catch (SqlException)
            {
            }
        }

        private static bool TableExists(DriveCareDBEntities db)
        {
            try
            {
                var id = db.Database.SqlQuery<int?>(
                    "SELECT OBJECT_ID(N'dbo.EmployeeNotifications', N'U')").FirstOrDefault();
                return id.HasValue && id.Value > 0;
            }
            catch
            {
                return false;
            }
        }

        private static string TrimTitle(string title)
        {
            var t = (title ?? string.Empty).Trim();
            return t.Length > 80 ? t.Substring(0, 77) + "…" : t;
        }

        private sealed class ParentLinkRow
        {
            public Guid? ParentTaskId { get; set; }
        }

        private sealed class DelegateLinkRow
        {
            public Guid? DelegateTaskId { get; set; }
        }
    }
}
