using DriveCareCore.Data.BD;
using DriveCareCore.ServiceVisits;
using DriveCarePro.Services.ServiceBooking;
using DriveCarePro.Services.WorkshopServices;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.ServiceDocuments
{
    internal static class ServiceDocumentService
    {
        public static Task<Guid?> TryGetDocumentIdForTaskAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => TryGetDocumentIdForTaskAsync(db, taskId));

        public static async Task<Guid?> TryGetDocumentIdForTaskAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                var id = await db.Database.SqlQuery<Guid?>(
                    "SELECT DocumentId FROM Tasks WHERE RowId = @p0", taskId).FirstOrDefaultAsync().ConfigureAwait(false);
                return id.HasValue && id.Value != Guid.Empty ? id : (Guid?)null;
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        /// <summary>DocumentId у задания или у родителя по цепочке поручений.</summary>
        public static Task<Guid?> TryResolveDocumentIdForTaskAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => TryResolveDocumentIdForTaskAsync(db, taskId));

        public static async Task<Guid?> TryResolveDocumentIdForTaskAsync(DriveCareDBEntities db, Guid taskId)
        {
            var walk = taskId;
            for (var i = 0; i < 64; i++)
            {
                var docId = await TryGetDocumentIdForTaskAsync(db, walk).ConfigureAwait(false);
                if (docId.HasValue)
                    return docId;

                var parentId = await TryGetParentTaskIdAsync(db, walk).ConfigureAwait(false);
                if (!parentId.HasValue)
                    break;
                walk = parentId.Value;
            }

            return null;
        }

        private static async Task<Guid?> TryGetParentTaskIdAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                var parent = await db.Database.SqlQuery<Guid?>(
                    "SELECT ParentTaskId FROM Tasks WHERE RowId = @p0", taskId).FirstOrDefaultAsync().ConfigureAwait(false);
                return parent.HasValue && parent.Value != Guid.Empty ? parent : (Guid?)null;
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        private static async Task<Guid?> TryGetDelegateTaskIdAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                var id = await db.Database.SqlQuery<Guid?>(
                    "SELECT DelegateTaskId FROM Tasks WHERE RowId = @p0", taskId).FirstOrDefaultAsync().ConfigureAwait(false);
                return id.HasValue && id.Value != Guid.Empty ? id : (Guid?)null;
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        /// <summary>Родительское задание, которое передало работу текущему (обратная связь по DelegateTaskId).</summary>
        private static async Task<Guid?> TryGetOwnerTaskIdByDelegateAsync(DriveCareDBEntities db, Guid childTaskId)
        {
            try
            {
                var id = await db.Database.SqlQuery<Guid?>(
                    "SELECT RowId FROM Tasks WHERE DelegateTaskId = @p0", childTaskId).FirstOrDefaultAsync().ConfigureAwait(false);
                return id.HasValue && id.Value != Guid.Empty ? id : (Guid?)null;
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        public static async Task<Guid?> CreateForBookingAsync(
            DriveCareDBEntities db,
            ServiceBookingContext ctx,
            Guid taskId,
            Guid carId,
            Guid repairHistoryId,
            Guid workshopId)
        {
            try
            {
                var docId = Guid.NewGuid();
                var title = string.IsNullOrWhiteSpace(ctx?.CarDescription)
                    ? "Заказ-наряд"
                    : "Заказ-наряд: " + ctx.CarDescription.Trim();
                if (title.Length > 300)
                    title = title.Substring(0, 300);

                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO ServiceDocuments
                      (RowId, RootTaskId, RepairHistoryId, WorkshopId, CarId, ClientUserId, Title,
                       ClientName, ClientPhone, ClientEmail, VisitReason, SpecialNotes, ServiceKind, Status, CreatedAt)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,0,@p13)",
                    docId, taskId, repairHistoryId, workshopId, carId,
                    (object)ctx?.FoundUser?.RowId ?? DBNull.Value,
                    title,
                    (object)ctx?.ClientFullName ?? DBNull.Value,
                    (object)ctx?.ClientPhone ?? DBNull.Value,
                    (object)ctx?.ClientEmail ?? DBNull.Value,
                    (object)ctx?.VisitReason ?? DBNull.Value,
                    (object)ctx?.SpecialNotes ?? DBNull.Value,
                    ctx?.RepairTypeDisplay ?? string.Empty,
                    DateTime.Now).ConfigureAwait(false);

                await ServiceDocumentClientStageService.TryMarkAcceptedAsync(db, taskId).ConfigureAwait(false);
                await TrySetTaskDocumentIdAsync(db, taskId, docId).ConfigureAwait(false);
                return docId;
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return null;
            }
        }

        public static Task TrySetTaskDocumentIdAsync(DriveCareDBEntities db, Guid taskId, Guid documentId) =>
            TrySetTaskDocumentIdAsync(db, taskId, (Guid?)documentId);

        public static async Task TrySetTaskDocumentIdAsync(DriveCareDBEntities db, Guid taskId, Guid? documentId)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "UPDATE Tasks SET DocumentId = @p1 WHERE RowId = @p0",
                    taskId, (object)documentId ?? DBNull.Value).ConfigureAwait(false);
            }
            catch (SqlException)
            {
            }
        }

        public static async Task TryCopyDocumentIdAsync(DriveCareDBEntities db, Guid fromTaskId, Guid toTaskId)
        {
            var docId = await TryGetDocumentIdForTaskAsync(db, fromTaskId).ConfigureAwait(false);
            if (docId.HasValue)
                await TrySetTaskDocumentIdAsync(db, toTaskId, docId.Value).ConfigureAwait(false);
        }

        /// <summary>Собирает услуги и запчасти со всех заданий документа в единый заказ-наряд.</summary>
        public static Task SyncDocumentFromChainAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => SyncDocumentFromChainAsync(db, taskId));

        public static async Task SyncDocumentFromChainAsync(DriveCareDBEntities db, Guid taskId)
        {
            var docId = await TryResolveDocumentIdForTaskAsync(db, taskId).ConfigureAwait(false);
            if (!docId.HasValue)
                return;

            var taskIds = await LoadTaskIdsForDocumentAsync(db, docId.Value).ConfigureAwait(false);
            if (taskIds.Count == 0)
                return;

            var allServices = new List<TaskServiceLineRow>();
            var allParts = new List<TaskPartLineRow>();
            var freeNotes = new List<string>();

            foreach (var tid in taskIds)
            {
                allServices.AddRange(await TaskReportService.LoadServiceLinesAsync(db, tid).ConfigureAwait(false));
                allParts.AddRange(await TaskReportService.LoadPartLinesAsync(db, tid).ConfigureAwait(false));

                var note = await TaskReportService.LoadFreeTextNoteAsync(db, tid).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(note))
                    freeNotes.Add(note.Trim());
            }

            var mergedServices = MergeServiceLines(allServices);
            var mergedParts = MergePartLines(allParts);
            var combinedNote = string.Join(Environment.NewLine + Environment.NewLine,
                freeNotes.Distinct(StringComparer.OrdinalIgnoreCase));

            await SaveDocumentReportAsync(db, docId.Value, mergedServices, mergedParts, combinedNote)
                .ConfigureAwait(false);
        }

        public static Task<ServiceDocumentPreview> TryLoadPreviewAsync(Guid taskId, bool syncFromChainFirst = true) =>
            DatabaseExecutor.WithDbAsync(db => TryLoadPreviewAsync(db, taskId, syncFromChainFirst));

        public static async Task<ServiceDocumentPreview> TryLoadPreviewAsync(
            DriveCareDBEntities db,
            Guid taskId,
            bool syncFromChainFirst = true)
        {
            if (syncFromChainFirst)
                await SyncDocumentFromChainAsync(db, taskId).ConfigureAwait(false);

            var info = await TryLoadInfoAsync(db, taskId).ConfigureAwait(false);
            if (info != null)
            {
                try
                {
                    var reportText = await db.Database.SqlQuery<string>(
                        "SELECT ReportText FROM ServiceDocuments WHERE RowId = @p0", info.DocumentId)
                        .FirstOrDefaultAsync().ConfigureAwait(false);

                    var services = await db.Database.SqlQuery<TaskServiceLineRow>(
                        @"SELECT CAST(NULL AS uniqueidentifier) AS RowId, WorkshopServiceId, ServiceName, Quantity,
                                 UnitName, UnitPrice, DiscountPercent, LineAmount
                          FROM ServiceDocumentServiceLines WHERE DocumentId = @p0 ORDER BY SortOrder",
                        info.DocumentId).ToListAsync().ConfigureAwait(false);

                    var parts = await db.Database.SqlQuery<TaskPartLineRow>(
                        @"SELECT CAST(NULL AS uniqueidentifier) AS RowId, WorkshopPartId, PartName, Quantity,
                                 UnitName, UnitPrice, LineAmount
                          FROM ServiceDocumentPartLines WHERE DocumentId = @p0 ORDER BY SortOrder",
                        info.DocumentId).ToListAsync().ConfigureAwait(false);

                    return new ServiceDocumentPreview
                    {
                        Info = info,
                        ReportText = reportText ?? string.Empty,
                        Services = services,
                        Parts = parts
                    };
                }
                catch (SqlException)
                {
                }
            }

            return await TryLoadPreviewFromTaskChainAsync(db, taskId).ConfigureAwait(false);
        }

        /// <summary>Просмотр без таблицы ServiceDocuments — сводка по цепочке заданий.</summary>
        private static async Task<ServiceDocumentPreview> TryLoadPreviewFromTaskChainAsync(
            DriveCareDBEntities db,
            Guid taskId)
        {
            var rootId = await FindRootTaskIdAsync(db, taskId).ConfigureAwait(false);
            var taskIds = await CollectDelegationChainTaskIdsAsync(db, rootId).ConfigureAwait(false);
            if (taskIds.Count == 0)
                taskIds.Add(taskId);

            var allServices = new List<TaskServiceLineRow>();
            var allParts = new List<TaskPartLineRow>();
            var freeNotes = new List<string>();
            string title = null;

            foreach (var tid in taskIds.Distinct())
            {
                allServices.AddRange(await TaskReportService.LoadServiceLinesAsync(db, tid).ConfigureAwait(false));
                allParts.AddRange(await TaskReportService.LoadPartLinesAsync(db, tid).ConfigureAwait(false));
                var note = await TaskReportService.LoadFreeTextNoteAsync(db, tid).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(note))
                    freeNotes.Add(note.Trim());

                if (tid == taskId || tid == rootId)
                {
                    var t = await db.Tasks.AsNoTracking()
                        .Where(x => x.RowId == tid)
                        .Select(x => x.Title)
                        .FirstOrDefaultAsync().ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(t))
                        title = t.Trim();
                }
            }

            if (string.IsNullOrWhiteSpace(title))
                title = "Задание";

            return new ServiceDocumentPreview
            {
                Info = new ServiceDocumentInfo
                {
                    DocumentId = Guid.Empty,
                    RootTaskId = rootId,
                    Status = ServiceDocumentStatus.Open,
                    Title = "Сводка: " + (title.Length > 120 ? title.Substring(0, 117) + "…" : title),
                    StatusDisplay = "По цепочке заданий (без общего документа в БД)",
                    CreatedAt = DateTime.Now,
                    IsCurrentTaskRoot = rootId == taskId
                },
                ReportText = string.Join(Environment.NewLine + Environment.NewLine,
                    freeNotes.Distinct(StringComparer.OrdinalIgnoreCase)),
                Services = MergeServiceLines(allServices),
                Parts = MergePartLines(allParts)
            };
        }

        public static Task<ServiceDocumentInfo> TryLoadInfoAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => TryLoadInfoAsync(db, taskId));

        public static async Task<ServiceDocumentInfo> TryLoadInfoAsync(DriveCareDBEntities db, Guid taskId)
        {
            var docId = await TryResolveDocumentIdForTaskAsync(db, taskId).ConfigureAwait(false);
            if (!docId.HasValue)
                return null;

            try
            {
                var row = await db.Database.SqlQuery<DocumentRow>(
                    @"SELECT RowId, RootTaskId, Title, Status, CreatedAt, CompletedAt
                      FROM ServiceDocuments WHERE RowId = @p0", docId.Value).FirstOrDefaultAsync().ConfigureAwait(false);
                if (row == null)
                    return null;

                var status = row.Status == 1 ? ServiceDocumentStatus.Completed : ServiceDocumentStatus.Open;
                var stage = await ServiceDocumentClientStageService
                    .TryGetStageForRootTaskAsync(db, row.RootTaskId).ConfigureAwait(false)
                    ?? ServiceDocumentClientStageLabels.Normalize(0, row.Status);
                return new ServiceDocumentInfo
                {
                    DocumentId = row.RowId,
                    RootTaskId = row.RootTaskId,
                    Status = status,
                    Title = row.Title ?? "Заказ-наряд",
                    StatusDisplay = status == ServiceDocumentStatus.Completed
                        ? "Документ закрыт"
                        : stage == ServiceDocumentClientStage.ReadyForPickup
                            ? ServiceDocumentClientStageLabels.ReadyForPickup
                            : "Документ в работе",
                    CreatedAt = row.CreatedAt,
                    CompletedAt = row.CompletedAt,
                    IsCurrentTaskRoot = row.RootTaskId == taskId,
                    ClientStage = stage,
                    ClientStageDisplay = ServiceDocumentClientStageLabels.ForUser(stage)
                };
            }
            catch (SqlException)
            {
                return null;
            }
        }

        public static Task TryCompleteForRootTaskAsync(DriveCareDBEntities db, Guid rootTaskId) =>
            ServiceDocumentClientStageService.TryMarkReadyForPickupAsync(db, rootTaskId);

        public static Task TryFinalizeForRootTaskAsync(DriveCareDBEntities db, Guid rootTaskId) =>
            ServiceDocumentClientStageService.TryFinalizeDocumentAsync(db, rootTaskId);

        public static Task TryFinalizeForRootTaskAsync(Guid rootTaskId) =>
            DatabaseExecutor.WithDbAsync(db => TryFinalizeForRootTaskAsync(db, rootTaskId));

        public static Task<List<TaskTreeNodeVm>> BuildTreeForTaskAsync(Guid taskId, Guid currentEmployeeId) =>
            BuildTreeForTaskChainAsync(taskId, currentEmployeeId);

        public static Task<List<TaskTreeNodeVm>> BuildTreeForDocumentAsync(Guid documentId, Guid currentEmployeeId) =>
            DatabaseExecutor.WithDbAsync(db => BuildTreeForDocumentAsync(db, documentId, currentEmployeeId));

        public static async Task<List<TaskTreeNodeVm>> BuildTreeForDocumentAsync(
            DriveCareDBEntities db,
            Guid documentId,
            Guid currentEmployeeId)
        {
            try
            {
                var doc = await db.Database.SqlQuery<DocumentRow>(
                    @"SELECT RowId, RootTaskId, Title, Status, CreatedAt, CompletedAt
                      FROM ServiceDocuments WHERE RowId = @p0", documentId).FirstOrDefaultAsync().ConfigureAwait(false);
                if (doc == null)
                    return new List<TaskTreeNodeVm>();

                var tasks = await db.Database.SqlQuery<TaskTreeRow>(
                    @"SELECT t.RowId, t.Title, t.EmployeeId, t.IsCompleted, t.ParentTaskId, t.DelegateTaskId
                      FROM Tasks t WHERE t.DocumentId = @p0", documentId).ToListAsync().ConfigureAwait(false);

                if (tasks.Count == 0)
                    return await BuildTreeForTaskChainAsync(db, doc.RootTaskId, currentEmployeeId)
                        .ConfigureAwait(false);

                return await BuildTreeFromTaskRowsAsync(db, tasks, doc.RootTaskId, currentEmployeeId)
                    .ConfigureAwait(false);
            }
            catch (SqlException)
            {
                return new List<TaskTreeNodeVm>();
            }
        }

        public static Task<List<TaskTreeNodeVm>> BuildTreeForTaskChainAsync(Guid taskId, Guid currentEmployeeId) =>
            DatabaseExecutor.WithDbAsync(db => BuildTreeForTaskChainAsync(db, taskId, currentEmployeeId));

        public static async Task<List<TaskTreeNodeVm>> BuildTreeForTaskChainAsync(
            DriveCareDBEntities db,
            Guid taskId,
            Guid currentEmployeeId)
        {
            var rootId = await FindRootTaskIdAsync(db, taskId).ConfigureAwait(false);
            var taskIds = await CollectFullDelegationChainIdsAsync(db, taskId).ConfigureAwait(false);
            if (taskIds.Count == 0)
                return new List<TaskTreeNodeVm>();

            var tasks = await LoadTaskTreeRowsAsync(db, taskIds).ConfigureAwait(false);
            return await BuildTreeFromTaskRowsAsync(db, tasks, rootId, currentEmployeeId).ConfigureAwait(false);
        }

        public static Task<List<TaskTreeNodeVm>> BuildForestForEmployeeAsync(Guid employeeId) =>
            DatabaseExecutor.WithDbAsync(db => BuildForestForEmployeeAsync(db, employeeId));

        public static async Task<List<TaskTreeNodeVm>> BuildForestForEmployeeAsync(
            DriveCareDBEntities db,
            Guid employeeId)
        {
            try
            {
                var docIds = await db.Database.SqlQuery<Guid>(
                    @"SELECT DISTINCT DocumentId FROM Tasks
                      WHERE EmployeeId = @p0 AND DocumentId IS NOT NULL AND IsCompleted = 0",
                    employeeId).ToListAsync().ConfigureAwait(false);

                var forest = new List<TaskTreeNodeVm>();
                foreach (var docId in docIds)
                {
                    var trees = await BuildTreeForDocumentAsync(db, docId, employeeId).ConfigureAwait(false);
                    forest.AddRange(trees);
                }

                return forest;
            }
            catch (SqlException)
            {
                return new List<TaskTreeNodeVm>();
            }
        }

        private static void SetDepth(TaskTreeNodeVm node, int depth)
        {
            node.Depth = depth;
            foreach (var child in node.Children)
                SetDepth(child, depth + 1);
        }

        private static int CountTreeNodes(IList<TaskTreeNodeVm> roots)
        {
            if (roots == null || roots.Count == 0)
                return 0;
            var n = 0;
            foreach (var r in roots)
                CountTreeNodesRecursive(r, ref n);
            return n;
        }

        private static void CountTreeNodesRecursive(TaskTreeNodeVm node, ref int count)
        {
            count++;
            foreach (var c in node.Children)
                CountTreeNodesRecursive(c, ref count);
        }

        private static async Task<Guid> FindRootTaskIdAsync(DriveCareDBEntities db, Guid taskId)
        {
            var walk = taskId;
            for (var i = 0; i < 64; i++)
            {
                var parentId = await TryGetParentTaskIdAsync(db, walk).ConfigureAwait(false);
                if (parentId.HasValue)
                {
                    walk = parentId.Value;
                    continue;
                }

                var ownerId = await TryGetOwnerTaskIdByDelegateAsync(db, walk).ConfigureAwait(false);
                if (ownerId.HasValue)
                {
                    walk = ownerId.Value;
                    continue;
                }

                return walk;
            }

            return walk;
        }

        private static async Task<List<TaskTreeNodeVm>> BuildMinimalTreeForTaskAsync(
            DriveCareDBEntities db,
            Guid taskId,
            Guid currentEmployeeId)
        {
            try
            {
                var t = await db.Tasks.AsNoTracking()
                    .Where(x => x.RowId == taskId)
                    .Select(x => new { x.RowId, x.Title, x.EmployeeId, x.IsCompleted })
                    .FirstOrDefaultAsync().ConfigureAwait(false);
                if (t == null)
                    return new List<TaskTreeNodeVm>();

                var emp = await db.Employees.AsNoTracking()
                    .FirstOrDefaultAsync(e => e.RowId == t.EmployeeId).ConfigureAwait(false);
                var empName = emp == null ? "—" : AppState.FormatEmployeeDisplayName(emp);

                return new List<TaskTreeNodeVm>
                {
                    new TaskTreeNodeVm
                    {
                        TaskId = t.RowId,
                        Title = FormatTreeTitle(t.Title),
                        EmployeeName = empName,
                        StatusDisplay = t.IsCompleted ? "Завершено" : "В работе",
                        IsCompleted = t.IsCompleted,
                        IsCurrentEmployeeTask = t.EmployeeId == currentEmployeeId,
                        IsRoot = true,
                        Depth = 0
                    }
                };
            }
            catch (SqlException)
            {
                return new List<TaskTreeNodeVm>();
            }
        }

        /// <summary>Все задания цепочки поручений (как в BuildChainTextAsync) + параллельные ветки от корня.</summary>
        private static async Task<List<Guid>> CollectDelegationChainTaskIdsAsync(DriveCareDBEntities db, Guid taskId)
        {
            var ids = new HashSet<Guid> { taskId };

            var walk = taskId;
            for (var i = 0; i < 64; i++)
            {
                var parentId = await TryGetParentTaskIdAsync(db, walk).ConfigureAwait(false);
                if (!parentId.HasValue)
                {
                    var ownerId = await TryGetOwnerTaskIdByDelegateAsync(db, walk).ConfigureAwait(false);
                    if (!ownerId.HasValue)
                        break;
                    parentId = ownerId;
                }

                if (!ids.Add(parentId.Value))
                    break;
                walk = parentId.Value;
            }

            walk = taskId;
            for (var i = 0; i < 64; i++)
            {
                var delegateId = await TryGetDelegateTaskIdAsync(db, walk).ConfigureAwait(false);
                if (!delegateId.HasValue)
                    break;
                if (!ids.Add(delegateId.Value))
                    break;
                walk = delegateId.Value;
            }

            var rootId = await FindRootTaskIdAsync(db, taskId).ConfigureAwait(false);
            foreach (var id in await CollectDelegationSubtreeIdsAsync(db, rootId).ConfigureAwait(false))
                ids.Add(id);

            return ids.ToList();
        }

        private static Task<List<Guid>> CollectFullDelegationChainIdsAsync(DriveCareDBEntities db, Guid taskId) =>
            CollectDelegationChainTaskIdsAsync(db, taskId);

        private static async Task<List<Guid>> CollectDelegationSubtreeIdsAsync(DriveCareDBEntities db, Guid rootTaskId)
        {
            try
            {
                var ids = new HashSet<Guid> { rootTaskId };
                var changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (var id in ids.ToList())
                    {
                        List<Guid> byParent;
                        try
                        {
                            byParent = await db.Database.SqlQuery<Guid>(
                                "SELECT RowId FROM Tasks WHERE ParentTaskId = @p0", id).ToListAsync().ConfigureAwait(false);
                        }
                        catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
                        {
                            byParent = new List<Guid>();
                        }

                        foreach (var childId in byParent)
                        {
                            if (ids.Add(childId))
                                changed = true;
                        }

                        var delegateId = await TryGetDelegateTaskIdAsync(db, id).ConfigureAwait(false);
                        if (delegateId.HasValue && ids.Add(delegateId.Value))
                            changed = true;
                    }
                }

                return ids.ToList();
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                return new List<Guid> { rootTaskId };
            }
        }

        private static async Task<List<TaskTreeRow>> LoadTaskTreeRowsAsync(DriveCareDBEntities db, IList<Guid> taskIds)
        {
            var rows = new List<TaskTreeRow>();
            if (taskIds == null || taskIds.Count == 0)
                return rows;

            foreach (var id in taskIds.Distinct())
            {
                TaskTreeRow row = null;
                try
                {
                    row = await db.Database.SqlQuery<TaskTreeRow>(
                        @"SELECT RowId, Title, EmployeeId, IsCompleted, ParentTaskId, DelegateTaskId
                          FROM Tasks WHERE RowId = @p0", id).FirstOrDefaultAsync().ConfigureAwait(false);
                }
                catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
                {
                }

                if (row == null)
                {
                    var t = await db.Tasks.AsNoTracking()
                        .Where(x => x.RowId == id)
                        .Select(x => new { x.RowId, x.Title, x.EmployeeId, x.IsCompleted })
                        .FirstOrDefaultAsync().ConfigureAwait(false);
                    if (t != null)
                    {
                        row = new TaskTreeRow
                        {
                            RowId = t.RowId,
                            Title = t.Title,
                            EmployeeId = t.EmployeeId,
                            IsCompleted = t.IsCompleted
                        };
                    }
                }

                if (row != null)
                {
                    if (!row.ParentTaskId.HasValue)
                        row.ParentTaskId = await TryGetParentTaskIdAsync(db, id).ConfigureAwait(false);
                    if (!row.DelegateTaskId.HasValue)
                        row.DelegateTaskId = await TryGetDelegateTaskIdAsync(db, id).ConfigureAwait(false);
                    rows.Add(row);
                }
            }

            return rows;
        }

        private static async Task<List<TaskTreeNodeVm>> BuildTreeFromTaskRowsAsync(
            DriveCareDBEntities db,
            List<TaskTreeRow> tasks,
            Guid rootTaskId,
            Guid currentEmployeeId)
        {
            if (tasks == null || tasks.Count == 0)
                return new List<TaskTreeNodeVm>();

            var empIds = tasks.Select(t => t.EmployeeId).Distinct().ToList();
            var empNames = await db.Employees.AsNoTracking()
                .Where(e => empIds.Contains(e.RowId))
                .ToListAsync().ConfigureAwait(false);
            var nameMap = empNames.ToDictionary(e => e.RowId, e => AppState.FormatEmployeeDisplayName(e));

            var taskById = tasks.ToDictionary(t => t.RowId);
            var nodeMap = tasks.ToDictionary(
                t => t.RowId,
                t => new TaskTreeNodeVm
                {
                    TaskId = t.RowId,
                    Title = FormatTreeTitle(t.Title),
                    EmployeeName = nameMap.TryGetValue(t.EmployeeId, out var n) ? n : "—",
                    StatusDisplay = t.IsCompleted ? "Завершено" : "В работе",
                    IsCompleted = t.IsCompleted,
                    IsCurrentEmployeeTask = t.EmployeeId == currentEmployeeId,
                    IsRoot = t.RowId == rootTaskId
                });

            var linked = new HashSet<Guid>();
            foreach (var t in tasks)
            {
                if (t.DelegateTaskId.HasValue && taskById.ContainsKey(t.DelegateTaskId.Value))
                {
                    var parent = nodeMap[t.RowId];
                    var child = nodeMap[t.DelegateTaskId.Value];
                    if (!parent.Children.Any(c => c.TaskId == child.TaskId))
                        parent.Children.Add(child);
                    linked.Add(t.DelegateTaskId.Value);
                }

                if (t.ParentTaskId.HasValue && taskById.ContainsKey(t.ParentTaskId.Value))
                {
                    var parent = nodeMap[t.ParentTaskId.Value];
                    var child = nodeMap[t.RowId];
                    if (!parent.Children.Any(c => c.TaskId == child.TaskId))
                        parent.Children.Add(child);
                    linked.Add(t.RowId);
                }
            }

            if (nodeMap.TryGetValue(rootTaskId, out var root))
            {
                await AppendDelegateChainRecursiveAsync(db, root, taskById, nodeMap, linked).ConfigureAwait(false);
                EnsureAllTasksAttachedUnderRoot(root, nodeMap, linked, rootTaskId);
                SetDepth(root, 0);
                return new List<TaskTreeNodeVm> { root };
            }

            var roots = nodeMap.Values.Where(n => !linked.Contains(n.TaskId)).ToList();
            foreach (var r in roots)
                SetDepth(r, 0);
            return roots;
        }

        /// <summary>Дочерние звенья по DelegateTaskId (как в цепочке поручений).</summary>
        private static async Task AppendDelegateChainRecursiveAsync(
            DriveCareDBEntities db,
            TaskTreeNodeVm parentVm,
            Dictionary<Guid, TaskTreeRow> taskById,
            Dictionary<Guid, TaskTreeNodeVm> nodeMap,
            HashSet<Guid> linked)
        {
            if (!taskById.TryGetValue(parentVm.TaskId, out var parentRow))
                return;

            var delegateId = parentRow.DelegateTaskId;
            if (!delegateId.HasValue)
                delegateId = await TryGetDelegateTaskIdAsync(db, parentVm.TaskId).ConfigureAwait(false);

            if (!delegateId.HasValue || !taskById.ContainsKey(delegateId.Value))
                return;

            if (parentVm.Children.Any(c => c.TaskId == delegateId.Value))
            {
                var existing = parentVm.Children.First(c => c.TaskId == delegateId.Value);
                await AppendDelegateChainRecursiveAsync(db, existing, taskById, nodeMap, linked).ConfigureAwait(false);
                return;
            }

            var childVm = nodeMap[delegateId.Value];
            parentVm.Children.Add(childVm);
            linked.Add(delegateId.Value);
            await AppendDelegateChainRecursiveAsync(db, childVm, taskById, nodeMap, linked).ConfigureAwait(false);
        }

        private static void EnsureAllTasksAttachedUnderRoot(
            TaskTreeNodeVm root,
            Dictionary<Guid, TaskTreeNodeVm> nodeMap,
            HashSet<Guid> linked,
            Guid rootTaskId)
        {
            var visited = new HashSet<Guid>();
            CollectVisited(root, visited);

            foreach (var kv in nodeMap)
            {
                if (kv.Key == rootTaskId || visited.Contains(kv.Key))
                    continue;

                if (!root.Children.Any(c => c.TaskId == kv.Key))
                    root.Children.Add(kv.Value);
            }
        }

        private static void CollectVisited(TaskTreeNodeVm node, HashSet<Guid> visited)
        {
            if (node == null || !visited.Add(node.TaskId))
                return;
            foreach (var child in node.Children)
                CollectVisited(child, visited);
        }

        private static string FormatTreeTitle(string title)
        {
            var t = (title ?? "Задание").Trim();
            return t.Length > 80 ? t.Substring(0, 77) + "…" : t;
        }

        private static async Task<List<Guid>> LoadTaskIdsForDocumentAsync(DriveCareDBEntities db, Guid documentId)
        {
            return await db.Database.SqlQuery<Guid>(
                "SELECT RowId FROM Tasks WHERE DocumentId = @p0", documentId).ToListAsync().ConfigureAwait(false);
        }

        private static async Task SaveDocumentReportAsync(
            DriveCareDBEntities db,
            Guid documentId,
            IList<TaskServiceLineRow> services,
            IList<TaskPartLineRow> parts,
            string freeTextNote)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM ServiceDocumentServiceLines WHERE DocumentId = @p0", documentId).ConfigureAwait(false);
                await db.Database.ExecuteSqlCommandAsync(
                    "DELETE FROM ServiceDocumentPartLines WHERE DocumentId = @p0", documentId).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return;
            }

            var sort = 0;
            foreach (var s in services)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO ServiceDocumentServiceLines
                      (RowId, DocumentId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount, SortOrder)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)",
                    Guid.NewGuid(), documentId,
                    (object)s.WorkshopServiceId ?? DBNull.Value,
                    s.ServiceName.Trim(), s.Quantity, s.UnitName ?? "усл.", s.UnitPrice, s.DiscountPercent, s.LineAmount, sort++)
                    .ConfigureAwait(false);
            }

            sort = 0;
            foreach (var p in parts)
            {
                await InsertDocumentPartLineAsync(db, documentId, p, sort++).ConfigureAwait(false);
            }

            var report = TaskReportService.BuildReportText(services, parts, freeTextNote);
            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE ServiceDocuments SET ReportText = @p1 WHERE RowId = @p0",
                documentId, (object)report ?? string.Empty).ConfigureAwait(false);
        }

        private static async Task InsertDocumentPartLineAsync(
            DriveCareDBEntities db,
            Guid documentId,
            TaskPartLineRow p,
            int sortOrder)
        {
            try
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO ServiceDocumentPartLines
                      (RowId, DocumentId, WorkshopPartId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8)",
                    Guid.NewGuid(), documentId,
                    (object)p.WorkshopPartId ?? DBNull.Value,
                    p.PartName.Trim(), p.Quantity, p.UnitName ?? "шт.", p.UnitPrice, p.LineAmount, sortOrder)
                    .ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207)
            {
                await db.Database.ExecuteSqlCommandAsync(
                    @"INSERT INTO ServiceDocumentPartLines
                      (RowId, DocumentId, PartName, Quantity, UnitName, UnitPrice, LineAmount, SortOrder)
                      VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7)",
                    Guid.NewGuid(), documentId,
                    p.PartName.Trim(), p.Quantity, p.UnitName ?? "шт.", p.UnitPrice, p.LineAmount, sortOrder)
                    .ConfigureAwait(false);
            }
        }

        private static List<TaskPartLineRow> MergePartLines(IEnumerable<TaskPartLineRow> lines)
        {
            var result = new List<TaskPartLineRow>();
            foreach (var inc in lines.Where(p => !string.IsNullOrWhiteSpace(p.PartName)))
            {
                var key = PartKey(inc);
                var existing = result.FirstOrDefault(p => PartKey(p) == key);
                if (existing == null)
                    result.Add(ClonePart(inc));
                else
                {
                    existing.Quantity += inc.Quantity;
                    if (inc.UnitPrice > 0)
                        existing.UnitPrice = inc.UnitPrice;
                    existing.RecalculateAmount();
                }
            }
            return result;
        }

        private static List<TaskServiceLineRow> MergeServiceLines(IEnumerable<TaskServiceLineRow> lines)
        {
            var result = new List<TaskServiceLineRow>();
            foreach (var inc in lines.Where(s => !string.IsNullOrWhiteSpace(s.ServiceName)))
            {
                var key = ServiceKey(inc);
                var existing = result.FirstOrDefault(s => ServiceKey(s) == key);
                if (existing == null)
                    result.Add(CloneService(inc));
                else
                {
                    existing.Quantity += inc.Quantity;
                    if (inc.UnitPrice > 0)
                        existing.UnitPrice = inc.UnitPrice;
                    existing.RecalculateAmount();
                }
            }
            return result;
        }

        private static string PartKey(TaskPartLineRow p)
        {
            if (p.WorkshopPartId.HasValue && p.WorkshopPartId.Value != Guid.Empty)
                return "id:" + p.WorkshopPartId.Value.ToString("N");
            return "n:" + (p.PartName ?? "").Trim().ToLowerInvariant();
        }

        private static string ServiceKey(TaskServiceLineRow s)
        {
            if (s.WorkshopServiceId.HasValue && s.WorkshopServiceId.Value != Guid.Empty)
                return "id:" + s.WorkshopServiceId.Value.ToString("N");
            return "n:" + (s.ServiceName ?? "").Trim().ToLowerInvariant();
        }

        private static TaskPartLineRow ClonePart(TaskPartLineRow p) => new TaskPartLineRow
        {
            WorkshopPartId = p.WorkshopPartId,
            PartName = p.PartName,
            Quantity = p.Quantity,
            UnitName = p.UnitName,
            UnitPrice = p.UnitPrice,
            LineAmount = p.LineAmount
        };

        private static TaskServiceLineRow CloneService(TaskServiceLineRow s) => new TaskServiceLineRow
        {
            WorkshopServiceId = s.WorkshopServiceId,
            ServiceName = s.ServiceName,
            Quantity = s.Quantity,
            UnitName = s.UnitName,
            UnitPrice = s.UnitPrice,
            DiscountPercent = s.DiscountPercent,
            LineAmount = s.LineAmount
        };

        private sealed class DocumentRow
        {
            public Guid RowId { get; set; }
            public Guid RootTaskId { get; set; }
            public string Title { get; set; }
            public byte Status { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
        }

        private sealed class TaskTreeRow
        {
            public Guid RowId { get; set; }
            public string Title { get; set; }
            public Guid EmployeeId { get; set; }
            public bool IsCompleted { get; set; }
            public Guid? ParentTaskId { get; set; }
            public Guid? DelegateTaskId { get; set; }
        }
    }
}
