using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace DriveCareCore.Maps
{
    public enum WorkshopServiceKindCode
    {
        AutoService = 0,
        Painting = 1,
        TireService = 2,
        Other = 99
    }

    public sealed class WorkshopServiceKindItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public WorkshopServiceKindCode Code { get; set; }
    }

    public static class WorkshopServiceKinds
    {
        public static readonly Guid AutoServiceId = new Guid("DC010001-0001-4001-8001-000000000001");
        public static readonly Guid PaintingId = new Guid("DC010002-0002-4002-8002-000000000002");
        public static readonly Guid TireServiceId = new Guid("DC010003-0003-4003-8003-000000000003");

        public static IReadOnlyList<WorkshopServiceKindItem> All { get; } = new List<WorkshopServiceKindItem>
        {
            new WorkshopServiceKindItem { Id = AutoServiceId, Name = "Автосервис", Code = WorkshopServiceKindCode.AutoService },
            new WorkshopServiceKindItem { Id = PaintingId, Name = "Покраска", Code = WorkshopServiceKindCode.Painting },
            new WorkshopServiceKindItem { Id = TireServiceId, Name = "Шиномонтаж", Code = WorkshopServiceKindCode.TireService }
        };

        public static WorkshopServiceKindCode ResolveCode(Guid? businessTypeId, string businessTypeName)
        {
            if (businessTypeId.HasValue)
            {
                if (businessTypeId.Value == PaintingId)
                    return WorkshopServiceKindCode.Painting;
                if (businessTypeId.Value == TireServiceId)
                    return WorkshopServiceKindCode.TireService;
                if (businessTypeId.Value == AutoServiceId)
                    return WorkshopServiceKindCode.AutoService;
            }

            var n = (businessTypeName ?? string.Empty).Trim().ToLowerInvariant();
            if (n.Contains("покрас") || n.Contains("кузов"))
                return WorkshopServiceKindCode.Painting;
            if (n.Contains("шин") || n.Contains("колес"))
                return WorkshopServiceKindCode.TireService;
            if (n.Contains("авто") || n.Contains("сервис") || n.Contains("сто"))
                return WorkshopServiceKindCode.AutoService;
            return WorkshopServiceKindCode.Other;
        }

        public static string GetDisplayName(Guid? businessTypeId, string businessTypeName)
        {
            var item = All.FirstOrDefault(x => businessTypeId.HasValue && x.Id == businessTypeId.Value);
            if (item != null)
                return item.Name;
            if (!string.IsNullOrWhiteSpace(businessTypeName))
                return businessTypeName.Trim();
            return "Автосервис";
        }

        public static string GetYandexIconPreset(WorkshopServiceKindCode code)
        {
            switch (code)
            {
                case WorkshopServiceKindCode.Painting:
                    return "islands#orangeAutoIcon";
                case WorkshopServiceKindCode.TireService:
                    return "islands#darkGreenAutoIcon";
                case WorkshopServiceKindCode.AutoService:
                    return "islands#blueAutoIcon";
                default:
                    return "islands#grayAutoIcon";
            }
        }

        public static string BuildKindsLabel(IEnumerable<Guid> businessTypeIds)
        {
            if (businessTypeIds == null)
                return "Автосервис";

            var names = new List<string>();
            foreach (var id in businessTypeIds.Distinct())
            {
                var item = All.FirstOrDefault(x => x.Id == id);
                if (item != null && !names.Contains(item.Name))
                    names.Add(item.Name);
            }

            return names.Count > 0 ? string.Join(" · ", names) : "Автосервис";
        }

        public static WorkshopServiceKindCode ResolvePrimaryIconCode(IEnumerable<Guid> businessTypeIds)
        {
            if (businessTypeIds == null)
                return WorkshopServiceKindCode.Other;

            var codes = businessTypeIds.Select(id => ResolveCode(id, null)).Distinct().ToList();
            if (codes.Contains(WorkshopServiceKindCode.Painting))
                return WorkshopServiceKindCode.Painting;
            if (codes.Contains(WorkshopServiceKindCode.TireService))
                return WorkshopServiceKindCode.TireService;
            if (codes.Contains(WorkshopServiceKindCode.AutoService))
                return WorkshopServiceKindCode.AutoService;
            return WorkshopServiceKindCode.Other;
        }

        /// <summary>Создаёт в dbo.BusinessTypes три типа мастерской (если ещё нет) — нужно для FK заявок и junction.</summary>
        public static void EnsureCatalogInDatabase()
        {
            try
            {
                if (!TableExists())
                    return;

                foreach (var item in All)
                {
                    AppConnect.model1.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.BusinessTypes WHERE RowId = @p_id)
    INSERT INTO dbo.BusinessTypes (RowId, Name, Description)
    VALUES (@p_id, @p_name, @p_desc);",
                        new SqlParameter("@p_id", SqlDbType.UniqueIdentifier) { Value = item.Id },
                        new SqlParameter("@p_name", SqlDbType.NVarChar, 120) { Value = item.Name ?? string.Empty },
                        new SqlParameter("@p_desc", SqlDbType.NVarChar, 500) { Value = GetDescription(item.Code) });
                }
            }
            catch
            {
            }
        }

        /// <summary>Приводит выбранные Id к существующим в БД (фиксированный GUID или поиск по имени типа).</summary>
        public static List<Guid> ResolveBusinessTypeIdsForDatabase(IReadOnlyList<Guid> selectedIds)
        {
            EnsureCatalogInDatabase();
            if (selectedIds == null || selectedIds.Count == 0)
                return new List<Guid>();

            var result = new List<Guid>();
            foreach (var id in selectedIds.Where(x => x != Guid.Empty).Distinct())
            {
                var resolved = ResolveSingleBusinessTypeId(id);
                if (resolved != Guid.Empty && !result.Contains(resolved))
                    result.Add(resolved);
            }

            return result;
        }

        static Guid ResolveSingleBusinessTypeId(Guid typeId)
        {
            if (typeId == Guid.Empty)
                return Guid.Empty;

            try
            {
                var exists = AppConnect.model1.Database.SqlQuery<int>(
                    @"SELECT COUNT(1) FROM dbo.BusinessTypes WHERE RowId = @p0;",
                    new SqlParameter("@p0", SqlDbType.UniqueIdentifier) { Value = typeId }).FirstOrDefault();
                if (exists > 0)
                    return typeId;
            }
            catch
            {
            }

            var code = ResolveCode(typeId, null);
            var item = All.FirstOrDefault(x => x.Code == code);
            if (item == null)
                return typeId;

            try
            {
                var byName = AppConnect.model1.Database.SqlQuery<Guid?>(
                    @"SELECT TOP 1 RowId FROM dbo.BusinessTypes WHERE Name = @p0 ORDER BY RowId;",
                    new SqlParameter("@p0", SqlDbType.NVarChar, 120) { Value = item.Name }).FirstOrDefault();
                if (byName.HasValue && byName.Value != Guid.Empty)
                    return byName.Value;
            }
            catch
            {
            }

            return item.Id;
        }

        static bool TableExists()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.BusinessTypes', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        static string GetDescription(WorkshopServiceKindCode code)
        {
            switch (code)
            {
                case WorkshopServiceKindCode.Painting:
                    return "Кузовной ремонт и покраска";
                case WorkshopServiceKindCode.TireService:
                    return "Шины, диски, сезонное хранение";
                default:
                    return "Ремонт и обслуживание автомобилей";
            }
        }

        public static bool MatchesFilter(WorkshopMapPin pin, WorkshopServiceKindCode? filter)
        {
            if (!filter.HasValue)
                return true;
            if (pin == null)
                return false;

            if (pin.BusinessTypeIds != null && pin.BusinessTypeIds.Count > 0)
            {
                foreach (var id in pin.BusinessTypeIds)
                {
                    if (ResolveCode(id, null) == filter.Value)
                        return true;
                }
            }

            return ResolveCode(pin.BusinessTypeId, pin.ServiceKindName) == filter.Value;
        }
    }
}
