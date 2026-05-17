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
    internal static class ProHomeDataService
    {
        public sealed class AdminStatsVm
        {
            public string Users { get; set; }
            public string Employees { get; set; }
            public string CarSales { get; set; }
            public string Parts { get; set; }
            public string Companies { get; set; }
            public string Cars { get; set; }
        }

        public sealed class EmployeeTaskRowVm
        {
            public Guid TaskId { get; set; }
            public string Title { get; set; }
            public string StatusDisplay { get; set; }
            public string DeadlineDisplay { get; set; }
            public string CreatedDisplay { get; set; }
            public string CompletedDisplay { get; set; }
            public bool IsPartnerDone { get; set; }
        }

        private sealed class OwnerDelegationStatusRow
        {
            public Guid TaskId { get; set; }
            public bool HasDelegate { get; set; }
            public bool PartnerDone { get; set; }
        }

        public static async System.Threading.Tasks.Task<AdminStatsVm> LoadAdminStatsAsync()
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db => new AdminStatsVm
                {
                    Users = (await db.Users.CountAsync().ConfigureAwait(false)).ToString("N0"),
                    Employees = (await db.Employees.CountAsync().ConfigureAwait(false)).ToString("N0"),
                    CarSales = (await db.CarSales.CountAsync().ConfigureAwait(false)).ToString("N0"),
                    Parts = (await db.Parts.CountAsync().ConfigureAwait(false)).ToString("N0"),
                    Companies = (await db.Companies.CountAsync().ConfigureAwait(false)).ToString("N0"),
                    Cars = (await db.Cars.CountAsync().ConfigureAwait(false)).ToString("N0")
                }).ConfigureAwait(false);
            }
            catch
            {
                return new AdminStatsVm
                {
                    Users = "—",
                    Employees = "—",
                    CarSales = "—",
                    Parts = "—",
                    Companies = "—",
                    Cars = "—"
                };
            }
        }

        public static async System.Threading.Tasks.Task<List<EmployeeTaskRowVm>> LoadTasksAsync(Guid employeeId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var statuses = await db.Statuses.ToListAsync().ConfigureAwait(false);
                    var statusLookup = statuses.ToDictionary(
                        s => s.RowId,
                        s => (s.Name ?? string.Empty).Trim());

                    var tasks = await db.Tasks
                        .Where(t => t.EmployeeId == employeeId && !t.IsCompleted)
                        .OrderByDescending(t => t.CreatedAt)
                        .Take(150)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    var delegationMap = await LoadDelegationStatusMapAsync(db, employeeId).ConfigureAwait(false);
                    var purchaseMap = await TaskPurchaseRequestService.LoadPurchaseStatusMapAsync(employeeId).ConfigureAwait(false);

                    return tasks.Select(t =>
                    {
                        var baseStatus = statusLookup.TryGetValue(t.StatusId, out var sn) ? sn : "—";
                        var row = new EmployeeTaskRowVm
                        {
                            TaskId = t.RowId,
                            Title = string.IsNullOrWhiteSpace(t.Title) ? "—" : t.Title.Trim(),
                            StatusDisplay = baseStatus,
                            DeadlineDisplay = t.Deadline.HasValue ? t.Deadline.Value.ToString("dd.MM.yyyy HH:mm") : "—",
                            CreatedDisplay = t.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                            CompletedDisplay = t.IsCompleted ? "Да" : "Нет"
                        };

                        if (purchaseMap.TryGetValue(t.RowId, out var pur))
                        {
                            if (pur.PurchaserDone)
                            {
                                row.IsPartnerDone = true;
                                row.StatusDisplay = "Закупка выполнена — проверьте отчёт";
                            }
                            else if (pur.HasPurchase)
                                row.StatusDisplay = "Закупка у сотрудника";
                        }
                        else if (delegationMap.TryGetValue(t.RowId, out var del))
                        {
                            if (del.PartnerDone)
                            {
                                row.IsPartnerDone = true;
                                row.StatusDisplay = "Исполнитель завершил — завершите своё";
                            }
                            else if (del.HasDelegate)
                                row.StatusDisplay = "Передано сотруднику";
                        }

                        return row;
                    }).ToList();
                }).ConfigureAwait(false);
            }
            catch
            {
                return new List<EmployeeTaskRowVm>();
            }
        }

        private static async System.Threading.Tasks.Task<Dictionary<Guid, OwnerDelegationStatusRow>> LoadDelegationStatusMapAsync(
            DriveCareDBEntities db,
            Guid employeeId)
        {
            var map = new Dictionary<Guid, OwnerDelegationStatusRow>();
            try
            {
                var rows = await db.Database.SqlQuery<OwnerDelegationStatusRow>(
                    @"SELECT p.RowId AS TaskId,
                             CAST(CASE WHEN p.DelegateTaskId IS NOT NULL THEN 1 ELSE 0 END AS bit) AS HasDelegate,
                             CAST(CASE WHEN c.IsCompleted = 1 THEN 1 ELSE 0 END AS bit) AS PartnerDone
                      FROM Tasks p
                      LEFT JOIN Tasks c ON c.RowId = p.DelegateTaskId
                      WHERE p.EmployeeId = @p0 AND p.IsCompleted = 0",
                    employeeId).ToListAsync().ConfigureAwait(false);

                foreach (var row in rows)
                    map[row.TaskId] = row;
            }
            catch (SqlException)
            {
            }

            return map;
        }
    }
}
