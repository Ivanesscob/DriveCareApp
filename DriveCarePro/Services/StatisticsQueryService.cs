using DriveCareCore.Analytics;
using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DriveCarePro.Services
{
    public sealed class ActivitySummaryItem
    {
        public string EventCode { get; set; }
        public string Title { get; set; }
        public int Count { get; set; }
    }

    public sealed class ActivityEventListItem
    {
        public DateTime CreatedAt { get; set; }
        public string EventCode { get; set; }
        public string Title { get; set; }
        public string ActorLabel { get; set; }
        public string EntityLabel { get; set; }
        public string PayloadJson { get; set; }
    }

    public sealed class EntityViewStatItem
    {
        public Guid EntityId { get; set; }
        public string Label { get; set; }
        public int ViewCount { get; set; }
    }

    public sealed class StatisticsDashboardVm
    {
        public bool TableMissing { get; set; }
        public string ScopeHint { get; set; }
        public List<ActivitySummaryItem> Summary { get; set; } = new List<ActivitySummaryItem>();
        public List<ActivityEventListItem> RecentEvents { get; set; } = new List<ActivityEventListItem>();
        public List<EntityViewStatItem> TopCarSaleViews { get; set; } = new List<EntityViewStatItem>();
        public int TotalEvents { get; set; }
    }

    public static class StatisticsQueryService
    {
        public static Task<StatisticsDashboardVm> LoadAsync(
            int days,
            bool platformWide,
            Guid? companyId,
            IReadOnlyList<Guid> workshopIds)
        {
            return DatabaseExecutor.WithDbAsync(db => LoadCoreAsync(db, days, platformWide, companyId, workshopIds));
        }

        private static async Task<StatisticsDashboardVm> LoadCoreAsync(
            DriveCareDBEntities db,
            int days,
            bool platformWide,
            Guid? companyId,
            IReadOnlyList<Guid> workshopIds)
        {
            var vm = new StatisticsDashboardVm();
            if (!await TableExistsAsync(db).ConfigureAwait(false))
            {
                vm.TableMissing = true;
                vm.ScopeHint = "Таблица AppActivityEvents не найдена. Выполните SQL: DriveCareCore\\Data\\BD\\Sql\\AppActivityEvents_Tables.sql";
                return vm;
            }

            vm.ScopeHint = platformWide
                ? "Статистика по всей платформе"
                : "Статистика по вашей организации";

            var since = days <= 0 ? (DateTime?)null : DateTime.UtcNow.AddDays(-days);
            var scopeSql = BuildScopeSql(platformWide, companyId, workshopIds);
            var dateSql = since.HasValue ? " AND e.CreatedAt >= @p0" : string.Empty;
            var dateParam = since.HasValue ? new object[] { since.Value } : Array.Empty<object>();

            var summarySql = $@"
SELECT e.EventCode AS EventCode, COUNT(*) AS Count
FROM dbo.AppActivityEvents e
WHERE 1=1{dateSql}{scopeSql}
GROUP BY e.EventCode
ORDER BY COUNT(*) DESC";

            var summaryRows = await db.Database.SqlQuery<SummarySqlRow>(summarySql, dateParam)
                .ToListAsync()
                .ConfigureAwait(false);

            vm.Summary = summaryRows.Select(r => new ActivitySummaryItem
            {
                EventCode = r.EventCode,
                Title = ActivityEventCodes.GetTitle(r.EventCode),
                Count = r.Count
            }).ToList();
            vm.TotalEvents = vm.Summary.Sum(s => s.Count);

            var recentSql = $@"
SELECT TOP 200
    e.CreatedAt,
    e.EventCode,
    e.ActorKind,
    e.UserId,
    e.EmployeeId,
    e.EntityType,
    e.EntityId,
    e.PayloadJson
FROM dbo.AppActivityEvents e
WHERE 1=1{dateSql}{scopeSql}
ORDER BY e.CreatedAt DESC";

            var recentRows = await db.Database.SqlQuery<RecentSqlRow>(recentSql, dateParam)
                .ToListAsync()
                .ConfigureAwait(false);
            vm.RecentEvents = recentRows.Select(MapRecent).ToList();

            var topSql = $@"
SELECT TOP 15
    e.EntityId,
    COUNT(*) AS ViewCount
FROM dbo.AppActivityEvents e
WHERE e.EventCode = N'{ActivityEventCodes.CarSaleDetailView}'
  AND e.EntityType = N'CarSale'
  AND e.EntityId IS NOT NULL{dateSql}{scopeSql}
GROUP BY e.EntityId
ORDER BY COUNT(*) DESC";

            var topRows = await db.Database.SqlQuery<TopEntitySqlRow>(topSql, dateParam)
                .ToListAsync()
                .ConfigureAwait(false);
            vm.TopCarSaleViews = await EnrichCarSaleLabelsAsync(db, topRows).ConfigureAwait(false);
            return vm;
        }

        private static async Task<List<EntityViewStatItem>> EnrichCarSaleLabelsAsync(
            DriveCareDBEntities db,
            List<TopEntitySqlRow> rows)
        {
            var list = new List<EntityViewStatItem>();
            if (rows == null || rows.Count == 0)
                return list;

            var ids = rows.Select(r => r.EntityId).Distinct().ToList();
            var sales = await db.CarSales.AsNoTracking()
                .Where(s => ids.Contains(s.RowId))
                .Select(s => new { s.RowId, s.Title })
                .ToListAsync()
                .ConfigureAwait(false);

            var priceRows = await db.CarSalePrices.AsNoTracking()
                .Where(p => ids.Contains(p.CarSaleId))
                .ToListAsync()
                .ConfigureAwait(false);

            var latestPrices = priceRows
                .GroupBy(p => p.CarSaleId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.StartDate).First().Price);

            foreach (var row in rows)
            {
                var sale = sales.FirstOrDefault(s => s.RowId == row.EntityId);
                latestPrices.TryGetValue(row.EntityId, out var price);
                var title = sale?.Title?.Trim();
                string label;
                if (sale == null)
                    label = "Объявление " + row.EntityId.ToString().Substring(0, 8) + "…";
                else if (!string.IsNullOrEmpty(title))
                    label = title + (price > 0 ? " · " + price.ToString("N0") + " ₽" : "");
                else
                    label = "Объявление · " + (price > 0 ? price.ToString("N0") + " ₽ · " : "")
                              + row.EntityId.ToString().Substring(0, 8) + "…";
                list.Add(new EntityViewStatItem
                {
                    EntityId = row.EntityId,
                    Label = label,
                    ViewCount = row.ViewCount
                });
            }

            return list;
        }

        private static ActivityEventListItem MapRecent(RecentSqlRow row)
        {
            var actor = row.ActorKind == ActivityActorKind.Employee
                ? "Сотрудник " + ShortId(row.EmployeeId)
                : row.ActorKind == ActivityActorKind.User
                    ? "Пользователь " + ShortId(row.UserId)
                    : "Система";

            var entity = string.IsNullOrWhiteSpace(row.EntityType)
                ? "—"
                : row.EntityType + (row.EntityId.HasValue ? " " + ShortId(row.EntityId) : string.Empty);

            return new ActivityEventListItem
            {
                CreatedAt = row.CreatedAt.ToLocalTime(),
                EventCode = row.EventCode,
                Title = ActivityEventCodes.GetTitle(row.EventCode),
                ActorLabel = actor,
                EntityLabel = entity,
                PayloadJson = row.PayloadJson
            };
        }

        private static string ShortId(Guid? id) =>
            !id.HasValue || id.Value == Guid.Empty ? "—" : id.Value.ToString().Substring(0, 8);

        private static string BuildScopeSql(bool platformWide, Guid? companyId, IReadOnlyList<Guid> workshopIds)
        {
            if (platformWide)
                return string.Empty;

            var parts = new List<string>();
            if (companyId.HasValue && companyId.Value != Guid.Empty)
                parts.Add("e.CompanyId = '" + companyId.Value + "'");

            if (workshopIds != null)
            {
                foreach (var wid in workshopIds.Where(id => id != Guid.Empty).Distinct())
                    parts.Add("e.WorkshopId = '" + wid + "'");
            }

            if (parts.Count == 0)
                return " AND 1=0";

            return " AND (" + string.Join(" OR ", parts) + ")";
        }

        private static async Task<bool> TableExistsAsync(DriveCareDBEntities db)
        {
            var v = await db.Database.SqlQuery<int>(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.AppActivityEvents', N'U') IS NOT NULL THEN 1 ELSE 0 END")
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);
            return v == 1;
        }

        private sealed class SummarySqlRow
        {
            public string EventCode { get; set; }
            public int Count { get; set; }
        }

        private sealed class RecentSqlRow
        {
            public DateTime CreatedAt { get; set; }
            public string EventCode { get; set; }
            public byte ActorKind { get; set; }
            public Guid? UserId { get; set; }
            public Guid? EmployeeId { get; set; }
            public string EntityType { get; set; }
            public Guid? EntityId { get; set; }
            public string PayloadJson { get; set; }
        }

        private sealed class TopEntitySqlRow
        {
            public Guid EntityId { get; set; }
            public int ViewCount { get; set; }
        }
    }
}
