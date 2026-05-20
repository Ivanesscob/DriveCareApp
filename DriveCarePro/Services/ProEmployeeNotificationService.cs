using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services
{
    public sealed class ProEmployeeNotificationItem
    {
        public Guid EmployeeNotificationId { get; set; }
        public Guid TaskId { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public bool IsRead { get; set; }
        public string Kind { get; set; }
    }

    internal static class ProEmployeeNotificationService
    {
        public static async Task<List<ProEmployeeNotificationItem>> LoadForEmployeeAsync(Guid employeeId)
        {
            if (employeeId == Guid.Empty)
                return new List<ProEmployeeNotificationItem>();

            try
            {
                var rows = await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.SqlQuery<NotificationRow>(
                        @"SELECT en.RowId AS EmployeeNotificationId,
                                 en.TaskId,
                                 n.Title,
                                 n.Message,
                                 n.Description AS Kind,
                                 n.CreatedAt,
                                 en.IsRead
                          FROM EmployeeNotifications en
                          INNER JOIN Notifications n ON n.RowId = en.NotificationId
                          WHERE en.EmployeeId = @p0
                          ORDER BY n.CreatedAt DESC",
                        employeeId).ToListAsync()).ConfigureAwait(false);

                return rows.Select(Map).ToList();
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new List<ProEmployeeNotificationItem>();
            }
        }

        public static async Task<int> CountUnreadAsync(Guid employeeId)
        {
            if (employeeId == Guid.Empty)
                return 0;

            try
            {
                return await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.SqlQuery<int>(
                        "SELECT COUNT(1) FROM EmployeeNotifications WHERE EmployeeId = @p0 AND IsRead = 0",
                        employeeId).FirstOrDefaultAsync()).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return 0;
            }
        }

        public static Task MarkReadAsync(Guid employeeNotificationId) =>
            DatabaseExecutor.WithDbAsync(db =>
                db.Database.ExecuteSqlCommandAsync(
                    "UPDATE EmployeeNotifications SET IsRead = 1 WHERE RowId = @p0",
                    employeeNotificationId));

        private static ProEmployeeNotificationItem Map(NotificationRow row)
        {
            var date = row.CreatedAt.HasValue
                ? row.CreatedAt.Value.ToString("dd.MM.yyyy HH:mm")
                : string.Empty;
            var read = row.IsRead ? "прочитано" : "новое";
            var text = (row.Message ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(date))
                text += Environment.NewLine + date + " · " + read;

            return new ProEmployeeNotificationItem
            {
                EmployeeNotificationId = row.EmployeeNotificationId,
                TaskId = row.TaskId ?? Guid.Empty,
                Title = row.Title ?? string.Empty,
                Text = text,
                IsRead = row.IsRead,
                Kind = row.Kind ?? string.Empty
            };
        }

        private sealed class NotificationRow
        {
            public Guid EmployeeNotificationId { get; set; }
            public Guid? TaskId { get; set; }
            public string Title { get; set; }
            public string Message { get; set; }
            public string Kind { get; set; }
            public DateTime? CreatedAt { get; set; }
            public bool IsRead { get; set; }
        }
    }
}
