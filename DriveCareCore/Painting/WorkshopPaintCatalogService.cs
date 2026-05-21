using DriveCareCore.Bookings;
using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace DriveCareCore.Painting
{
    public static class WorkshopPaintCatalogService
    {
        public static bool TablesExist()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.WorkshopPaintServices', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static List<WorkshopPaintServiceOffer> LoadServicesForWorkshop(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return GetDefaultServices(Guid.Empty);

            if (!TablesExist())
                return GetDefaultServices(workshopId);

            try
            {
                const string sql = @"
SELECT RowId, WorkshopId, PaintKind, Name, Description, PriceFrom
FROM WorkshopPaintServices
WHERE WorkshopId = @p0 AND IsActive = 1
ORDER BY SortOrder, Name";

                var rows = AppConnect.model1.Database
                    .SqlQuery<ServiceSqlRow>(sql, workshopId)
                    .ToList();

                if (rows.Count == 0)
                    return GetDefaultServices(workshopId);

                return rows.Select(MapService).ToList();
            }
            catch
            {
                return GetDefaultServices(workshopId);
            }
        }

        public static List<WorkshopPaintColorOffer> LoadColorsForWorkshop(Guid workshopId)
        {
            var result = new List<WorkshopPaintColorOffer>();

            if (TablesExist() && workshopId != Guid.Empty)
            {
                try
                {
                    const string sql = @"
SELECT wpc.RowId, wpc.ColorId, wpc.ColorName
FROM WorkshopPaintColors wpc
WHERE wpc.WorkshopId = @p0 AND wpc.IsActive = 1
ORDER BY wpc.SortOrder, wpc.ColorName";

                    var rows = AppConnect.model1.Database
                        .SqlQuery<ColorSqlRow>(sql, workshopId)
                        .ToList();

                    foreach (var row in rows)
                    {
                        if (string.IsNullOrWhiteSpace(row.ColorName))
                            continue;
                        result.Add(new WorkshopPaintColorOffer
                        {
                            RowId = row.RowId,
                            ColorId = row.ColorId,
                            ColorName = row.ColorName.Trim()
                        });
                    }
                }
                catch
                {
                }
            }

            if (result.Count > 0)
                return result;

            try
            {
                return AppConnect.model1.Colors
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Take(30)
                    .Select(c => new WorkshopPaintColorOffer
                    {
                        RowId = c.RowId,
                        ColorId = c.RowId,
                        ColorName = c.Name
                    })
                    .ToList();
            }
            catch
            {
                return new List<WorkshopPaintColorOffer>
                {
                    new WorkshopPaintColorOffer { ColorName = "Чёрный" },
                    new WorkshopPaintColorOffer { ColorName = "Белый" },
                    new WorkshopPaintColorOffer { ColorName = "Серебристый" },
                    new WorkshopPaintColorOffer { ColorName = "Синий" },
                    new WorkshopPaintColorOffer { ColorName = "Красный" }
                };
            }
        }

        public static WorkshopPaintShopDetail LoadShopDetail(Guid workshopId)
        {
            if (workshopId == Guid.Empty)
                return null;

            try
            {
                var d = WorkshopOnlineBookingService.LoadWorkshopDetailAsync(workshopId)
                    .GetAwaiter().GetResult();
                if (d == null)
                    return null;

                return new WorkshopPaintShopDetail
                {
                    WorkshopId = workshopId,
                    WorkshopName = d.WorkshopName,
                    CompanyName = d.CompanyName,
                    AddressLine = d.AddressLine,
                    Phone = d.Phone,
                    Description = d.Description
                };
            }
            catch
            {
                return null;
            }
        }

        public static List<WorkshopPaintColorOffer> LoadManageColorsForWorkshop(Guid workshopId)
        {
            if (workshopId == Guid.Empty || !TablesExist())
                return new List<WorkshopPaintColorOffer>();

            try
            {
                const string sql = @"
SELECT wpc.RowId, wpc.ColorId, wpc.ColorName
FROM WorkshopPaintColors wpc
WHERE wpc.WorkshopId = @p0 AND wpc.IsActive = 1
ORDER BY wpc.SortOrder, wpc.ColorName";

                return AppConnect.model1.Database.SqlQuery<ColorSqlRow>(sql, workshopId)
                    .ToList()
                    .Where(r => !string.IsNullOrWhiteSpace(r.ColorName))
                    .Select(r => new WorkshopPaintColorOffer
                    {
                        RowId = r.RowId,
                        ColorId = r.ColorId,
                        ColorName = r.ColorName.Trim()
                    })
                    .ToList();
            }
            catch
            {
                return new List<WorkshopPaintColorOffer>();
            }
        }

        public static List<WorkshopPaintServiceOffer> LoadManageServicesForWorkshop(Guid workshopId)
        {
            if (workshopId == Guid.Empty || !TablesExist())
                return new List<WorkshopPaintServiceOffer>();

            try
            {
                const string sql = @"
SELECT RowId, WorkshopId, PaintKind, Name, Description, PriceFrom
FROM WorkshopPaintServices
WHERE WorkshopId = @p0 AND IsActive = 1
ORDER BY SortOrder, Name";

                return AppConnect.model1.Database.SqlQuery<ServiceSqlRow>(sql, workshopId)
                    .Select(MapService)
                    .ToList();
            }
            catch
            {
                return new List<WorkshopPaintServiceOffer>();
            }
        }

        public static (bool ok, string error) AddColor(Guid workshopId, string colorName)
        {
            if (workshopId == Guid.Empty)
                return (false, "Мастерская не указана.");
            var name = (colorName ?? string.Empty).Trim();
            if (name.Length == 0)
                return (false, "Введите название цвета.");
            if (!TablesExist())
                return (false, "Выполните SQL WorkshopPaintServices_Tables.sql");

            try
            {
                Guid? colorId = null;
                var existing = AppConnect.model1.Colors.FirstOrDefault(c => c.Name == name);
                if (existing != null)
                    colorId = existing.RowId;

                const string sql = @"
INSERT INTO WorkshopPaintColors (RowId, WorkshopId, ColorId, ColorName, IsActive, SortOrder, CreatedAt)
VALUES (@p0, @p1, @p2, @p3, 1, 0, SYSUTCDATETIME())";

                AppConnect.model1.Database.ExecuteSqlCommand(
                    sql,
                    Guid.NewGuid(),
                    workshopId,
                    colorId.HasValue ? (object)colorId.Value : DBNull.Value,
                    name);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string error) DeactivateColor(Guid rowId)
        {
            if (rowId == Guid.Empty || !TablesExist())
                return (false, "Запись не найдена.");
            try
            {
                AppConnect.model1.Database.ExecuteSqlCommand(
                    @"UPDATE WorkshopPaintColors SET IsActive = 0 WHERE RowId = @p0;",
                    rowId);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string error) AddService(
            Guid workshopId,
            CarPaintKind paintKind,
            string name,
            string description,
            decimal? priceFrom)
        {
            if (workshopId == Guid.Empty)
                return (false, "Мастерская не указана.");
            var n = (name ?? string.Empty).Trim();
            if (n.Length == 0)
                return (false, "Введите название услуги.");
            if (!TablesExist())
                return (false, "Выполните SQL WorkshopPaintServices_Tables.sql");

            try
            {
                const string sql = @"
INSERT INTO WorkshopPaintServices (RowId, WorkshopId, PaintKind, Name, Description, PriceFrom, IsActive, SortOrder, CreatedAt)
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, 1, 0, SYSUTCDATETIME())";

                AppConnect.model1.Database.ExecuteSqlCommand(
                    sql,
                    Guid.NewGuid(),
                    workshopId,
                    (byte)paintKind,
                    n,
                    string.IsNullOrWhiteSpace(description) ? (object)DBNull.Value : description.Trim(),
                    priceFrom.HasValue && priceFrom.Value > 0 ? (object)priceFrom.Value : DBNull.Value);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string error) DeactivateService(Guid rowId)
        {
            if (rowId == Guid.Empty || !TablesExist())
                return (false, "Запись не найдена.");
            try
            {
                AppConnect.model1.Database.ExecuteSqlCommand(
                    @"UPDATE WorkshopPaintServices SET IsActive = 0 WHERE RowId = @p0;",
                    rowId);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static (bool ok, string error, Guid? inquiryId) CreateInquiry(
            Guid userId,
            Guid userCarId,
            Guid carId,
            Guid workshopId,
            Guid? workshopPaintServiceId,
            CarPaintKind paintKind,
            Guid? colorId,
            string colorName,
            string partName,
            string notes)
        {
            if (userId == Guid.Empty || userCarId == Guid.Empty || workshopId == Guid.Empty)
                return (false, "Не заполнены обязательные данные.", null);

            var name = (colorName ?? string.Empty).Trim();
            if (name.Length == 0)
                return (false, "Выберите цвет.", null);

            if (paintKind == CarPaintKind.Part && string.IsNullOrWhiteSpace(partName))
                return (false, "Укажите название детали.", null);

            if (!TablesExist())
                return (false, "Таблицы покраски не созданы. Выполните SQL WorkshopPaintServices_Tables.sql", null);

            try
            {
                var id = Guid.NewGuid();
                const string sql = @"
INSERT INTO UserWorkshopPaintInquiries (
    RowId, UserId, UserCarId, CarId, WorkshopId, WorkshopPaintServiceId,
    PaintKind, ColorId, ColorName, PartName, Notes, StatusCode, CreatedAt)
VALUES (
    @p0, @p1, @p2, @p3, @p4, @p5,
    @p6, @p7, @p8, @p9, @p10, 0, SYSUTCDATETIME())";

                AppConnect.model1.Database.ExecuteSqlCommand(
                    sql,
                    id,
                    userId,
                    userCarId,
                    carId,
                    workshopId,
                    workshopPaintServiceId.HasValue && workshopPaintServiceId.Value != Guid.Empty
                        ? (object)workshopPaintServiceId.Value
                        : DBNull.Value,
                    (byte)paintKind,
                    colorId.HasValue && colorId.Value != Guid.Empty ? (object)colorId.Value : DBNull.Value,
                    name,
                    string.IsNullOrWhiteSpace(partName) ? (object)DBNull.Value : partName.Trim(),
                    string.IsNullOrWhiteSpace(notes) ? (object)DBNull.Value : notes.Trim());

                return (true, null, id);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        private static List<WorkshopPaintServiceOffer> GetDefaultServices(Guid workshopId)
        {
            return new List<WorkshopPaintServiceOffer>
            {
                new WorkshopPaintServiceOffer
                {
                    RowId = Guid.Empty,
                    WorkshopId = workshopId,
                    PaintKind = CarPaintKind.Wheels,
                    Name = "Покраска дисков",
                    Description = "Колёса и диски"
                },
                new WorkshopPaintServiceOffer
                {
                    RowId = Guid.Empty,
                    WorkshopId = workshopId,
                    PaintKind = CarPaintKind.FullCar,
                    Name = "Покраска всей машины",
                    Description = "Полная перекраска кузова"
                },
                new WorkshopPaintServiceOffer
                {
                    RowId = Guid.Empty,
                    WorkshopId = workshopId,
                    PaintKind = CarPaintKind.Part,
                    Name = "Перекраска детали",
                    Description = "Локальная покраска элемента кузова"
                }
            };
        }

        private static WorkshopPaintServiceOffer MapService(ServiceSqlRow row)
        {
            var kind = (CarPaintKind)row.PaintKind;
            var price = row.PriceFrom.HasValue && row.PriceFrom.Value > 0
                ? $"от {row.PriceFrom.Value:N0} ₽"
                : null;

            return new WorkshopPaintServiceOffer
            {
                RowId = row.RowId,
                WorkshopId = row.WorkshopId,
                PaintKind = kind,
                Name = row.Name ?? CarPaintService.GetKindTitle(kind),
                Description = row.Description,
                PriceFrom = row.PriceFrom,
                PriceDisplay = price
            };
        }

        private sealed class ServiceSqlRow
        {
            public Guid RowId { get; set; }
            public Guid WorkshopId { get; set; }
            public byte PaintKind { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public decimal? PriceFrom { get; set; }
        }

        private sealed class ColorSqlRow
        {
            public Guid RowId { get; set; }
            public Guid? ColorId { get; set; }
            public string ColorName { get; set; }
        }
    }
}
