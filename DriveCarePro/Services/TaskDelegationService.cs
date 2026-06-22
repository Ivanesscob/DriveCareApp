using DriveCareCore.Data.BD;
using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Services.ServiceDocuments;
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

        public const string AutoCompleteByDocumentReportTemplate =
            "Завершено автоматически: закрыт заказ-наряд ({0}).";

        public static async Task<TaskDelegationLinks> TryLoadLinksAsync(Guid taskId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(db => TryLoadLinksAsync(db, taskId)).ConfigureAwait(false)
                       ?? new TaskDelegationLinks();
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return new TaskDelegationLinks();
            }
        }

        public static async Task<TaskDelegationLinks> TryLoadLinksAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                return await db.Database.SqlQuery<TaskDelegationLinks>(
                        "SELECT ParentTaskId, DelegateTaskId FROM Tasks WHERE RowId = @p0", taskId)
                    .FirstOrDefaultAsync().ConfigureAwait(false) ?? new TaskDelegationLinks();
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return new TaskDelegationLinks();
            }
        }

        /// <summary>Задание-отправитель, у которого DelegateTaskId указывает на childTaskId.</summary>
        public static async Task<Guid?> TryGetOwnerTaskIdByDelegateAsync(DriveCareDBEntities db, Guid childTaskId)
        {
            try
            {
                var id = await db.Database.SqlQuery<Guid?>(
                        "SELECT RowId FROM Tasks WHERE DelegateTaskId = @p0", childTaskId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);
                return id.HasValue && id.Value != Guid.Empty ? id : (Guid?)null;
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        /// <summary>Корневое (самое верхнее) задание в цепочке поручений.</summary>
        public static Task<Guid> FindRootTaskIdAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => FindRootTaskIdAsync(db, taskId));

        public static async Task<Guid> FindRootTaskIdAsync(DriveCareDBEntities db, Guid taskId)
        {
            var chain = await CollectOrderedChainTaskIdsAsync(db, taskId).ConfigureAwait(false);
            return chain.Count > 0 ? chain[0] : taskId;
        }

        /// <summary>Упорядоченная цепочка: предки → текущее → переданные вниз (как в BuildChainTextAsync).</summary>
        public static async Task<List<Guid>> CollectOrderedChainTaskIdsAsync(DriveCareDBEntities db, Guid taskId)
        {
            var ordered = new List<Guid>();
            var upstream = new List<Guid>();
            var walk = taskId;

            for (var depth = 0; depth < MaxChainDepth; depth++)
            {
                var links = await TryLoadLinksAsync(db, walk).ConfigureAwait(false);
                Guid? parentId = links.ParentTaskId;
                if (!parentId.HasValue)
                    parentId = await TryGetOwnerTaskIdByDelegateAsync(db, walk).ConfigureAwait(false);
                if (!parentId.HasValue)
                    break;

                upstream.Insert(0, parentId.Value);
                walk = parentId.Value;
            }

            ordered.AddRange(upstream);
            ordered.Add(taskId);

            var downLinks = await TryLoadLinksAsync(db, taskId).ConfigureAwait(false);
            var downId = downLinks.DelegateTaskId;
            for (var depth = 0; depth < MaxChainDepth && downId.HasValue; depth++)
            {
                ordered.Add(downId.Value);
                var childLinks = await TryLoadLinksAsync(db, downId.Value).ConfigureAwait(false);
                downId = childLinks.DelegateTaskId;
            }

            return ordered.Distinct().ToList();
        }

        /// <summary>Все задания ниже по ParentTaskId и DelegateTaskId завершены.</summary>
        public static async Task<bool> IsDelegateSubtreeFullyCompletedAsync(DriveCareDBEntities db, Guid taskId)
        {
            List<Guid> ids;
            try
            {
                ids = await CollectSubtreeTaskIdsAsync(db, taskId).ConfigureAwait(false);
            }
            catch (SqlException)
            {
                return false;
            }

            if (ids.Count == 0)
                return false;

            var rows = await db.Tasks.AsNoTracking()
                .Where(t => ids.Contains(t.RowId))
                .Select(t => t.IsCompleted)
                .ToListAsync()
                .ConfigureAwait(false);

            return rows.Count == ids.Count && rows.All(c => c);
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
            var links = await TryLoadLinksAsync(db, taskId).ConfigureAwait(false);

            info.CanDelegate = !archiveView && !isCompleted && !links.DelegateTaskId.HasValue;

            var sb = new StringBuilder();

            if (links.ParentTaskId.HasValue)
            {
                var fromName = await GetAssigneeNameAsync(db, links.ParentTaskId.Value).ConfigureAwait(false);
                sb.AppendLine("Поручение от: " + fromName + ".");
                sb.AppendLine("Услуги и запчасти попадают в общий документ заказ-наряда при сохранении или завершении.");
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
                    await ServiceDocumentService.TryCopyDocumentIdAsync(db, sourceTaskId, childId).ConfigureAwait(false);
                    await CopyReportLinesAsync(db, sourceTaskId, childId).ConfigureAwait(false);

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

                var orderedIds = await CollectOrderedChainTaskIdsAsync(db, taskId).ConfigureAwait(false);
                if (orderedIds.Count <= 1)
                    return string.Empty;

                foreach (var id in orderedIds)
                {
                    var row = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.RowId == id).ConfigureAwait(false);
                    if (row == null)
                        continue;

                    var name = await GetEmployeeNameAsync(db, row.EmployeeId).ConfigureAwait(false);
                    if (id == taskId)
                        chain.Add(name + (row.IsCompleted ? " ✓" : " (вы)"));
                    else
                        chain.Add(name + (row.IsCompleted ? " ✓" : " …"));
                }

                return "Цепочка: " + string.Join(" → ", chain);
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
            var reportNote = FormatAutoCloseNote(AutoCompleteByParentReportTemplate, parentAssigneeName);
            var links = await TryLoadLinksAsync(parentTaskId).ConfigureAwait(false);
            var downId = links.DelegateTaskId;
            var depth = 0;
            var now = DateTime.Now;

            while (downId.HasValue && depth++ < MaxChainDepth)
            {
                var child = await db.Tasks.FirstOrDefaultAsync(t => t.RowId == downId.Value).ConfigureAwait(false);
                if (child == null)
                    break;

                if (TryMarkAutoCompleted(child, reportNote, now))
                    closed++;

                var childLinks = await TryLoadLinksAsync(child.RowId).ConfigureAwait(false);
                downId = childLinks.DelegateTaskId;
            }

            return closed;
        }

        /// <summary>
        /// Закрывает все незавершённые задания документа (поручения, закупки и т.д.).
        /// </summary>
        public static async Task<int> CompleteOpenTasksForDocumentAsync(
            DriveCareDBEntities db,
            Guid documentId,
            string closedByName,
            Guid? excludeTaskId = null)
        {
            List<Guid> taskIds;
            try
            {
                taskIds = await db.Database.SqlQuery<Guid>(
                    "SELECT RowId FROM Tasks WHERE DocumentId = @p0", documentId).ToListAsync().ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return 0;
            }

            var note = FormatAutoCloseNote(AutoCompleteByDocumentReportTemplate, closedByName);
            return await CompleteTasksWithNoteAsync(db, taskIds, note, excludeTaskId).ConfigureAwait(false);
        }

        /// <summary>
        /// Закрывает всё поддерево от корня (ParentTaskId + DelegateTaskId).
        /// </summary>
        public static async Task<int> CompleteEntireSubtreeAsync(
            DriveCareDBEntities db,
            Guid rootTaskId,
            string closedByName,
            Guid? excludeTaskId = null)
        {
            var taskIds = await CollectSubtreeTaskIdsAsync(db, rootTaskId).ConfigureAwait(false);
            var note = FormatAutoCloseNote(AutoCompleteByParentReportTemplate, closedByName);
            return await CompleteTasksWithNoteAsync(db, taskIds, note, excludeTaskId).ConfigureAwait(false);
        }

        private static async Task<List<Guid>> CollectSubtreeTaskIdsAsync(DriveCareDBEntities db, Guid rootTaskId)
        {
            var ids = new HashSet<Guid> { rootTaskId };
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var id in ids.ToList())
                {
                    try
                    {
                        var children = await db.Database.SqlQuery<Guid>(
                            "SELECT RowId FROM Tasks WHERE ParentTaskId = @p0", id).ToListAsync().ConfigureAwait(false);
                        foreach (var childId in children)
                        {
                            if (ids.Add(childId))
                                changed = true;
                        }

                        var delegateId = await db.Database.SqlQuery<Guid?>(
                            "SELECT DelegateTaskId FROM Tasks WHERE RowId = @p0", id).FirstOrDefaultAsync().ConfigureAwait(false);
                        if (delegateId.HasValue && delegateId.Value != Guid.Empty && ids.Add(delegateId.Value))
                            changed = true;
                    }
                    catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
                    {
                        return ids.ToList();
                    }
                }
            }

            return ids.ToList();
        }

        private static async Task<int> CompleteTasksWithNoteAsync(
            DriveCareDBEntities db,
            IList<Guid> taskIds,
            string reportNote,
            Guid? excludeTaskId)
        {
            var closed = 0;
            var now = DateTime.Now;
            foreach (var id in taskIds)
            {
                if (excludeTaskId.HasValue && excludeTaskId.Value == id)
                    continue;

                var task = await db.Tasks.FirstOrDefaultAsync(t => t.RowId == id).ConfigureAwait(false);
                if (task == null)
                    continue;

                if (TryMarkAutoCompleted(task, reportNote, now))
                    closed++;
            }

            return closed;
        }

        private static bool TryMarkAutoCompleted(TaskEntity task, string reportNote, DateTime now)
        {
            if (task.IsCompleted)
                return false;

            task.IsCompleted = true;
            task.EndDate = now;
            task.ReportText = AppendAutoReportNote(task.ReportText, reportNote);
            return true;
        }

        private static string FormatAutoCloseNote(string template, string closedByName)
        {
            var name = string.IsNullOrWhiteSpace(closedByName) ? "инициатор" : closedByName.Trim();
            return string.Format(template, name);
        }

        private static string AppendAutoReportNote(string existing, string autoNote)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return autoNote;
            if (existing.IndexOf(autoNote, StringComparison.OrdinalIgnoreCase) >= 0)
                return existing;
            return existing.TrimEnd() + Environment.NewLine + Environment.NewLine + autoNote;
        }

        /// <summary>
        /// Переносит строки запчастей из поручения вверх по цепочке ParentTaskId (единый заказ-наряд).
        /// </summary>
        public static Task SyncPartLinesToAncestorsAsync(Guid childTaskId) =>
            DatabaseExecutor.WithDbAsync(db => SyncPartLinesToAncestorsAsync(db, childTaskId));

        /// <summary>
        /// Переносит строки услуг из поручения вверх по цепочке ParentTaskId.
        /// </summary>
        public static Task SyncServiceLinesToAncestorsAsync(Guid childTaskId) =>
            DatabaseExecutor.WithDbAsync(db => SyncServiceLinesToAncestorsAsync(db, childTaskId));

        public static async Task SyncServiceLinesToAncestorsAsync(DriveCareDBEntities db, Guid childTaskId)
        {
            var childServices = await TaskReportService.LoadServiceLinesAsync(db, childTaskId).ConfigureAwait(false);
            childServices = childServices.Where(s => !string.IsNullOrWhiteSpace(s.ServiceName)).ToList();
            if (childServices.Count == 0)
                return;

            var links = await TryLoadLinksAsync(childTaskId).ConfigureAwait(false);
            var walkId = links.ParentTaskId;
            var depth = 0;

            while (walkId.HasValue && walkId.Value != Guid.Empty && depth++ < MaxChainDepth)
            {
                var parentId = walkId.Value;
                await MergeChildServicesIntoParentAsync(db, parentId, childServices).ConfigureAwait(false);

                var parentLinks = await TryLoadLinksAsync(parentId).ConfigureAwait(false);
                walkId = parentLinks.ParentTaskId;
            }
        }

        public static async Task SyncPartLinesToAncestorsAsync(DriveCareDBEntities db, Guid childTaskId)
        {
            var childParts = await TaskReportService.LoadPartLinesAsync(db, childTaskId).ConfigureAwait(false);
            childParts = childParts.Where(p => !string.IsNullOrWhiteSpace(p.PartName)).ToList();
            if (childParts.Count == 0)
                return;

            var links = await TryLoadLinksAsync(childTaskId).ConfigureAwait(false);
            var walkId = links.ParentTaskId;
            var depth = 0;

            while (walkId.HasValue && walkId.Value != Guid.Empty && depth++ < MaxChainDepth)
            {
                var parentId = walkId.Value;
                await MergeChildPartsIntoParentAsync(db, parentId, childParts).ConfigureAwait(false);

                var parentLinks = await TryLoadLinksAsync(parentId).ConfigureAwait(false);
                walkId = parentLinks.ParentTaskId;
            }
        }

        /// <summary>
        /// WorkshopPartId, уже списанные в переданных поручениях ниже — не списывать повторно у инициатора.
        /// </summary>
        public static async Task<HashSet<Guid>> GetWorkshopPartIdsInDelegateSubtreeAsync(Guid taskId)
        {
            var set = new HashSet<Guid>();
            var links = await TryLoadLinksAsync(taskId).ConfigureAwait(false);
            var downId = links.DelegateTaskId;
            var depth = 0;

            while (downId.HasValue && depth++ < MaxChainDepth)
            {
                try
                {
                    var parts = await TaskReportService.LoadPartLinesAsync(downId.Value).ConfigureAwait(false);
                    foreach (var p in parts)
                    {
                        if (p.WorkshopPartId.HasValue && p.WorkshopPartId.Value != Guid.Empty)
                            set.Add(p.WorkshopPartId.Value);
                    }
                }
                catch
                {
                    break;
                }

                var childLinks = await TryLoadLinksAsync(downId.Value).ConfigureAwait(false);
                downId = childLinks.DelegateTaskId;
            }

            return set;
        }

        private static async Task CopyReportLinesAsync(DriveCareDBEntities db, Guid fromTaskId, Guid toTaskId)
        {
            var services = await TaskReportService.LoadServiceLinesAsync(db, fromTaskId).ConfigureAwait(false);
            var parts = await TaskReportService.LoadPartLinesAsync(db, fromTaskId).ConfigureAwait(false);
            if (services.Count == 0 && parts.Count == 0)
                return;

            var freeNote = await TaskReportService.LoadFreeTextNoteAsync(db, fromTaskId).ConfigureAwait(false);
            await TaskReportService.SaveReportAsync(db, toTaskId, services, parts, freeNote).ConfigureAwait(false);
        }

        private static async Task MergeChildServicesIntoParentAsync(
            DriveCareDBEntities db,
            Guid parentTaskId,
            IList<TaskServiceLineRow> childServices)
        {
            var parentServices = await TaskReportService.LoadServiceLinesAsync(db, parentTaskId).ConfigureAwait(false);
            var merged = MergeServiceLists(parentServices, childServices);
            if (ServicesListsEquivalent(parentServices, merged))
                return;

            var parts = await TaskReportService.LoadPartLinesAsync(db, parentTaskId).ConfigureAwait(false);
            var freeNote = await TaskReportService.LoadFreeTextNoteAsync(db, parentTaskId).ConfigureAwait(false);
            await TaskReportService.SaveReportAsync(db, parentTaskId, merged, parts, freeNote).ConfigureAwait(false);
        }

        private static async Task MergeChildPartsIntoParentAsync(
            DriveCareDBEntities db,
            Guid parentTaskId,
            IList<TaskPartLineRow> childParts)
        {
            var parentParts = await TaskReportService.LoadPartLinesAsync(db, parentTaskId).ConfigureAwait(false);
            var merged = MergePartLists(parentParts, childParts);
            if (PartsListsEquivalent(parentParts, merged))
                return;

            var services = await TaskReportService.LoadServiceLinesAsync(db, parentTaskId).ConfigureAwait(false);
            var freeNote = await TaskReportService.LoadFreeTextNoteAsync(db, parentTaskId).ConfigureAwait(false);
            await TaskReportService.SaveReportAsync(db, parentTaskId, services, merged, freeNote).ConfigureAwait(false);
        }

        private static List<TaskServiceLineRow> MergeServiceLists(
            IList<TaskServiceLineRow> parentServices,
            IList<TaskServiceLineRow> incomingServices)
        {
            var result = parentServices.Select(CloneServiceRow).ToList();

            foreach (var inc in incomingServices)
            {
                if (string.IsNullOrWhiteSpace(inc.ServiceName))
                    continue;

                var key = ServiceMatchKey(inc);
                var existing = result.FirstOrDefault(s => ServiceMatchKey(s) == key);
                if (existing == null)
                {
                    result.Add(CloneServiceRow(inc));
                    continue;
                }

                if (inc.Quantity > existing.Quantity)
                    existing.Quantity = inc.Quantity;
                if (inc.UnitPrice > 0)
                    existing.UnitPrice = inc.UnitPrice;
                if (inc.DiscountPercent > existing.DiscountPercent)
                    existing.DiscountPercent = inc.DiscountPercent;
                existing.RecalculateAmount();
            }

            return result;
        }

        private static bool ServicesListsEquivalent(IList<TaskServiceLineRow> left, IList<TaskServiceLineRow> right)
        {
            var leftMap = left
                .GroupBy(ServiceMatchKey)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var rightMap = right
                .GroupBy(ServiceMatchKey)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            if (leftMap.Count != rightMap.Count)
                return false;

            foreach (var kv in leftMap)
            {
                if (!rightMap.TryGetValue(kv.Key, out var r))
                    return false;
                if (kv.Value.Quantity != r.Quantity || kv.Value.UnitPrice != r.UnitPrice)
                    return false;
            }

            return true;
        }

        private static string ServiceMatchKey(TaskServiceLineRow s)
        {
            if (s.WorkshopServiceId.HasValue && s.WorkshopServiceId.Value != Guid.Empty)
                return "id:" + s.WorkshopServiceId.Value.ToString("N");

            return "n:" + (s.ServiceName ?? string.Empty).Trim().ToLowerInvariant()
                   + "|" + (s.UnitName ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static TaskServiceLineRow CloneServiceRow(TaskServiceLineRow s) => new TaskServiceLineRow
        {
            RowId = s.RowId,
            WorkshopServiceId = s.WorkshopServiceId,
            ServiceName = s.ServiceName,
            Quantity = s.Quantity,
            UnitName = s.UnitName,
            UnitPrice = s.UnitPrice,
            DiscountPercent = s.DiscountPercent,
            LineAmount = s.LineAmount
        };

        private static List<TaskPartLineRow> MergePartLists(
            IList<TaskPartLineRow> parentParts,
            IList<TaskPartLineRow> incomingParts)
        {
            var result = parentParts.Select(ClonePartRow).ToList();

            foreach (var inc in incomingParts)
            {
                if (string.IsNullOrWhiteSpace(inc.PartName))
                    continue;

                var key = PartMatchKey(inc);
                var existing = result.FirstOrDefault(p => PartMatchKey(p) == key);
                if (existing == null)
                {
                    result.Add(ClonePartRow(inc));
                    continue;
                }

                if (inc.Quantity > existing.Quantity)
                    existing.Quantity = inc.Quantity;
                if (inc.UnitPrice > 0)
                    existing.UnitPrice = inc.UnitPrice;
                existing.RecalculateAmount();
            }

            return result;
        }

        private static bool PartsListsEquivalent(IList<TaskPartLineRow> left, IList<TaskPartLineRow> right)
        {
            var leftMap = left
                .GroupBy(PartMatchKey)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            var rightMap = right
                .GroupBy(PartMatchKey)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            if (leftMap.Count != rightMap.Count)
                return false;

            foreach (var kv in leftMap)
            {
                if (!rightMap.TryGetValue(kv.Key, out var r))
                    return false;
                if (kv.Value.Quantity != r.Quantity || kv.Value.UnitPrice != r.UnitPrice)
                    return false;
            }

            return true;
        }

        private static string PartMatchKey(TaskPartLineRow p)
        {
            if (p.WorkshopPartId.HasValue && p.WorkshopPartId.Value != Guid.Empty)
                return "id:" + p.WorkshopPartId.Value.ToString("N");

            return "n:" + (p.PartName ?? string.Empty).Trim().ToLowerInvariant()
                   + "|" + (p.UnitName ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static TaskPartLineRow ClonePartRow(TaskPartLineRow p) => new TaskPartLineRow
        {
            RowId = p.RowId,
            WorkshopPartId = p.WorkshopPartId,
            PartName = p.PartName,
            Quantity = p.Quantity,
            UnitName = p.UnitName,
            UnitPrice = p.UnitPrice,
            LineAmount = p.LineAmount
        };
    }
}
