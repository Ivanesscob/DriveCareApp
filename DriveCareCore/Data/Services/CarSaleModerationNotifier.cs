using DriveCareCore.Data.BD;
using System;
using System.Linq;

namespace DriveCareCore.Data.Services
{
    public static class CarSaleModerationNotifier
    {
        public static void NotifySeller(DriveCareDBEntities db, Guid carSaleId, string title, string message)
        {
            if (db == null)
                return;
            var link = db.UserCarSales.FirstOrDefault(u => u.CarSaleId == carSaleId);
            if (link == null)
                return;
            var carId = db.CarSales.Where(c => c.RowId == carSaleId).Select(c => (Guid?)c.CarId).FirstOrDefault();

            var notification = new Notification
            {
                RowId = Guid.NewGuid(),
                Title = title ?? string.Empty,
                Message = message ?? string.Empty,
                Description = "CarSaleModeration",
                CarId = carId,
                CreatedAt = DateTime.Now
            };
            db.Notifications.Add(notification);
            db.UserNotifications.Add(new UserNotification
            {
                RowId = Guid.NewGuid(),
                UserId = link.UserId,
                NotificationId = notification.RowId,
                IsRead = false
            });
        }
    }
}
