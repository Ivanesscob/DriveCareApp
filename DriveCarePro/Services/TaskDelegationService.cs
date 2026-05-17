using DriveCareCore.Data.BD;
using DriveCarePro.Services.ServiceBooking;
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
    public sealed class TaskDelegationLinks
    {
        public Guid? ParentTaskId { get; set; }
        public Guid? DelegateTaskId { get; set; }
    }

    public sealed class DelegateEmployeeOption
    {
        public Guid EmployeeId { get; set; }
        public string DisplayName { get; set; }
        public string WorkshopName { get; set; }
    }

    public sealed class TaskDelegationCardInfo
    {
        public bool CanDelegate { get; set; }
        public string StatusText { get; set; } = string.Empty;
        public string ChainText { get; set; } = string.Empty;
    }

    internal static class TaskDelegationService
    {
        private const int MaxChainDepth = 50;

        public const string AutoCompleteByParentReportTemplate =
            "Завершено автоматически: закрыто родительское задание ({0}).";

        public static async Task<TaskDelegationLinks> TryLoadLinksAsync(Guid taskId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.SqlQuery<TaskDelegationLinks>(
                        "SELECT ParentTaskId, DelegateTaskId FROM Tasks WHERE RowId = @p0", taskId)
                        .FirstOrDefaultAsync()).ConfigureAwait(false) ?? new TaskDelegationLinks();
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return new TaskDelegationLinks();
            }
        }

        public static async Task<List<DelegateEmployeeOption>> ListDelegateTargetsAsync(
            Guid currentEmployeeId,
            Guid sourceTaskId)
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

                return employees.Select(e => new DelegateEmployeeOption
                {
                    EmployeeId = e.RowId,
                    DisplayName = AppState.FormatEmployeeDisplayName(e),
                    WorkshopName = e.WorkshopId.HasValue && wsDict.TryGetValue(e.WorkshopId.Value, out var wn) ? wn : "—"
                }).ToList();
            }).ConfigureAwait(false);
        }

        public static async Task<TaskDelegationCardInfo> BuildCardInfoAsync(
            DriveCareDBEntities db,
            Guid taskId,
            Guid assigneeEmployeeId,
            bool archiveView,
            bool isCompleted)
        {
            var info = new TaskDelegationCardInfo();
            var links = await TryLoadLinksAsync(taskId).ConfigureAwait(false);

            info.CanDelegate = !archiveView && !isCompleted && !links.DelegateTaskId.HasValue;

            var sb = new StringBuilder();

            if (links.ParentTaskId.HasValue)
            {
                var fromName = await GetAssigneeNameAsync(db, links.ParentTaskId.Value).ConfigureAwait(false);
                sb.AppendLine("Поручение от: " + fromName + ".");
                sb.AppendLine("Можно передать дальше другому сотруднику — ваше задание останется у вас.");
            }

            if (links.DelegateTaskId.HasValue)
            {
                var child = await db.Tasks.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.RowId == links.DelegateTaskId.Value)
                    .ConfigureAwait(false);
                if (child != null)
                {
                    var childName = await GetEmployeeNameAsync(db, child.EmployeeId).ConfigureAwait(false);
                    if (child.IsCompleted)
                    {
                        sb.AppendLine(childName + " завершил(а) переданное задание.");
                        sb.AppendLine("Проверьте результат и завершите своё — следующему в цепочке откроется шаг выше.");
                    }
                    else
                        sb.AppendLine("Передано: " + childName + " — в работе.");
                }
            }
            else if (info.CanDelegate && !links.ParentTaskId.HasValue)
            {
                sb.AppendLine("Можно передать копию задания другому сотруднику. Ваше задание останется в списке.");
                sb.AppendLine("Сотрудник тоже сможет передать дальше при необходимости.");
            }

            info.StatusText = sb.ToString().Trim();
            info.ChainText = await BuildChainTextAsync(db, taskId).ConfigureAwait(false);
            return info;
        }

        public static async Task<(bool ok, string error, Guid? newTaskId)> DelegateAsync(
            Guid sourceTaskId,
            Guid fromEmployeeId,
            Guid toEmployeeId)
        {
            if (fromEmployeeId == toEmployeeId)
                return (false, "Нельзя передать задание самому себе.", null);

            try
            {
                return await DatabaseExecutor.WithDbAsync(async db =>
                {
                    var source = await db.Tasks.FirstOrDefaultAsync(t =>
                        t.RowId == sourceTaskId && t.EmployeeId == fromEmployeeId && !t.IsCompleted).ConfigureAwait(false);

                    if (source == null)
                        return (false, "Задание не найдено или уже завершено.", (Guid?)null);

                    var links = await TryLoadLinksAsync(sourceTaskId).ConfigureAwait(false);
                    if (links.DelegateTaskId.HasValue)
                        return (false, "Вы уже передали это задание дальше. Дождитесь завершения или завершите своё после исполнителя.", null);

                    if (await IsEmployeeInAncestorChainAsync(db, sourceTaskId, toEmployeeId).ConfigureAwait(false))
                        return (false, "Нельзя передать задание сотруднику, который уже есть выше по цепочке поручений.", null);

                    var scope = await CompletedTasksDataService.GetScopeEmployeeIdsAsync(fromEmployeeId).ConfigureAwait(false);
                    if (!scope.Contains(toEmployeeId))
                        return (false, "Сотрудник не из вашей организации.", null);

                    var fromEmp = await db.Employees.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.RowId == fromEmployeeId).ConfigureAwait(false);
                    var ownerName = fromEmp == null ? "коллега" : AppState.FormatEmployeeDisplayName(fromEmp);

                    var statusId = source.StatusId;
                    if (statusId == Guid.Empty)
                    {
                        statusId = await db.Statuses.Select(s => s.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
                        if (statusId == Guid.Empty)
                            return (false, "В справочнике нет статусов заданий.", null);
                    }

                    var childId = Guid.NewGuid();
                    var childTitle = source.Title ?? "Задание";
                    if (!childTitle.StartsWith("[Поручение]", StringComparison.OrdinalIgnoreCase))
                        childTitle = "[Поручение] " + childTitle.Trim();
                    if (childTitle.Length > 250)
                        childTitle = childTitle.Substring(0, 250);

                    var childDesc = new StringBuilder();
                    childDesc.AppendLine("Поручение от: " + ownerName);
                    childDesc.AppendLine("Ссылка на предыдущее задание в цепочке.");
                    childDesc.AppendLine();
                    if (!string.IsNullOrWhiteSpace(source.Description))
                        childDesc.AppendLine(source.Description.Trim());

                    var child = new TaskEntity
                    {
                        RowId = childId,
                        Title = childTitle,
                        Description = childDesc.ToString().Trim(),
                        EmployeeId = toEmployeeId,
                        StatusId = statusId,
                        CreatedAt = DateTime.Now,
                        StartDate = DateTime.Now,
                        IsCompleted = false,
                        CarId = source.CarId,
                        ClientUserId = source.ClientUserId
                    };

                    db.Tasks.Add(child);
                    await db.SaveChangesAsync().ConfigureAwait(false);

                    await CopyExtendedFieldsAsync(db, sourceTaskId, childId).ConfigureAwait(false);
                    await TrySetParentTaskIdAsync(db, childId, sourceTaskId).ConfigureAwait(false);
                    await TrySetDelegateTaskIdAsync(db, sourceTaskId, childId).ConfigureAwait(false);

                    return (true, null, childId);
                }).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return (false, "Колонки делегирования не найдены. Выполните SQL Tasks_Add_DelegationFields.sql", null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static async Task<string> BuildChainTextAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                var chain = new List<string>();
                var current = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RowId == taskId).ConfigureAwait(false);
                if (current == null)
                    return string.Empty;

                var currentLinks = await TryLoadLinksAsync(taskId).ConfigureAwait(false);

                var walkId = currentLinks.ParentTaskId;
                var depth = 0;
                while (walkId.HasValue && depth++ < MaxChainDepth)
                {
                    var parent = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RowId == walkId.Value).ConfigureAwait(false);
                    if (parent == null)
                        break;

                    var name = await GetEmployeeNameAsync(db, parent.EmployeeId).ConfigureAwait(false);
                    chain.Insert(0, name + (parent.IsCompleted ? " ✓" : string.Empty));

                    var parentLinks = await TryLoadLinksAsync(parent.RowId).ConfigureAwait(false);
                    walkId = parentLinks.ParentTaskId;
                }

                var myName = await GetEmployeeNameAsync(db, current.EmployeeId).ConfigureAwait(false);
                chain.Add(myName + (current.IsCompleted ? " ✓" : " (вы)"));

                var downId = currentLinks.DelegateTaskId;
                depth = 0;
                while (downId.HasValue && depth++ < MaxChainDepth)
                {
                    var child = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RowId == downId.Value).ConfigureAwait(false);
                    if (child == null)
                        break;

                    var name = await GetEmployeeNameAsync(db, child.EmployeeId).ConfigureAwait(false);
                    chain.Add(name + (child.IsCompleted ? " ✓" : " …"));

                    var childLinks = await TryLoadLinksAsync(child.RowId).ConfigureAwait(false);
                    downId = childLinks.DelegateTaskId;
                }

                return chain.Count <= 1 ? string.Empty : "Цепочка: " + string.Join(" → ", chain);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static async Task<bool> IsEmployeeInAncestorChainAsync(
            DriveCareDBEntities db,
            Guid sourceTaskId,
            Guid toEmployeeId)
        {
            var links = await TryLoadLinksAsync(sourceTaskId).ConfigureAwait(false);
            var walkId = links.ParentTaskId;
            var depth = 0;

            while (walkId.HasValue && depth++ < MaxChainDepth)
            {
                var parent = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RowId == walkId.Value).ConfigureAwait(false);
                if (parent == null)
                    break;

                if (parent.EmployeeId == toEmployeeId)
                    return true;

                var parentLinks = await TryLoadLinksAsync(parent.RowId).ConfigureAwait(false);
                walkId = parentLinks.ParentTaskId;
            }

            return false;
        }

        private static async Task<string> GetAssigneeNameAsync(DriveCareDBEntities db, Guid taskId)
        {
            var taskRow = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RowId == taskId).ConfigureAwait(false);
            return taskRow == null ? "—" : await GetEmployeeNameAsync(db, taskRow.EmployeeId).ConfigureAwait(false);
        }

        private static async Task<string> GetEmployeeNameAsync(DriveCareDBEntities db, Guid employeeId)
        {
            var emp = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.RowId == employeeId).ConfigureAwait(false);
            return emp == null ? "сотрудник" : AppState.FormatEmployeeDisplayName(emp);
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

        private static async Task TrySetDelegateTaskIdAsync(DriveCareDBEntities db, Guid parentId, Guid childId)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE Tasks SET DelegateTaskId = @p1 WHERE RowId = @p0", parentId, childId).ConfigureAwait(false);
            }
            catch (SqlException)
            {
            }
        }

        /// <summary>
        /// Закрывает все незавершённые задания ниже по цепочке DelegateTaskId.
        /// </summary>
        public static async Task<int> CompleteDescendantChainAsync(
            DriveCareDBEntities db,
            Guid parentTaskId,
            string parentAssigneeName)
        {
            var closed = 0;
            var reportNote = string.Format(AutoCompleteByParentReportTemplate,
                string.IsNullOrWhiteSpace(parentAssigneeName) ? "инициатор" : parentAssigneeName.Trim());

            var links = await TryLoadLinksAsync(parentTaskId).ConfigureAwait(false);
            var downId = links.DelegateTaskId;
            var depth = 0;
            var now = DateTime.Now;

            while (downId.HasValue && depth++ < MaxChainDepth)
            {
                var child = await db.Tasks.FirstOrDefaultAsync(t => t.RowId == downId.Value).ConfigureAwait(false);
                if (child == null)
                    break;

                if (!child.IsCompleted)
                {
                    child.IsCompleted = true;
                    child.EndDate = now;
                    child.ReportText = AppendAutoReportNote(child.ReportText, reportNote);
                    closed++;
                }

                var childLinks = await TryLoadLinksAsync(child.RowId).ConfigureAwait(false);
                downId = childLinks.DelegateTaskId;
            }

            return closed;
        }

        private static string AppendAutoReportNote(string existing, string autoNote)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return autoNote;
            if (existing.IndexOf(autoNote, StringComparison.OrdinalIgnoreCase) >= 0)
                return existing;
            return existing.TrimEnd() + Environment.NewLine + Environment.NewLine + autoNote;
        }
    }
}
