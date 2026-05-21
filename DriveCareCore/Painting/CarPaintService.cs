using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace DriveCareCore.Painting
{
    public static class CarPaintService
    {
        private static bool? _paintColumnsExist;

        public static bool PaintColumnsExist()
        {
            if (_paintColumnsExist.HasValue)
                return _paintColumnsExist.Value;
            try
            {
                const string sql = @"SELECT CASE WHEN COL_LENGTH(N'dbo.CarColors', N'PaintKind') IS NOT NULL THEN 1 ELSE 0 END;";
                _paintColumnsExist = AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                _paintColumnsExist = false;
            }

            return _paintColumnsExist.Value;
        }

        public static string GetKindTitle(CarPaintKind kind)
        {
            switch (kind)
            {
                case CarPaintKind.Wheels: return "Покраска дисков";
                case CarPaintKind.FullCar: return "Покраска всей машины";
                case CarPaintKind.Part: return "Перекраска детали";
                default: return "Покраска";
            }
        }

        public static List<CarPaintColorOption> LoadColorOptions()
        {
            try
            {
                return AppConnect.model1.Colors
                    .AsNoTracking()
                    .OrderBy(c => c.Name)
                    .Select(c => new CarPaintColorOption { ColorId = c.RowId, Name = c.Name })
                    .ToList();
            }
            catch
            {
                return new List<CarPaintColorOption>();
            }
        }

        public static CarPaintCurrentInfo GetCurrentBodyColor(Guid carId)
        {
            if (carId == Guid.Empty)
                return new CarPaintCurrentInfo { ColorName = "—" };

            try
            {
                if (PaintColumnsExist())
                {
                    const string sql = @"
SELECT TOP 1 col.Name AS ColorName, cc.StartDate AS Since
FROM CarColors cc
LEFT JOIN Colors col ON col.RowId = cc.ColorId
WHERE cc.CarId = @p0
  AND (cc.PaintKind IS NULL OR cc.PaintKind = 2)
  AND (cc.EndDate IS NULL OR cc.EndDate > GETUTCDATE())
ORDER BY cc.StartDate DESC";

                    var row = AppConnect.model1.Database
                        .SqlQuery<CurrentColorSqlRow>(sql, carId)
                        .FirstOrDefault();
                    if (row != null && !string.IsNullOrWhiteSpace(row.ColorName))
                        return new CarPaintCurrentInfo
                        {
                            ColorName = row.ColorName.Trim(),
                            Since = row.Since
                        };
                }

                const string fallbackSql = @"
SELECT TOP 1 col.Name AS ColorName, cc.StartDate AS Since
FROM CarColors cc
LEFT JOIN Colors col ON col.RowId = cc.ColorId
WHERE cc.CarId = @p0
ORDER BY cc.StartDate DESC";

                var fb = AppConnect.model1.Database
                    .SqlQuery<CurrentColorSqlRow>(fallbackSql, carId)
                    .FirstOrDefault();
                return new CarPaintCurrentInfo
                {
                    ColorName = string.IsNullOrWhiteSpace(fb?.ColorName) ? "Не указан" : fb.ColorName.Trim(),
                    Since = fb?.Since
                };
            }
            catch
            {
                return new CarPaintCurrentInfo { ColorName = "—" };
            }
        }

        public static List<CarPaintHistoryItem> LoadHistory(Guid carId)
        {
            if (carId == Guid.Empty)
                return new List<CarPaintHistoryItem>();

            try
            {
                if (PaintColumnsExist())
                {
                    const string sql = @"
SELECT
    cc.RowId,
    cc.StartDate,
    cc.EndDate,
    ISNULL(col.Name, N'') AS ColorName,
    cc.PaintKind,
    cc.PartName,
    cc.Description
FROM CarColors cc
LEFT JOIN Colors col ON col.RowId = cc.ColorId
WHERE cc.CarId = @p0
ORDER BY cc.StartDate DESC";

                    return AppConnect.model1.Database
                        .SqlQuery<HistorySqlRow>(sql, carId)
                        .ToList()
                        .Select(MapHistory)
                        .ToList();
                }

                const string legacySql = @"
SELECT
    cc.RowId,
    cc.StartDate,
    cc.EndDate,
    ISNULL(col.Name, N'') AS ColorName,
    CAST(NULL AS TINYINT) AS PaintKind,
    CAST(NULL AS NVARCHAR(200)) AS PartName,
    cc.Description
FROM CarColors cc
LEFT JOIN Colors col ON col.RowId = cc.ColorId
WHERE cc.CarId = @p0
ORDER BY cc.StartDate DESC";

                return AppConnect.model1.Database
                    .SqlQuery<HistorySqlRow>(legacySql, carId)
                    .ToList()
                    .Select(MapHistory)
                    .ToList();
            }
            catch
            {
                return new List<CarPaintHistoryItem>();
            }
        }

        public static (bool ok, string error) RecordPaint(
            Guid carId,
            CarPaintKind kind,
            Guid? colorId,
            string colorName,
            string partName,
            string notes)
        {
            if (carId == Guid.Empty)
                return (false, "Автомобиль не выбран.");

            if (!colorId.HasValue || colorId.Value == Guid.Empty)
            {
                var name = (colorName ?? string.Empty).Trim();
                if (name.Length == 0)
                    return (false, "Укажите цвет.");
            }

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    var resolvedColorId = ResolveColorId(db, colorId, colorName);
                    if (!resolvedColorId.HasValue)
                        return (false, "Не удалось определить цвет.");

                    var now = DateTime.UtcNow;
                    if (kind == CarPaintKind.FullCar)
                        CloseActiveBodyColors(db, carId, now);

                    var desc = BuildDescription(kind, partName, notes);
                    if (PaintColumnsExist())
                    {
                        var part = kind == CarPaintKind.Part ? (partName ?? string.Empty).Trim() : null;
                        if (kind == CarPaintKind.Part && string.IsNullOrWhiteSpace(part))
                            return (false, "Укажите название детали.");

                        const string insertSql = @"
INSERT INTO CarColors (RowId, CarId, ColorId, StartDate, EndDate, Description, PaintKind, PartName)
VALUES (@p0, @p1, @p2, @p3, NULL, @p4, @p5, @p6)";

                        db.Database.ExecuteSqlCommand(
                            insertSql,
                            Guid.NewGuid(),
                            carId,
                            resolvedColorId.Value,
                            now,
                            string.IsNullOrEmpty(desc) ? (object)DBNull.Value : desc,
                            (byte)kind,
                            string.IsNullOrWhiteSpace(part) ? (object)DBNull.Value : part);
                    }
                    else
                    {
                        if (kind == CarPaintKind.Part && string.IsNullOrWhiteSpace((partName ?? string.Empty).Trim()))
                            return (false, "Укажите название детали.");

                        var legacyDesc = BuildLegacyDescription(kind, partName, notes);
                        db.CarColors.Add(new CarColor
                        {
                            RowId = Guid.NewGuid(),
                            CarId = carId,
                            ColorId = resolvedColorId.Value,
                            StartDate = now,
                            EndDate = null,
                            Description = legacyDesc
                        });
                        db.SaveChanges();
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static void CloseActiveBodyColors(DriveCareDBEntities db, Guid carId, DateTime endUtc)
        {
            const string sql = @"
UPDATE CarColors
SET EndDate = @p0
WHERE CarId = @p1
  AND EndDate IS NULL
  AND (PaintKind IS NULL OR PaintKind = 2)";

            db.Database.ExecuteSqlCommand(sql, endUtc, carId);
        }

        private static Guid? ResolveColorId(DriveCareDBEntities db, Guid? colorId, string colorName)
        {
            if (colorId.HasValue && colorId.Value != Guid.Empty)
            {
                var exists = db.Colors.AsNoTracking().Any(c => c.RowId == colorId.Value);
                if (exists)
                    return colorId.Value;
            }

            var name = (colorName ?? string.Empty).Trim();
            if (name.Length == 0)
                return null;

            var existing = db.Colors.FirstOrDefault(c => c.Name == name);
            if (existing != null)
                return existing.RowId;

            var row = new Color
            {
                RowId = Guid.NewGuid(),
                Name = name,
                Description = null
            };
            db.Colors.Add(row);
            db.SaveChanges();
            return row.RowId;
        }

        private static string BuildDescription(CarPaintKind kind, string partName, string notes)
        {
            var note = (notes ?? string.Empty).Trim();
            if (note.Length > 0)
                return note;
            return null;
        }

        private static string BuildLegacyDescription(CarPaintKind kind, string partName, string notes)
        {
            var parts = new List<string> { GetKindTitle(kind) };
            var part = (partName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(part))
                parts.Add("Деталь: " + part);
            var note = (notes ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(note))
                parts.Add(note);
            return string.Join(" · ", parts);
        }

        private static CarPaintHistoryItem MapHistory(HistorySqlRow row)
        {
            CarPaintKind? kind = null;
            if (row.PaintKind.HasValue && Enum.IsDefined(typeof(CarPaintKind), row.PaintKind.Value))
                kind = (CarPaintKind)row.PaintKind.Value;

            return new CarPaintHistoryItem
            {
                RowId = row.RowId,
                StartDate = row.StartDate,
                EndDate = row.EndDate,
                ColorName = string.IsNullOrWhiteSpace(row.ColorName) ? "—" : row.ColorName.Trim(),
                PaintKind = kind,
                PartName = row.PartName,
                Description = row.Description
            };
        }

        private sealed class CurrentColorSqlRow
        {
            public string ColorName { get; set; }
            public DateTime? Since { get; set; }
        }

        private sealed class HistorySqlRow
        {
            public Guid RowId { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public string ColorName { get; set; }
            public byte? PaintKind { get; set; }
            public string PartName { get; set; }
            public string Description { get; set; }
        }
    }
}
