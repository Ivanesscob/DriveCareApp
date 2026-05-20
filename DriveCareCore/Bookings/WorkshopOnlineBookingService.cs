using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Bookings
{
    public static class WorkshopOnlineBookingService
    {
        public static bool TablesExist()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopOnlineBookings', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task<List<UserCarPickerItem>> LoadUserCarsAsync(Guid userId) =>
            WithDb(db => LoadUserCarsAsync(db, userId));

        public static async Task<List<UserCarPickerItem>> LoadUserCarsAsync(DriveCareDBEntities db, Guid userId)
        {
            if (userId == Guid.Empty)
                return new List<UserCarPickerItem>();

            var rows = await db.UserCars
                .Include(uc => uc.Car)
                .Include(uc => uc.Car.Model)
                .Include(uc => uc.Car.Model.Brand)
                .Where(uc => uc.UserId == userId)
                .OrderBy(uc => uc.Car.Model.Brand.Name)
                .ThenBy(uc => uc.Car.Model.Name)
                .ToListAsync()
                .ConfigureAwait(false);

            var result = new List<UserCarPickerItem>();
            var seen = new HashSet<Guid>();
            foreach (var uc in rows.Where(r => r.Car != null))
            {
                if (!seen.Add(uc.RowId))
                    continue;

                var brand = uc.Car.Model?.Brand?.Name?.Trim() ?? string.Empty;
                var model = uc.Car.Model?.Name?.Trim() ?? string.Empty;
                var name = string.IsNullOrEmpty(brand) && string.IsNullOrEmpty(model)
                    ? "Автомобиль"
                    : (brand + " " + model).Trim();
                if (!string.IsNullOrWhiteSpace(uc.Description))
                    name += " — " + uc.Description.Trim();
                if (uc.Car.Year.HasValue && uc.Car.Year.Value > 0)
                    name += " (" + uc.Car.Year + ")";

                result.Add(new UserCarPickerItem
                {
                    UserCarId = uc.RowId,
                    CarId = uc.CarId,
                    DisplayName = name
                });
            }

            return result;
        }

        public static Task<(bool ok, string error, Guid? bookingId)> CreateBookingAsync(
            Guid userId,
            Guid workshopId,
            Guid userCarId,
            string issueCategory,
            string phone,
            string comment,
            DateTime? preferredDate) =>
            WithDb(db => CreateBookingAsync(db, userId, workshopId, userCarId, issueCategory, phone, comment, preferredDate));

        public static async Task<(bool ok, string error, Guid? bookingId)> CreateBookingAsync(
            DriveCareDBEntities db,
            Guid userId,
            Guid workshopId,
            Guid userCarId,
            string issueCategory,
            string phone,
            string comment,
            DateTime? preferredDate)
        {
            if (!TablesExist())
                return (false, "Таблица записей не найдена. Выполните SQL WorkshopOnlineBookings_Tables.sql.", null);
            if (userId == Guid.Empty || workshopId == Guid.Empty)
                return (false, "Не указан клиент или автосервис.", null);
            if (userCarId == Guid.Empty)
                return (false, "Выберите автомобиль из гаража.", null);
            if (string.IsNullOrWhiteSpace(issueCategory))
                return (false, "Выберите категорию неисправности.", null);

            var carOk = await db.UserCars.AnyAsync(uc => uc.RowId == userCarId && uc.UserId == userId)
                .ConfigureAwait(false);
            if (!carOk)
                return (false, "Автомобиль не найден в вашем гараже.", null);

            var exists = await db.Database.SqlQuery<int>(
                "SELECT COUNT(1) FROM dbo.Workshops WHERE RowId = @w", new SqlParameter("@w", workshopId))
                .FirstOrDefaultAsync().ConfigureAwait(false);
            if (exists == 0)
                return (false, "Автосервис не найден.", null);

            var hasIssueCol = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "IssueCategory")
                .ConfigureAwait(false);

            var clientPhone = (phone ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(clientPhone))
            {
                clientPhone = await db.Users
                    .Where(u => u.RowId == userId)
                    .Select(u => u.Phone)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false) ?? string.Empty;
                clientPhone = clientPhone.Trim();
            }

            var id = Guid.NewGuid();
            try
            {
                if (hasIssueCol)
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        @"INSERT INTO dbo.WorkshopOnlineBookings
                          (RowId, WorkshopId, UserId, UserCarId, IssueCategory, ClientPhone, ClientComment, PreferredDate, Status, CreatedAt)
                          VALUES (@id, @w, @u, @car, @cat, @ph, @cm, @dt, 0, GETDATE())",
                        new SqlParameter("@id", id),
                        new SqlParameter("@w", workshopId),
                        new SqlParameter("@u", userId),
                        new SqlParameter("@car", userCarId),
                        new SqlParameter("@cat", issueCategory.Trim()),
                        new SqlParameter("@ph", (object)clientPhone ?? DBNull.Value),
                        new SqlParameter("@cm", (object)(comment ?? string.Empty).Trim() ?? DBNull.Value),
                        new SqlParameter("@dt", (object)preferredDate ?? DBNull.Value)).ConfigureAwait(false);
                }
                else
                {
                    var catLabel = WorkshopBookingIssueCategories.GetDisplayName(issueCategory);
                    var mergedComment = "Категория: " + catLabel;
                    if (!string.IsNullOrWhiteSpace(comment))
                        mergedComment += "\n" + comment.Trim();

                    await db.Database.ExecuteSqlCommandAsync(
                        @"INSERT INTO dbo.WorkshopOnlineBookings
                          (RowId, WorkshopId, UserId, UserCarId, ClientPhone, ClientComment, PreferredDate, Status, CreatedAt)
                          VALUES (@id, @w, @u, @car, @ph, @cm, @dt, 0, GETDATE())",
                        new SqlParameter("@id", id),
                        new SqlParameter("@w", workshopId),
                        new SqlParameter("@u", userId),
                        new SqlParameter("@car", userCarId),
                        new SqlParameter("@ph", (object)clientPhone ?? DBNull.Value),
                        new SqlParameter("@cm", mergedComment),
                        new SqlParameter("@dt", (object)preferredDate ?? DBNull.Value)).ConfigureAwait(false);
                }

                return (true, null, id);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static Task<List<WorkshopOnlineBookingItem>> ListForWorkshopsAsync(
            IList<Guid> workshopIds,
            bool pendingOnly) =>
            WithDb(db => ListForWorkshopsAsync(db, workshopIds, pendingOnly));

        public static async Task<List<WorkshopOnlineBookingItem>> ListForWorkshopsAsync(
            DriveCareDBEntities db,
            IList<Guid> workshopIds,
            bool pendingOnly)
        {
            if (!TablesExist() || workshopIds == null || workshopIds.Count == 0)
                return new List<WorkshopOnlineBookingItem>();

            var hasIssueCol = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "IssueCategory")
                .ConfigureAwait(false);

            var ids = workshopIds.Distinct().ToList();
            var paramNames = ids.Select((_, i) => "@w" + i).ToList();
            var statusFilter = pendingOnly ? " AND b.Status = 0 " : string.Empty;
            var issueSelect = hasIssueCol ? "b.IssueCategory" : "CAST(NULL AS NVARCHAR(120)) AS IssueCategory";
            var sql = $@"
SELECT b.RowId AS BookingId, b.WorkshopId, w.Name AS WorkshopName, b.UserId,
       COALESCE(NULLIF(LTRIM(RTRIM(u.Login)), N''), u.Email, N'Клиент') AS ClientDisplayName,
       b.ClientPhone, b.ClientComment, {issueSelect},
       COALESCE(
         NULLIF(LTRIM(RTRIM(CONCAT(br.Name, N' ', m.Name))), N''),
         N'Автомобиль'
       ) AS CarDisplayName,
       b.PreferredDate, b.Status AS StatusCode, b.CreatedAt
FROM dbo.WorkshopOnlineBookings b
INNER JOIN dbo.Workshops w ON w.RowId = b.WorkshopId
INNER JOIN dbo.Users u ON u.RowId = b.UserId
LEFT JOIN dbo.UserCars uc ON uc.RowId = b.UserCarId
LEFT JOIN dbo.Cars c ON c.RowId = uc.CarId
LEFT JOIN dbo.Models m ON m.RowId = c.ModelId
LEFT JOIN dbo.Brands br ON br.RowId = m.BrandId
WHERE b.WorkshopId IN ({string.Join(",", paramNames)}) {statusFilter}
ORDER BY b.CreatedAt DESC;";

            var parameters = ids.Select((id, i) => new SqlParameter(paramNames[i], id)).ToArray();
            return await db.Database.SqlQuery<WorkshopOnlineBookingItem>(sql, parameters)
                .ToListAsync().ConfigureAwait(false);
        }

        public static Task<(bool ok, string error)> ConfirmBookingAsync(Guid bookingId, Guid employeeId) =>
            WithDb(db => ConfirmBookingAsync(db, bookingId, employeeId));

        public static async Task<(bool ok, string error)> ConfirmBookingAsync(
            DriveCareDBEntities db,
            Guid bookingId,
            Guid employeeId)
        {
            if (!TablesExist())
                return (false, "Таблица записей не найдена.");
            if (bookingId == Guid.Empty || employeeId == Guid.Empty)
                return (false, "Не указана запись или сотрудник.");

            var n = await db.Database.ExecuteSqlCommandAsync(
                @"UPDATE dbo.WorkshopOnlineBookings
                  SET Status = 1, ConfirmedAt = GETDATE(), ConfirmedByEmployeeId = @e
                  WHERE RowId = @id AND Status = 0",
                new SqlParameter("@e", employeeId),
                new SqlParameter("@id", bookingId)).ConfigureAwait(false);

            return n > 0 ? (true, null) : (false, "Запись не найдена или уже обработана.");
        }

        public static Task<WorkshopMapDetail> LoadWorkshopDetailAsync(Guid workshopId) =>
            WithDb(db => LoadWorkshopDetailAsync(db, workshopId));

        public static async Task<WorkshopMapDetail> LoadWorkshopDetailAsync(DriveCareDBEntities db, Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return null;

            var hasCoord = await ColumnExistsAsync(db, "Addresses", "Latitude").ConfigureAwait(false);
            var sql = $@"
SELECT w.RowId AS WorkshopId, w.Name AS WorkshopName, co.Name AS CompanyName, w.Description,
       w.BusinessTypeId, bt.Name AS ServiceKindName,
       COALESCE(NULLIF(LTRIM(RTRIM(a.FullAddress)), N''),
         NULLIF(LTRIM(RTRIM(
           COALESCE(a.City, N'') +
           CASE WHEN a.Street IS NOT NULL AND LTRIM(RTRIM(a.Street)) <> N'' THEN N', ' + a.Street ELSE N'' END +
           CASE WHEN a.House IS NOT NULL AND LTRIM(RTRIM(a.House)) <> N'' THEN N' ' + a.House ELSE N'' END
         )), N'')) AS AddressLine,
       (SELECT TOP 1 e.Phone FROM dbo.Employees e
        WHERE e.WorkshopId = w.RowId AND e.IsActive = 1 AND e.Phone IS NOT NULL AND LTRIM(RTRIM(e.Phone)) <> N''
        ORDER BY e.HireDate) AS Phone" +
(hasCoord ? ", a.Latitude, a.Longitude" : ", CAST(NULL AS FLOAT) AS Latitude, CAST(NULL AS FLOAT) AS Longitude") + @"
FROM dbo.Workshops w
INNER JOIN dbo.Companies co ON co.RowId = w.CompanyId
LEFT JOIN dbo.BusinessTypes bt ON bt.RowId = w.BusinessTypeId
LEFT JOIN dbo.Addresses a ON a.RowId = w.AddressId
WHERE w.RowId = @w;";

            return await db.Database.SqlQuery<WorkshopMapDetail>(sql, new SqlParameter("@w", workshopId))
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }

        static async Task<bool> ColumnExistsAsync(DriveCareDBEntities db, string table, string column)
        {
            var sql = "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NOT NULL THEN 1 ELSE 0 END";
            return await db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@t", "dbo." + table),
                    new SqlParameter("@c", column))
                .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
        }

        static async Task<T> WithDb<T>(Func<DriveCareDBEntities, Task<T>> work)
        {
            using (var db = new DriveCareDBEntities())
                return await work(db).ConfigureAwait(false);
        }
    }
}
