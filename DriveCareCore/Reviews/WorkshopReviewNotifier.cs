using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Reviews
{
    public static class WorkshopReviewNotifier
    {
        public static Task NotifyClientAfterRepairAsync(
            Guid clientUserId,
            Guid workshopId,
            Guid documentId,
            Guid? repairHistoryId,
            string workshopName) =>
            WithDb(db => NotifyClientAfterRepairAsync(db, clientUserId, workshopId, documentId, repairHistoryId, workshopName));

        public static async Task NotifyClientAfterRepairAsync(
            DriveCareDBEntities db,
            Guid clientUserId,
            Guid workshopId,
            Guid documentId,
            Guid? repairHistoryId,
            string workshopName)
        {
            if (clientUserId == Guid.Empty || workshopId == Guid.Empty || documentId == Guid.Empty)
                return;

            if (await WorkshopReviewService.HasReviewForDocumentAsync(db, clientUserId, documentId).ConfigureAwait(false))
                return;

            var wsName = string.IsNullOrWhiteSpace(workshopName) ? "автосервис" : workshopName.Trim();
            var notification = new Notification
            {
                RowId = Guid.NewGuid(),
                Title = "Оцените сервис",
                Message = $"Ремонт в «{wsName}» завершён. Поставьте оценку и оставьте отзыв — это поможет другим водителям.",
                Description = WorkshopReviewService.BuildNotificationDescription(documentId, workshopId, repairHistoryId),
                CreatedAt = DateTime.Now,
                IsViewed = false
            };

            db.Notifications.Add(notification);
            db.UserNotifications.Add(new UserNotification
            {
                RowId = Guid.NewGuid(),
                UserId = clientUserId,
                NotificationId = notification.RowId,
                IsRead = false
            });

            await db.SaveChangesAsync().ConfigureAwait(false);
        }

        static async Task WithDb(Func<DriveCareDBEntities, Task> action)
        {
            using (var db = new DriveCareDBEntities())
                await action(db).ConfigureAwait(false);
        }
    }
}
