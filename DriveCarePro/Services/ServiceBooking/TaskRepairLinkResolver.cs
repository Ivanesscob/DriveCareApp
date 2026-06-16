using DriveCareCore.Data.BD;
using DriveCarePro.Services.ServiceDocuments;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace DriveCarePro.Services.ServiceBooking
{
    /// <summary>Находит RepairHistoryId для задания (карточка, заказ-наряд).</summary>
    internal static class TaskRepairLinkResolver
    {
        public static async Task<Guid?> ResolveRepairHistoryIdAsync(
            DriveCareDBEntities db,
            Guid taskId,
            Guid? carId,
            Guid? fromTaskExtra)
        {
            if (fromTaskExtra.HasValue && fromTaskExtra.Value != Guid.Empty)
                return fromTaskExtra;

            try
            {
                var docId = await ServiceDocumentService.TryResolveDocumentIdForTaskAsync(db, taskId)
                    .ConfigureAwait(false);
                if (docId.HasValue)
                {
                    var fromDoc = await db.Database.SqlQuery<Guid?>(
                            @"SELECT RepairHistoryId FROM ServiceDocuments
                              WHERE RowId = @p0 AND RepairHistoryId IS NOT NULL",
                            docId.Value)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);
                    if (fromDoc.HasValue && fromDoc.Value != Guid.Empty)
                        return fromDoc;
                }
            }
            catch (SqlException)
            {
            }

            if (!carId.HasValue || carId.Value == Guid.Empty)
                return null;

            var fromCar = await db.RepairHistories.AsNoTracking()
                .Where(r => r.CarId == carId.Value)
                .OrderByDescending(r => r.RepairDate)
                .ThenByDescending(r => r.CreatedAt)
                .Select(r => r.RowId)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return fromCar != Guid.Empty ? (Guid?)fromCar : null;
        }
    }
}
