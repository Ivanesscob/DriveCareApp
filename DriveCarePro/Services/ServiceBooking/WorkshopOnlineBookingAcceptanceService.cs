using DriveCareCore.Bookings;
using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskEntity = DriveCareCore.Data.BD.Task;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.ServiceBooking
{
    /// <summary>Принятие онлайн-записи: подтверждение, создание задания, отметка «клиент не явился».</summary>
    internal static class WorkshopOnlineBookingAcceptanceService
    {
        public static Task<BookingActionResult> AcceptAndCreateTaskAsync(
            Guid bookingId,
            Guid employeeId,
            string visitWhenText) =>
            DatabaseExecutor.WithDbAsync(db => AcceptAndCreateTaskCoreAsync(db, bookingId, employeeId, visitWhenText));

        public static Task<BookingActionResult> MarkClientNoShowAsync(Guid bookingId, Guid employeeId) =>
            DatabaseExecutor.WithDbAsync(db => MarkClientNoShowCoreAsync(db, bookingId, employeeId, null));

        public static Task<BookingActionResult> MarkClientNoShowByTaskAsync(Guid taskId, Guid employeeId) =>
            DatabaseExecutor.WithDbAsync(db => MarkClientNoShowByTaskCoreAsync(db, taskId, employeeId));

        public static Task<Guid?> TryFindConfirmedBookingIdByTaskAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => TryFindConfirmedBookingIdByTaskCoreAsync(db, taskId));

        static async Task<BookingActionResult> MarkClientNoShowByTaskCoreAsync(
            DriveCareDBEntities db,
            Guid taskId,
            Guid employeeId)
        {
            if (taskId == Guid.Empty)
                return BookingActionResult.Fail("Не указано задание.");

            var bookingId = await TryFindConfirmedBookingIdByTaskCoreAsync(db, taskId).ConfigureAwait(false);
            if (!bookingId.HasValue || bookingId.Value == Guid.Empty)
                return BookingActionResult.Fail("Задание не связано с подтверждённой онлайн-записью.");

            return await MarkClientNoShowCoreAsync(db, bookingId.Value, employeeId, taskId).ConfigureAwait(false);
        }

        static async Task<Guid?> TryFindConfirmedBookingIdByTaskCoreAsync(DriveCareDBEntities db, Guid taskId)
        {
            if (!WorkshopOnlineBookingService.TablesExist())
                return null;

            var hasTaskCol = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "TaskId").ConfigureAwait(false);
            if (hasTaskCol)
            {
                var byTask = await db.Database.SqlQuery<Guid?>(
                    "SELECT TOP 1 RowId FROM dbo.WorkshopOnlineBookings WHERE TaskId = @p0 AND Status = @p1",
                    taskId,
                    (byte)WorkshopBookingStatus.Confirmed).FirstOrDefaultAsync().ConfigureAwait(false);
                if (byTask.HasValue && byTask.Value != Guid.Empty)
                    return byTask;
            }

            var hasRepairCol = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "RepairHistoryId").ConfigureAwait(false);
            if (!hasRepairCol)
                return null;

            var repairId = await db.Database.SqlQuery<Guid?>(
                "SELECT RepairHistoryId FROM dbo.Tasks WHERE RowId = @p0",
                taskId).FirstOrDefaultAsync().ConfigureAwait(false);
            if (!repairId.HasValue || repairId.Value == Guid.Empty)
                return null;

            return await db.Database.SqlQuery<Guid?>(
                "SELECT TOP 1 RowId FROM dbo.WorkshopOnlineBookings WHERE RepairHistoryId = @p0 AND Status = @p1",
                repairId.Value,
                (byte)WorkshopBookingStatus.Confirmed).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        static async Task<BookingActionResult> AcceptAndCreateTaskCoreAsync(
            DriveCareDBEntities db,
            Guid bookingId,
            Guid employeeId,
            string visitWhenText)
        {
            var accept = await WorkshopOnlineBookingService.AcceptBookingAsync(db, bookingId, employeeId, visitWhenText)
                .ConfigureAwait(false);
            if (!accept.Ok)
                return accept;

            var source = await LoadBookingSourceAsync(db, bookingId).ConfigureAwait(false);
            if (source == null)
                return BookingActionResult.Fail("Запись принята, но данные заявки не найдены для создания задания.");

            if (source.CarId == Guid.Empty)
                return BookingActionResult.Fail("Запись принята, но не указан автомобиль клиента — задание не создано.");

            var ctx = BuildContext(source, visitWhenText);
            var repairId = await CreateRepairHistoryAsync(db, ctx, source.CarId, employeeId).ConfigureAwait(false);
            var taskId = await ServiceBookingTaskService.CreateForBookingAsync(
                db, ctx, source.CarId, repairId, source.WorkshopId).ConfigureAwait(false);

            if (!taskId.HasValue || taskId.Value == Guid.Empty)
                return BookingActionResult.Fail("Запись принята, но не удалось создать задание. Проверьте справочник статусов Tasks.");

            await LinkBookingToTaskAsync(db, bookingId, taskId.Value, repairId).ConfigureAwait(false);

            return BookingActionResult.Success(accept.ChatWarning, taskId);
        }

        static async Task<BookingActionResult> MarkClientNoShowCoreAsync(
            DriveCareDBEntities db,
            Guid bookingId,
            Guid employeeId,
            Guid? explicitTaskId)
        {
            if (!WorkshopOnlineBookingService.TablesExist())
                return BookingActionResult.Fail("Таблица записей не найдена.");

            var source = await LoadBookingSourceAsync(db, bookingId).ConfigureAwait(false);
            if (source == null)
                return BookingActionResult.Fail("Запись не найдена.");
            if (source.Status != (byte)WorkshopBookingStatus.Confirmed)
                return BookingActionResult.Fail("Отметить неявку можно только для подтверждённой записи.");
            if (!source.TaskId.HasValue || source.TaskId.Value == Guid.Empty)
            {
                var repairId = source.RepairHistoryId;
                if (repairId.HasValue && repairId.Value != Guid.Empty)
                {
                    var byRepair = await db.Database.SqlQuery<Guid?>(
                        "SELECT TOP 1 RowId FROM dbo.Tasks WHERE RepairHistoryId = @p0",
                        repairId.Value).FirstOrDefaultAsync().ConfigureAwait(false);
                    if (byRepair.HasValue && byRepair.Value != Guid.Empty)
                        source.TaskId = byRepair;
                }
            }

            if (!source.TaskId.HasValue || source.TaskId.Value == Guid.Empty)
            {
                var orphan = await TryFindOrphanTaskIdAsync(db, source).ConfigureAwait(false);
                if (orphan.HasValue)
                    source.TaskId = orphan;
            }

            if (explicitTaskId.HasValue && explicitTaskId.Value != Guid.Empty)
                source.TaskId = explicitTaskId;

            var taskId = source.TaskId;

            var n = await db.Database.ExecuteSqlCommandAsync(
                @"UPDATE dbo.WorkshopOnlineBookings
                  SET Status = @p0, TaskId = NULL, RepairHistoryId = NULL
                  WHERE RowId = @p1 AND Status = 1",
                (byte)WorkshopBookingStatus.ClientNoShow,
                bookingId).ConfigureAwait(false);

            if (n == 0)
                return BookingActionResult.Fail("Не удалось обновить статус записи.");

            if (taskId.HasValue && taskId.Value != Guid.Empty)
            {
                try
                {
                    await DeleteTaskAsync(db, taskId.Value).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return BookingActionResult.Fail(
                        "Запись отмечена как неявка, но не удалось удалить задание: " + ex.Message);
                }
            }

            var car = string.IsNullOrWhiteSpace(source.CarDisplayName) ? "автомобиль" : source.CarDisplayName.Trim();
            var chatBody = "Клиент не явился на запись.\n\nАвтомобиль: " + car
                           + ".\nЗадание в мастерской снято.";
            var chatWarning = await TryNotifyUserAsync(db, source.WorkshopId, source.UserId, employeeId, chatBody)
                .ConfigureAwait(false);

            return BookingActionResult.Success(chatWarning);
        }

        static ServiceBookingContext BuildContext(OnlineBookingTaskSource source, string visitWhenText)
        {
            var issue = string.IsNullOrWhiteSpace(source.IssueCategory)
                ? "обращение"
                : WorkshopBookingIssueCategories.GetDisplayName(source.IssueCategory);

            var visit = (visitWhenText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(visit) && source.PreferredDate.HasValue)
                visit = source.PreferredDate.Value.ToString("dd.MM.yyyy") + " в 10:00";

            var reason = new StringBuilder();
            reason.Append(issue);
            if (!string.IsNullOrWhiteSpace(source.ClientComment))
                reason.Append(". ").Append(source.ClientComment.Trim());

            var notes = new StringBuilder();
            notes.Append("Онлайн-запись. Ожидаемый визит: ").Append(string.IsNullOrEmpty(visit) ? "—" : visit);
            if (source.PreferredDate.HasValue)
                notes.Append(". Дата из заявки: ").Append(source.PreferredDate.Value.ToString("dd.MM.yyyy"));

            var ctx = ServiceBookingContext.Create(ServiceBookingKind.Repair);
            ctx.WorkshopId = source.WorkshopId;
            ctx.ClientPath = ServiceClientPath.ExistingUserWithSelectedCar;
            ctx.SelectedCarId = source.CarId;
            ctx.SelectedUserCarId = source.UserCarId;
            ctx.ClientFullName = !string.IsNullOrWhiteSpace(source.UserLogin)
                ? source.UserLogin.Trim()
                : (source.UserEmail ?? "Клиент").Trim();
            ctx.ClientPhone = !string.IsNullOrWhiteSpace(source.ClientPhone)
                ? source.ClientPhone.Trim()
                : (source.UserPhone ?? string.Empty).Trim();
            ctx.ClientEmail = source.UserEmail ?? string.Empty;
            ctx.CarDescription = source.CarDisplayName ?? string.Empty;
            ctx.Vin = source.Vin ?? string.Empty;
            ctx.PlateNumber = source.PlateNumber ?? string.Empty;
            ctx.Year = source.Year.HasValue ? source.Year.Value.ToString() : string.Empty;
            ctx.VisitReason = reason.ToString();
            ctx.SpecialNotes = notes.ToString();
            ctx.FoundUser = new User
            {
                RowId = source.UserId,
                Login = source.UserLogin,
                Email = source.UserEmail,
                Phone = source.UserPhone
            };
            return ctx;
        }

        static async Task<Guid> CreateRepairHistoryAsync(
            DriveCareDBEntities db,
            ServiceBookingContext ctx,
            Guid carId,
            Guid employeeId)
        {
            var statusId = await db.Statuses.Select(s => s.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
            var categoryId = await db.RepairCategories.Select(c => c.RowId).FirstOrDefaultAsync().ConfigureAwait(false);

            var repair = new RepairHistory
            {
                RowId = Guid.NewGuid(),
                CarId = carId,
                EmployeeId = employeeId,
                Title = ctx.RepairTypeDisplay + " (онлайн-запись)",
                Description = ctx.VisitReason + (string.IsNullOrWhiteSpace(ctx.SpecialNotes)
                    ? string.Empty
                    : Environment.NewLine + Environment.NewLine + ctx.SpecialNotes),
                RepairDate = DateTime.Now,
                StatusId = statusId == Guid.Empty ? (Guid?)null : statusId,
                CategoryId = categoryId == Guid.Empty ? (Guid?)null : categoryId,
                CreatedAt = DateTime.Now
            };

            db.RepairHistories.Add(repair);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return repair.RowId;
        }

        static async Task LinkBookingToTaskAsync(DriveCareDBEntities db, Guid bookingId, Guid taskId, Guid repairId)
        {
            var hasTask = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "TaskId").ConfigureAwait(false);
            if (!hasTask)
                return;

            var hasRepair = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "RepairHistoryId").ConfigureAwait(false);
            if (hasRepair)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE dbo.WorkshopOnlineBookings SET TaskId = @p0, RepairHistoryId = @p1 WHERE RowId = @p2",
                    taskId, repairId, bookingId).ConfigureAwait(false);
            }
            else
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE dbo.WorkshopOnlineBookings SET TaskId = @p0 WHERE RowId = @p1",
                    taskId, bookingId).ConfigureAwait(false);
            }
        }

        static async Task DeleteTaskAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM TaskServiceLines WHERE TaskId = @p0", taskId).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM TaskPartLines WHERE TaskId = @p0", taskId).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM TaskPurchaseRequests WHERE SourceTaskId = @p0 OR PurchaseTaskId = @p0",
                    taskId).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM EmployeeNotifications WHERE TaskId = @p0", taskId).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM ServiceDocuments WHERE RootTaskId = @p0", taskId).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208) { }

            var task = await db.Tasks.FirstOrDefaultAsync(t => t.RowId == taskId).ConfigureAwait(false);
            if (task != null)
            {
                db.Tasks.Remove(task);
                await db.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM dbo.Tasks WHERE RowId = @p0", taskId).ConfigureAwait(false);
            }
        }

        static async Task<OnlineBookingTaskSource> LoadBookingSourceAsync(DriveCareDBEntities db, Guid bookingId)
        {
            var hasIssue = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "IssueCategory").ConfigureAwait(false);
            var hasTask = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "TaskId").ConfigureAwait(false);
            var hasRepair = await ColumnExistsAsync(db, "WorkshopOnlineBookings", "RepairHistoryId").ConfigureAwait(false);
            var hasVin = await ColumnExistsAsync(db, "Cars", "Vin").ConfigureAwait(false);

            var issueSel = hasIssue ? "b.IssueCategory AS IssueCategory" : "CAST(NULL AS NVARCHAR(120)) AS IssueCategory";
            var taskSel = hasTask ? "b.TaskId AS TaskId" : "CAST(NULL AS UNIQUEIDENTIFIER) AS TaskId";
            var repairSel = hasRepair ? "b.RepairHistoryId AS RepairHistoryId" : "CAST(NULL AS UNIQUEIDENTIFIER) AS RepairHistoryId";
            var vinSel = hasVin ? ", c.Vin, c.PlateNumber" : ", CAST(NULL AS NVARCHAR(50)) AS Vin, CAST(NULL AS NVARCHAR(20)) AS PlateNumber";

            var sql = $@"
SELECT b.RowId AS BookingId, b.WorkshopId, b.UserId, b.UserCarId, b.Status,
       b.ClientPhone, b.ClientComment, b.PreferredDate, b.ConfirmedAt, {issueSel}, {taskSel}, {repairSel},
       uc.CarId,
       u.Login AS UserLogin, u.Email AS UserEmail, u.Phone AS UserPhone,
       c.Year{vinSel},
       COALESCE(
         NULLIF(LTRIM(RTRIM(ISNULL(br.Name, N'') + N' ' + ISNULL(m.Name, N''))), N''),
         N'Автомобиль'
       ) AS CarDisplayName
FROM dbo.WorkshopOnlineBookings b
INNER JOIN dbo.Users u ON u.RowId = b.UserId
LEFT JOIN dbo.UserCars uc ON uc.RowId = b.UserCarId
LEFT JOIN dbo.Cars c ON c.RowId = uc.CarId
LEFT JOIN dbo.Models m ON m.RowId = c.ModelId
LEFT JOIN dbo.Brands br ON br.RowId = m.BrandId
WHERE b.RowId = @p0;";

            return await db.Database.SqlQuery<OnlineBookingTaskSource>(sql, bookingId)
                .FirstOrDefaultAsync().ConfigureAwait(false);
        }

        static async Task<string> TryNotifyUserAsync(
            DriveCareDBEntities db,
            Guid workshopId,
            Guid userId,
            Guid employeeId,
            string message)
        {
            if (!DriveCareCore.Messaging.WorkshopMessagingService.TablesExist())
                return "Чат не настроен — клиент не получил уведомление.";

            var chatStart = await DriveCareCore.Messaging.WorkshopMessagingService.StartConversationAsync(
                db, workshopId, userId, employeeId, message).ConfigureAwait(false);

            if (!chatStart.ok || !chatStart.conversationId.HasValue)
                return chatStart.error ?? "Не удалось отправить сообщение в чат.";

            DriveCareCore.Messaging.WorkshopChatRealtimeClient.NotifyNewMessage(
                chatStart.conversationId.Value, workshopId, userId, DriveCareCore.Messaging.MessageSenderKind.Employee);
            return null;
        }

        static async Task<Guid?> TryFindOrphanTaskIdAsync(DriveCareDBEntities db, OnlineBookingTaskSource source)
        {
            if (source.CarId == Guid.Empty || source.UserId == Guid.Empty)
                return null;

            var from = source.ConfirmedAt?.AddMinutes(-5) ?? DateTime.Now.AddDays(-1);
            var to = source.ConfirmedAt?.AddMinutes(30) ?? DateTime.Now;

            try
            {
                return await db.Database.SqlQuery<Guid?>(
                    @"SELECT TOP 1 t.RowId
                      FROM dbo.Tasks t
                      WHERE t.CarId = @p0
                        AND t.ClientUserId = @p1
                        AND t.IsCompleted = 0
                        AND t.CreatedAt >= @p2 AND t.CreatedAt <= @p3
                      ORDER BY t.CreatedAt DESC",
                    source.CarId,
                    source.UserId,
                    from,
                    to).FirstOrDefaultAsync().ConfigureAwait(false);
            }
            catch (SqlException)
            {
                return null;
            }
        }

        static async Task<bool> ColumnExistsAsync(DriveCareDBEntities db, string table, string column)
        {
            var sql = "SELECT CASE WHEN COL_LENGTH(@t, @c) IS NOT NULL THEN 1 ELSE 0 END";
            return await db.Database.SqlQuery<int>(sql,
                    new SqlParameter("@t", "dbo." + table),
                    new SqlParameter("@c", column))
                .FirstOrDefaultAsync().ConfigureAwait(false) == 1;
        }

        sealed class OnlineBookingTaskSource
        {
            public Guid BookingId { get; set; }
            public Guid WorkshopId { get; set; }
            public Guid UserId { get; set; }
            public Guid? UserCarId { get; set; }
            public Guid CarId { get; set; }
            public byte Status { get; set; }
            public string ClientPhone { get; set; }
            public string ClientComment { get; set; }
            public string IssueCategory { get; set; }
            public DateTime? PreferredDate { get; set; }
            public DateTime? ConfirmedAt { get; set; }
            public Guid? TaskId { get; set; }
            public Guid? RepairHistoryId { get; set; }
            public string UserLogin { get; set; }
            public string UserEmail { get; set; }
            public string UserPhone { get; set; }
            public string CarDisplayName { get; set; }
            public string Vin { get; set; }
            public string PlateNumber { get; set; }
            public int? Year { get; set; }
        }
    }
}
