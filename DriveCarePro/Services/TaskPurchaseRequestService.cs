using DriveCareCore.Data.BD;
using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Services.WorkshopServices;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskEntity = DriveCareCore.Data.BD.Task;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services
{
    public sealed class TaskPurchaseRequestInfo
    {
        public Guid RequestId { get; set; }
        public Guid SourceTaskId { get; set; }
        public Guid PurchaseTaskId { get; set; }
        public bool IsFulfilled { get; set; }
        public string RequesterName { get; set; }
        public string PurchaserName { get; set; }
        public List<TaskPartLineRow> Lines { get; set; } = new List<TaskPartLineRow>();
    }

    public sealed class TaskPurchaseStatusInfo
    {
        public bool HasOpenRequest { get; set; }
        public bool PurchaserCompleted { get; set; }
        public string StatusText { get; set; }
    }

    internal static class TaskPurchaseRequestService
    {
        public const string PurchasePartsPermission = ProPermissions.PurchaseParts;

        public static bool LooksLikePurchaseTask(string title) =>
            !string.IsNullOrWhiteSpace(title) &&
            title.Trim().StartsWith("[Закупка]", StringComparison.OrdinalIgnoreCase);

        public static async Task<List<DelegateEmployeeOption>> ListAuthorizedPurchasersAsync(Guid currentEmployeeId)
        {
            var scopeIds = await CompletedTasksDataService.GetScopeEmployeeIdsAsync(currentEmployeeId).ConfigureAwait(false);
            scopeIds = scopeIds.Where(id => id != currentEmployeeId).Distinct().ToList();
            if (scopeIds.Count == 0)
                return new List<DelegateEmployeeOption>();

            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                var workshops = await db.Workshops.AsNoTracking().ToListAsync().ConfigureAwait(false);
                var wsDict = workshops.ToDictionary(w => w.RowId, w => (w.Name ?? "—").Trim());

                var employees = await db.Employees.AsNoTracking()
                    .Where(e => scopeIds.Contains(e.RowId) && e.IsActive != false)
                    .OrderBy(e => e.LastName)
                    .ThenBy(e => e.FirstName)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var result = new List<DelegateEmployeeOption>();
                foreach (var e in employees)
                {
                    if (!await IsAuthorizedPurchaserAsync(db, e.RowId).ConfigureAwait(false))
                        continue;

                    result.Add(new DelegateEmployeeOption
                    {
                        EmployeeId = e.RowId,
                        DisplayName = AppState.FormatEmployeeDisplayName(e),
                        WorkshopName = e.WorkshopId.HasValue && wsDict.TryGetValue(e.WorkshopId.Value, out var wn) ? wn : "—"
                    });
                }

                return result;
            }).ConfigureAwait(false);
        }

        private static async Task<bool> IsAuthorizedPurchaserAsync(DriveCareDBEntities db, Guid employeeId)
        {
            var roleIds = await db.EmployeeRolesMaps.AsNoTracking()
                .Where(m => m.EmployeeId == employeeId)
                .Select(m => m.RoleId)
                .ToListAsync()
                .ConfigureAwait(false);

            if (roleIds.Count == 0)
                return false;

            var roles = await db.Roles.AsNoTracking()
                .Where(r => roleIds.Contains(r.RowId))
                .ToListAsync()
                .ConfigureAwait(false);

            foreach (var role in roles)
            {
                if (role == null)
                    continue;
                if (AppState.IsOwnerRoleName(role.Name))
                    return true;
                var n = (role.Name ?? string.Empty).Trim().ToLowerInvariant();
                if (n.Contains("закуп") || n.Contains("снаб") || n.Contains("склад"))
                    return true;
            }

            try
            {
                var codes = await (
                        from map in db.RolePermissionsMaps
                        where roleIds.Contains(map.RoleId)
                        join p in db.Permissions on map.PermissionId equals p.RowId
                        where p.Code != null
                        select p.Code.Trim()
                    )
                    .Distinct()
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (codes.Any(c => string.Equals(c, PurchasePartsPermission, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            catch
            {
            }

            return false;
        }

        public static async Task<(bool ok, string error, Guid? purchaseTaskId)> CreatePurchaseRequestAsync(
            Guid sourceTaskId,
            Guid fromEmployeeId,
            Guid purchaserEmployeeId,
            IList<TaskPartLineRow> lines)
        {
            if (lines == null || lines.Count == 0)
                return (false, "Корзина пуста.", null);

            if (fromEmployeeId == purchaserEmployeeId)
                return (false, "Нельзя назначить закупку самому себе.", null);

            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    if (!await IsAuthorizedPurchaserAsync(db, purchaserEmployeeId).ConfigureAwait(false))
                        return (false, "Выбранный сотрудник не уполномочен на закупку.", (Guid?)null);

                    var source = await db.Tasks.FirstOrDefaultAsync(t =>
                        t.RowId == sourceTaskId && t.EmployeeId == fromEmployeeId && !t.IsCompleted).ConfigureAwait(false);

                    if (source == null)
                        return (false, "Задание не найдено или уже завершено.", (Guid?)null);

                    var open = await GetOpenRequestForSourceAsync(db, sourceTaskId).ConfigureAwait(false);
                    if (open != null)
                        return (false, "По этому заданию уже есть открытый запрос на закупку. Дождитесь выполнения.", (Guid?)null);

                    var scope = await CompletedTasksDataService.GetScopeEmployeeIdsAsync(fromEmployeeId).ConfigureAwait(false);
                    if (!scope.Contains(purchaserEmployeeId))
                        return (false, "Сотрудник не из вашей организации.", null);

                    var requester = await db.Employees.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.RowId == fromEmployeeId).ConfigureAwait(false);
                    var requesterName = requester == null ? "коллега" : AppState.FormatEmployeeDisplayName(requester);

                    var statusId = source.StatusId;
                    if (statusId == Guid.Empty)
                    {
                        statusId = await db.Statuses.Select(s => s.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
                        if (statusId == Guid.Empty)
                            return (false, "В справочнике нет статусов заданий.", null);
                    }

                    var purchaseTaskId = Guid.NewGuid();
                    var requestId = Guid.NewGuid();

                    var sb = new StringBuilder();
                    sb.AppendLine("Закупка запчастей по запросу от: " + requesterName);
                    sb.AppendLine("После нажатия «Закупка выполнена» позиции автоматически попадут в отчёт исходного задания.");
                    sb.AppendLine();
                    sb.AppendLine("Список к закупке:");
                    var sort = 0;
                    foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l.PartName)))
                    {
                        line.RecalculateAmount();
                        sb.Append("— ").Append(line.PartName.Trim())
                            .Append(" — ").Append(line.Quantity.ToString("0.###"))
                            .Append(' ').Append(line.UnitName ?? "шт.");
                        if (line.UnitPrice > 0)
                            sb.Append(", ").Append(line.UnitPrice.ToString("N2")).Append(" ₽");
                        sb.AppendLine();
                    }

                    var title = source.Title ?? "Задание";
                    if (!title.StartsWith("[Закупка]", StringComparison.OrdinalIgnoreCase))
                        title = "[Закупка] " + title.Trim();
                    if (title.Length > 250)
                        title = title.Substring(0, 250);

                    var purchaseTask = new TaskEntity
                    {
                        RowId = purchaseTaskId,
                        Title = title,
                        Description = sb.ToString().Trim(),
                        EmployeeId = purchaserEmployeeId,
                        StatusId = statusId,
                        CreatedAt = DateTime.Now,
                        StartDate = DateTime.Now,
                        IsCompleted = false,
                        CarId = source.CarId,
                        ClientUserId = source.ClientUserId
                    };

                    db.Tasks.Add(purchaseTask);
                    await db.SaveChangesAsync().ConfigureAwait(false);

                    await CopyExtendedFieldsAsync(db, sourceTaskId, purchaseTaskId).ConfigureAwait(false);
                    await TrySetParentTaskIdAsync(db, purchaseTaskId, sourceTaskId).ConfigureAwait(false);

                    await db.Database.ExecuteSqlCommandAsync(
                        @"INSERT INTO TaskPurchaseRequests
                          (RowId, SourceTaskId, PurchaseTaskId, RequestedByEmployeeId, PurchaserEmployeeId, IsFulfilled, CreatedAt)
                          VALUES (@p0,@p1,@p2,@p3,@p4,0,@p5)",
                        requestId, sourceTaskId, purchaseTaskId, fromEmployeeId, purchaserEmployeeId, DateTime.Now)
                        .ConfigureAwait(false);

                    sort = 0;
                    foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l.PartName)))
                    {
                        line.RecalculateAmount();
                        await db.Database.ExecuteSqlCommandAsync(
                            @"INSERT INTO TaskPurchaseRequestLines
                              (RowId, RequestId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, SortOrder)
                              VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                            Guid.NewGuid(), requestId,
                            (object)line.WorkshopPartId ?? DBNull.Value,
                            line.PartName.Trim(), line.Quantity,
                            line.UnitName ?? "шт.", line.UnitPrice, sort++).ConfigureAwait(false);
                    }

                    return (true, null, purchaseTaskId);
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return (false, "Таблицы закупок не найдены. Выполните SQL TaskPurchaseRequests_Tables.sql", null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static async Task<TaskPurchaseRequestInfo> TryLoadByPurchaseTaskAsync(Guid purchaseTaskId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var req = await db.Database.SqlQuery<PurchaseRequestRow>(
                        @"SELECT RowId, SourceTaskId, PurchaseTaskId, RequestedByEmployeeId, PurchaserEmployeeId, IsFulfilled
                          FROM TaskPurchaseRequests WHERE PurchaseTaskId = @p0",
                        purchaseTaskId).FirstOrDefaultAsync().ConfigureAwait(false);

                    if (req == null)
                        return null;

                    var lines = await LoadRequestLinesAsync(db, req.RowId).ConfigureAwait(false);

                    var requester = await db.Employees.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.RowId == req.RequestedByEmployeeId).ConfigureAwait(false);
                    var purchaser = await db.Employees.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.RowId == req.PurchaserEmployeeId).ConfigureAwait(false);

                    return new TaskPurchaseRequestInfo
                    {
                        RequestId = req.RowId,
                        SourceTaskId = req.SourceTaskId,
                        PurchaseTaskId = req.PurchaseTaskId,
                        IsFulfilled = req.IsFulfilled,
                        RequesterName = requester == null ? "—" : AppState.FormatEmployeeDisplayName(requester),
                        PurchaserName = purchaser == null ? "—" : AppState.FormatEmployeeDisplayName(purchaser),
                        Lines = lines.Select(l => new TaskPartLineRow
                        {
                            WorkshopPartId = l.WorkshopPartId,
                            PartName = l.PartName,
                            Quantity = l.Quantity,
                            UnitName = l.UnitName ?? "шт.",
                            UnitPrice = l.UnitPrice
                        }).ToList()
                    };
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return null;
            }
        }

        public static async Task<TaskPurchaseStatusInfo> TryLoadStatusForSourceAsync(Guid sourceTaskId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var row = await db.Database.SqlQuery<SourcePurchaseStatusRow>(
                        @"SELECT TOP 1 pr.RowId, pr.IsFulfilled, pt.IsCompleted AS PurchaseCompleted,
                                 pe.FirstName, pe.LastName, pe.MidName, pe.Login
                          FROM TaskPurchaseRequests pr
                          INNER JOIN Tasks pt ON pt.RowId = pr.PurchaseTaskId
                          INNER JOIN Employees pe ON pe.RowId = pr.PurchaserEmployeeId
                          WHERE pr.SourceTaskId = @p0 AND pr.IsFulfilled = 0
                          ORDER BY pr.CreatedAt DESC",
                        sourceTaskId).FirstOrDefaultAsync().ConfigureAwait(false);

                    if (row == null)
                        return new TaskPurchaseStatusInfo();

                    var name = FormatName(row.FirstName, row.LastName, row.MidName, row.Login);
                    var info = new TaskPurchaseStatusInfo { HasOpenRequest = true };

                    if (row.PurchaseCompleted)
                    {
                        info.PurchaserCompleted = true;
                        info.StatusText = name + " завершил(а) закупку — детали добавлены в отчёт.";
                    }
                    else
                        info.StatusText = "Закупка у " + name + " — в работе.";

                    return info;
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new TaskPurchaseStatusInfo();
            }
        }

        public static async Task<(bool ok, string error)> FulfillOnPurchaseCompleteAsync(
            Guid purchaseTaskId,
            Guid purchaserEmployeeId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var req = await db.Database.SqlQuery<PurchaseRequestRow>(
                        @"SELECT RowId, SourceTaskId, PurchaseTaskId, RequestedByEmployeeId, PurchaserEmployeeId, IsFulfilled
                          FROM TaskPurchaseRequests WHERE PurchaseTaskId = @p0",
                        purchaseTaskId).FirstOrDefaultAsync().ConfigureAwait(false);

                    if (req == null)
                        return (false, "Это не задание на закупку.");

                    if (req.PurchaserEmployeeId != purchaserEmployeeId)
                        return (false, "Задание назначено другому сотруднику.");

                    if (req.IsFulfilled)
                        return (true, null);

                    var task = await db.Tasks.FirstOrDefaultAsync(t =>
                        t.RowId == purchaseTaskId && t.EmployeeId == purchaserEmployeeId).ConfigureAwait(false);

                    if (task == null)
                        return (false, "Задание не найдено.");

                    var lines = await LoadRequestLinesAsync(db, req.RowId).ConfigureAwait(false);

                    if (lines.Count == 0)
                        return (false, "В запросе нет позиций для переноса в отчёт. Проверьте таблицу TaskPurchaseRequestLines.");

                    var partRows = lines.Select(l =>
                    {
                        var row = new TaskPartLineRow
                        {
                            WorkshopPartId = l.WorkshopPartId,
                            PartName = l.PartName,
                            Quantity = l.Quantity,
                            UnitName = l.UnitName ?? "шт.",
                            UnitPrice = l.UnitPrice
                        };
                        row.RecalculateAmount();
                        return row;
                    }).ToList();

                    await TaskReportService.AppendPartLinesAsync(req.SourceTaskId, partRows).ConfigureAwait(false);

                    await db.Database.ExecuteSqlCommandAsync(
                        "UPDATE TaskPurchaseRequests SET IsFulfilled = 1 WHERE RowId = @p0", req.RowId)
                        .ConfigureAwait(false);

                    task.IsCompleted = true;
                    task.EndDate = DateTime.Now;
                    task.ReportText = "Закупка выполнена. Детали добавлены в отчёт исходного задания.";

                    await db.SaveChangesAsync().ConfigureAwait(false);
                    return (true, null);
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return (false, "Таблицы закупок не найдены. Выполните SQL TaskPurchaseRequests_Tables.sql");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task<Dictionary<Guid, OwnerPurchaseStatusRow>> LoadPurchaseStatusMapAsync(Guid employeeId)
        {
            var map = new Dictionary<Guid, OwnerPurchaseStatusRow>();
            try
            {
                var rows = await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.SqlQuery<OwnerPurchaseStatusRow>(
                        @"SELECT pr.SourceTaskId AS TaskId,
                                 CAST(1 AS bit) AS HasPurchase,
                                 CAST(CASE WHEN pt.IsCompleted = 1 THEN 1 ELSE 0 END AS bit) AS PurchaserDone
                          FROM TaskPurchaseRequests pr
                          INNER JOIN Tasks pt ON pt.RowId = pr.PurchaseTaskId
                          INNER JOIN Tasks src ON src.RowId = pr.SourceTaskId
                          WHERE src.EmployeeId = @p0 AND src.IsCompleted = 0 AND pr.IsFulfilled = 0",
                        employeeId).ToListAsync()).ConfigureAwait(false);

                foreach (var row in rows)
                    map[row.TaskId] = row;
            }
            catch (SqlException)
            {
            }

            return map;
        }

        private static async Task<List<TaskPurchaseLineRow>> LoadRequestLinesAsync(DriveCareDBEntities db, Guid requestId)
        {
            try
            {
                return await db.Database.SqlQuery<TaskPurchaseLineRow>(
                    @"SELECT WorkshopPartId, PartName, Quantity, UnitName, UnitPrice
                      FROM TaskPurchaseRequestLines WHERE RequestId = @p0 ORDER BY SortOrder",
                    requestId).ToListAsync().ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                return await db.Database.SqlQuery<TaskPurchaseLineRow>(
                    @"SELECT PartName, Quantity, UnitName, UnitPrice
                      FROM TaskPurchaseRequestLines WHERE RequestId = @p0 ORDER BY SortOrder",
                    requestId).ToListAsync().ConfigureAwait(false);
            }
        }

        private static async Task<PurchaseRequestRow> GetOpenRequestForSourceAsync(DriveCareDBEntities db, Guid sourceTaskId)
        {
            try
            {
                return await db.Database.SqlQuery<PurchaseRequestRow>(
                    @"SELECT TOP 1 RowId, SourceTaskId, PurchaseTaskId, RequestedByEmployeeId, PurchaserEmployeeId, IsFulfilled
                      FROM TaskPurchaseRequests
                      WHERE SourceTaskId = @p0 AND IsFulfilled = 0
                      ORDER BY CreatedAt DESC",
                    sourceTaskId).FirstOrDefaultAsync().ConfigureAwait(false);
            }
            catch (SqlException)
            {
                return null;
            }
        }

        private static string FormatName(string first, string last, string mid, string login)
        {
            var parts = new[] { last, first, mid }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
            if (parts.Length > 0)
                return string.Join(" ", parts);
            return string.IsNullOrWhiteSpace(login) ? "сотрудник" : login.Trim();
        }

        private static async Task CopyExtendedFieldsAsync(DriveCareDBEntities db, Guid fromTaskId, Guid toTaskId)
        {
            var extra = await ServiceBookingTaskService.TryLoadExtraAsync(db, fromTaskId).ConfigureAwait(false);
            if (extra == null)
                return;

            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"UPDATE Tasks SET
                        RepairHistoryId = @p1,
                        ClientName = @p2,
                        ClientPhone = @p3,
                        ClientEmail = @p4,
                        VisitReason = @p5,
                        SpecialNotes = @p6,
                        ServiceKind = @p7
                      WHERE RowId = @p0",
                    toTaskId,
                    (object)extra.RepairHistoryId ?? DBNull.Value,
                    (object)extra.ClientName ?? DBNull.Value,
                    (object)extra.ClientPhone ?? DBNull.Value,
                    (object)extra.ClientEmail ?? DBNull.Value,
                    (object)extra.VisitReason ?? DBNull.Value,
                    (object)extra.SpecialNotes ?? DBNull.Value,
                    (object)extra.ServiceKind ?? DBNull.Value).ConfigureAwait(false);
            }
            catch (SqlException)
            {
            }
        }

        private static async Task TrySetParentTaskIdAsync(DriveCareDBEntities db, Guid childId, Guid parentId)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE Tasks SET ParentTaskId = @p1 WHERE RowId = @p0", childId, parentId).ConfigureAwait(false);
            }
            catch (SqlException)
            {
            }
        }

        private sealed class PurchaseRequestRow
        {
            public Guid RowId { get; set; }
            public Guid SourceTaskId { get; set; }
            public Guid PurchaseTaskId { get; set; }
            public Guid RequestedByEmployeeId { get; set; }
            public Guid PurchaserEmployeeId { get; set; }
            public bool IsFulfilled { get; set; }
        }

        private sealed class TaskPurchaseLineRow
        {
            public Guid? WorkshopPartId { get; set; }
            public string PartName { get; set; }
            public decimal Quantity { get; set; }
            public string UnitName { get; set; }
            public decimal UnitPrice { get; set; }
        }

        private sealed class SourcePurchaseStatusRow
        {
            public Guid RowId { get; set; }
            public bool IsFulfilled { get; set; }
            public bool PurchaseCompleted { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string MidName { get; set; }
            public string Login { get; set; }
        }

        public sealed class OwnerPurchaseStatusRow
        {
            public Guid TaskId { get; set; }
            public bool HasPurchase { get; set; }
            public bool PurchaserDone { get; set; }
        }
    }
}
