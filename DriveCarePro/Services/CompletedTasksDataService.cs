using DriveCareCore.Data.BD;
using DriveCarePro.Services.ServiceBooking;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services
{
    public sealed class CompletedTaskRowVm
    {
        public Guid TaskId { get; set; }
        public string Title { get; set; }
        public string Vin { get; set; }
        public string PlateNumber { get; set; }
        public string CarDisplay { get; set; }
        public string ClientDisplay { get; set; }
        public string EmployeeName { get; set; }
        public string CompletedDisplay { get; set; }
    }

    internal static class CompletedTasksDataService
    {
        private sealed class TaskRepairVinRow
        {
            public Guid TaskId { get; set; }
            public Guid? RepairHistoryId { get; set; }
            public string GuestVin { get; set; }
            public string GuestPlate { get; set; }
        }

        private sealed class TaskExtraRow
        {
            public Guid TaskId { get; set; }
            public string ClientName { get; set; }
        }

        public static async Task<List<Guid>> GetScopeEmployeeIdsAsync(Guid currentEmployeeId)
        {
            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                var emp = await db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.RowId == currentEmployeeId)
                    .ConfigureAwait(false);
                if (emp == null)
                    return new List<Guid>();

                if (!emp.WorkshopId.HasValue)
                    return new List<Guid> { currentEmployeeId };

                var companyId = await db.Workshops.AsNoTracking()
                    .Where(w => w.RowId == emp.WorkshopId.Value)
                    .Select(w => w.CompanyId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (companyId == Guid.Empty)
                    return new List<Guid> { currentEmployeeId };

                var workshopIds = await db.Workshops.AsNoTracking()
                    .Where(w => w.CompanyId == companyId)
                    .Select(w => w.RowId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                var ids = await db.Employees.AsNoTracking()
                    .Where(e => e.WorkshopId.HasValue && workshopIds.Contains(e.WorkshopId.Value))
                    .Select(e => e.RowId)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return ids.Count > 0 ? ids : new List<Guid> { currentEmployeeId };
            }).ConfigureAwait(false);
        }

        public static async Task<List<CompletedTaskRowVm>> LoadCompletedAsync(Guid currentEmployeeId, string searchQuery)
        {
            try
            {
                var employeeIds = await GetScopeEmployeeIdsAsync(currentEmployeeId).ConfigureAwait(false);
                if (employeeIds.Count == 0)
                    return new List<CompletedTaskRowVm>();

                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var tasks = await db.Tasks.AsNoTracking()
                        .Where(t => t.IsCompleted && employeeIds.Contains(t.EmployeeId))
                        .OrderByDescending(t => t.EndDate ?? t.CreatedAt)
                        .Take(400)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (tasks.Count == 0)
                        return new List<CompletedTaskRowVm>();

                    var taskIds = tasks.Select(t => t.RowId).ToList();
                    var repairVinMap = await LoadRepairVinMapAsync(db, taskIds).ConfigureAwait(false);

                    var carIds = tasks.Where(t => t.CarId.HasValue).Select(t => t.CarId.Value).Distinct().ToList();
                    var cars = carIds.Count == 0
                        ? new Dictionary<Guid, Car>()
                        : (await db.Cars.AsNoTracking()
                            .Where(c => carIds.Contains(c.RowId))
                            .ToListAsync()
                            .ConfigureAwait(false))
                            .ToDictionary(c => c.RowId);

                    var employeeMap = await db.Employees.AsNoTracking()
                        .Where(e => employeeIds.Contains(e.RowId))
                        .ToDictionaryAsync(e => e.RowId, e => AppState.FormatEmployeeDisplayName(e))
                        .ConfigureAwait(false);

                    var clientNameMap = await LoadClientNamesBatchAsync(db, taskIds).ConfigureAwait(false);

                    var rows = new List<CompletedTaskRowVm>();
                    foreach (var t in tasks)
                    {
                        repairVinMap.TryGetValue(t.RowId, out var rv);
                        Car car = null;
                        if (t.CarId.HasValue)
                            cars.TryGetValue(t.CarId.Value, out car);

                        var vin = !string.IsNullOrWhiteSpace(rv?.GuestVin)
                            ? rv.GuestVin.Trim()
                            : CarDisplayHelper.ExtractVin(car?.Description);
                        var plate = !string.IsNullOrWhiteSpace(rv?.GuestPlate)
                            ? rv.GuestPlate.Trim()
                            : CarDisplayHelper.ExtractPlate(car?.Description);

                        clientNameMap.TryGetValue(t.RowId, out var clientName);
                        var client = BuildClientDisplay(t.ClientUserId, clientName, db);

                        rows.Add(new CompletedTaskRowVm
                        {
                            TaskId = t.RowId,
                            Title = string.IsNullOrWhiteSpace(t.Title) ? "—" : t.Title.Trim(),
                            Vin = string.IsNullOrWhiteSpace(vin) ? "—" : vin,
                            PlateNumber = string.IsNullOrWhiteSpace(plate) ? "—" : plate,
                            CarDisplay = CarDisplayHelper.FormatCar(db, t.CarId),
                            ClientDisplay = client,
                            EmployeeName = employeeMap.TryGetValue(t.EmployeeId, out var en) ? en : "—",
                            CompletedDisplay = (t.EndDate ?? t.CreatedAt).ToString("dd.MM.yyyy HH:mm")
                        });
                    }

                    return FilterRows(rows, searchQuery);
                }).ConfigureAwait(false);
            }
            catch
            {
                return new List<CompletedTaskRowVm>();
            }
        }

        public static async Task<bool> CanViewTaskAsync(Guid taskId, Guid currentEmployeeId)
        {
            var scope = await GetScopeEmployeeIdsAsync(currentEmployeeId).ConfigureAwait(false);
            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                var task = await db.Tasks.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.RowId == taskId)
                    .ConfigureAwait(false);
                return task != null && task.IsCompleted && scope.Contains(task.EmployeeId);
            }).ConfigureAwait(false);
        }

        private static async Task<Dictionary<Guid, TaskRepairVinRow>> LoadRepairVinMapAsync(
            DriveCareDBEntities db,
            List<Guid> taskIds)
        {
            var map = new Dictionary<Guid, TaskRepairVinRow>();
            try
            {
                foreach (var batch in taskIds.Take(400).Select((id, i) => new { id, i }).GroupBy(x => x.i / 50).Select(g => g.Select(x => x.id).ToList()))
                {
                    var idList = string.Join(",", batch.Select(id => $"'{id}'"));
                    var sql = $@"
SELECT t.RowId AS TaskId, t.RepairHistoryId,
       LTRIM(RTRIM(ISNULL(gc.Vin, ''))) AS GuestVin,
       LTRIM(RTRIM(ISNULL(gc.PlateNumber, ''))) AS GuestPlate
FROM Tasks t
LEFT JOIN WorkshopGuestCars gc ON gc.RepairHistoryId = t.RepairHistoryId
WHERE t.RowId IN ({idList})";

                    var chunk = await db.Database.SqlQuery<TaskRepairVinRow>(sql).ToListAsync().ConfigureAwait(false);
                    foreach (var row in chunk)
                        map[row.TaskId] = row;
                }
            }
            catch (SqlException)
            {
                foreach (var taskId in taskIds)
                {
                    var extra = await ServiceBookingTaskService.TryLoadExtraAsync(db, taskId).ConfigureAwait(false);
                    map[taskId] = new TaskRepairVinRow { TaskId = taskId, RepairHistoryId = extra?.RepairHistoryId };
                }
            }

            return map;
        }

        private static async Task<Dictionary<Guid, string>> LoadClientNamesBatchAsync(
            DriveCareDBEntities db,
            List<Guid> taskIds)
        {
            var map = new Dictionary<Guid, string>();
            try
            {
                foreach (var batch in taskIds.Select((id, i) => new { id, i }).GroupBy(x => x.i / 50).Select(g => g.Select(x => x.id).ToList()))
                {
                    var idList = string.Join(",", batch.Select(id => $"'{id}'"));
                    var sql = $"SELECT RowId AS TaskId, ClientName FROM Tasks WHERE RowId IN ({idList})";
                    var rows = await db.Database.SqlQuery<TaskExtraRow>(sql).ToListAsync().ConfigureAwait(false);
                    foreach (var row in rows)
                    {
                        if (!string.IsNullOrWhiteSpace(row.ClientName))
                            map[row.TaskId] = row.ClientName.Trim();
                    }
                }
            }
            catch (SqlException)
            {
                // extended columns missing
            }

            return map;
        }

        private static string BuildClientDisplay(Guid? userId, string clientName, DriveCareDBEntities db)
        {
            if (!string.IsNullOrWhiteSpace(clientName))
                return clientName.Trim();

            if (userId.HasValue)
            {
                var u = db.Users.AsNoTracking().FirstOrDefault(x => x.RowId == userId.Value);
                if (u != null && !string.IsNullOrWhiteSpace(u.Login))
                    return u.Login.Trim();
            }

            return "—";
        }

        private static List<CompletedTaskRowVm> FilterRows(List<CompletedTaskRowVm> rows, string searchQuery)
        {
            var q = (searchQuery ?? string.Empty).Trim();
            if (q.Length == 0)
                return rows;

            q = q.ToUpperInvariant();
            return rows.Where(r =>
                    (r.Vin ?? string.Empty).ToUpperInvariant().Contains(q) ||
                    (r.PlateNumber ?? string.Empty).ToUpperInvariant().Contains(q) ||
                    (r.Title ?? string.Empty).ToUpperInvariant().Contains(q) ||
                    (r.CarDisplay ?? string.Empty).ToUpperInvariant().Contains(q) ||
                    (r.ClientDisplay ?? string.Empty).ToUpperInvariant().Contains(q) ||
                    (r.EmployeeName ?? string.Empty).ToUpperInvariant().Contains(q))
                .ToList();
        }
    }
}
