using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.ServiceVisits
{
    public static class RepairMileageService
    {
        public static Task<int?> TryLoadMileageAsync(Guid repairHistoryId) =>
            WithDb(db => TryLoadMileageAsync(db, repairHistoryId));

        public static async Task<int?> TryLoadMileageAsync(DriveCareDBEntities db, Guid repairHistoryId)
        {
            if (repairHistoryId == Guid.Empty)
                return null;

            try
            {
                return await db.RepairHistories.AsNoTracking()
                    .Where(r => r.RowId == repairHistoryId)
                    .Select(r => r.Mileage)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        public static Task<(bool ok, string error)> SaveRepairMileageAsync(
            Guid repairHistoryId,
            int mileageKm,
            Guid? clientUserId,
            Guid? carId) =>
            WithDb(db => SaveRepairMileageAsync(db, repairHistoryId, mileageKm, clientUserId, carId));

        public static async Task<(bool ok, string error)> SaveRepairMileageAsync(
            DriveCareDBEntities db,
            Guid repairHistoryId,
            int mileageKm,
            Guid? clientUserId,
            Guid? carId)
        {
            if (repairHistoryId == Guid.Empty)
                return (false, "Нет записи ремонта.");
            if (mileageKm < 1)
                return (false, "Укажите пробег больше нуля.");

            var repair = await db.RepairHistories.FirstOrDefaultAsync(r => r.RowId == repairHistoryId)
                .ConfigureAwait(false);
            if (repair == null)
                return (false, "Запись ремонта не найдена.");

            repair.Mileage = mileageKm;
            if (!repair.EndDate.HasValue)
                repair.EndDate = DateTime.Now;

            await db.SaveChangesAsync().ConfigureAwait(false);

            if (clientUserId.HasValue && clientUserId.Value != Guid.Empty
                && carId.HasValue && carId.Value != Guid.Empty)
            {
                await TrySyncUserCarMaintenanceAsync(db, clientUserId.Value, carId.Value, mileageKm, repair)
                    .ConfigureAwait(false);
            }

            return (true, null);
        }

        public static bool TryParseMileage(string text, out int km)
        {
            km = 0;
            var raw = (text ?? string.Empty).Trim().Replace(" ", "").Replace("\u00A0", "");
            if (raw.Length == 0)
                return false;
            if (!int.TryParse(raw, out km) || km < 1)
                return false;
            return true;
        }

        static async Task TrySyncUserCarMaintenanceAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid carId,
            int mileageKm,
            RepairHistory repair)
        {
            try
            {
                const string existsSql = @"
SELECT CASE WHEN OBJECT_ID(N'dbo.UserCarMaintenanceHistory', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                if (await db.Database.SqlQuery<int>(existsSql).FirstOrDefaultAsync().ConfigureAwait(false) != 1)
                    return;

                var userCarId = await db.UserCars.AsNoTracking()
                    .Where(uc => uc.UserId == userId && uc.CarId == carId)
                    .Select(uc => (Guid?)uc.RowId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                if (!userCarId.HasValue || userCarId.Value == Guid.Empty)
                    return;

                var title = string.IsNullOrWhiteSpace(repair.Title) ? "Визит в сервис" : repair.Title.Trim();
                var notes = string.IsNullOrWhiteSpace(repair.Description) ? null : repair.Description.Trim();

                await db.Database.ExecuteSqlCommandAsync(@"
INSERT INTO dbo.UserCarMaintenanceHistory
    (RowId, UserCarRowId, ServiceDate, MileageKm, Title, Notes)
VALUES (@id, @uc, @dt, @km, @title, @notes);",
                    new SqlParameter("@id", Guid.NewGuid()),
                    new SqlParameter("@uc", userCarId.Value),
                    new SqlParameter("@dt", repair.EndDate ?? repair.RepairDate),
                    new SqlParameter("@km", mileageKm),
                    new SqlParameter("@title", title),
                    new SqlParameter("@notes", (object)notes ?? DBNull.Value)).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> action)
        {
            using (var db = new DriveCareDBEntities())
                return await action(db).ConfigureAwait(false);
        }
    }
}
