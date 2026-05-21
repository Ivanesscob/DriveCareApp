using DriveCareCore.Data.BD;
using System;
using System.Data.SqlClient;
using System.Linq;

namespace DriveCareCore.Data.Services
{
    public static class WorkshopBusinessTypeModerationNotifier
    {
        public const string Kind = "WorkshopBusinessTypeModeration";

        public static void NotifyRequester(
            DriveCareDBEntities db,
            Guid employeeId,
            bool approved,
            Guid workshopId,
            string approvedTypesLabel,
            string rejectionComment)
        {
            if (db == null || employeeId == Guid.Empty || !EmployeeNotificationsTableExists(db))
                return;

            string workshopName;
            try
            {
                workshopName = db.Database.SqlQuery<string>(
                    "SELECT Name FROM dbo.Workshops WHERE RowId = @p0", workshopId).FirstOrDefault()
                    ?? "мастерская";
            }
            catch
            {
                workshopName = "мастерская";
            }

            string title;
            string message;
            if (approved)
            {
                title = "Типы мастерской одобрены";
                message = "Администратор одобрил смену типов для «" + workshopName.Trim() + "». "
                    + "На карте DriveCare: " + (approvedTypesLabel ?? "—") + ".";
            }
            else
            {
                title = "Заявка на смену типов отклонена";
                message = "Заявка для «" + workshopName.Trim() + "» отклонена."
                    + (string.IsNullOrWhiteSpace(rejectionComment)
                        ? string.Empty
                        : " Причина: " + rejectionComment.Trim());
            }

            var notificationId = Guid.NewGuid();
            var rowId = Guid.NewGuid();
            var now = DateTime.Now;

            try
            {
                db.Notifications.Add(new Notification
                {
                    RowId = notificationId,
                    Title = title,
                    Message = message,
                    Description = Kind,
                    CreatedAt = now,
                    IsViewed = false
                });
                db.SaveChanges();

                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO EmployeeNotifications (RowId, EmployeeId, NotificationId, TaskId, IsRead, CreatedAt)
                      VALUES (@p0, @p1, @p2, @p3, 0, @p4)",
                    rowId, employeeId, notificationId, Guid.Empty, now);
            }
            catch (SqlException)
            {
            }
        }

        static bool EmployeeNotificationsTableExists(DriveCareDBEntities db)
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
    }
}
