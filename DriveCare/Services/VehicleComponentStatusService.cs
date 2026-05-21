using DriveCare.Data;
using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;

namespace DriveCare.Services
{
    public sealed class VehicleComponentStatusVm
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public int SortOrder { get; set; }
        /// <summary>Good, Watch, Due, Unknown.</summary>
        public string StatusLevel { get; set; }
        public string StatusLabel { get; set; }
        public string Hint { get; set; }
        public bool HasHistory { get; set; }
    }

    public static class VehicleComponentStatusService
    {
        sealed class ComponentDef
        {
            public string Code;
            public string Name;
            public int Sort;
            public int IntervalKm;
            public string[] Keys;
        }

        static readonly ComponentDef[] Components =
        {
            new ComponentDef { Code = "service", Name = "Плановое ТО", Sort = 1, IntervalKm = 15_000,
                Keys = new[] { "т.о.", "техобслуж", "планов", "регламент", "осмотр", "диагностик", "сервисн" } },
            new ComponentDef { Code = "oil", Name = "Моторное масло", Sort = 2, IntervalKm = 10_000,
                Keys = new[] { "масло", "маслян", "oil" } },
            new ComponentDef { Code = "filters", Name = "Фильтры", Sort = 3, IntervalKm = 20_000,
                Keys = new[] { "фильтр", "салон", "воздуш" } },
            new ComponentDef { Code = "brakes", Name = "Тормоза", Sort = 4, IntervalKm = 80_000,
                Keys = new[] { "тормоз", "колодк", "диск", "суппорт" } },
            new ComponentDef { Code = "timing", Name = "ГРМ / ремень", Sort = 5, IntervalKm = 60_000,
                Keys = new[] { "грм", "ремень", "цепь", "помпа", "ролик" } },
            new ComponentDef { Code = "tires", Name = "Шины", Sort = 6, IntervalKm = 40_000,
                Keys = new[] { "шин", "колес", "резин", "баланс", "развал" } },
            new ComponentDef { Code = "battery", Name = "Аккумулятор", Sort = 7, IntervalKm = 50_000,
                Keys = new[] { "акб", "аккум", "батаре", "акум" } },
            new ComponentDef { Code = "coolant", Name = "Охлаждение", Sort = 8, IntervalKm = 60_000,
                Keys = new[] { "антифриз", "охлажд", "тосол", "радиатор" } }
        };

        public static bool StatusTableExists()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN OBJECT_ID(N'dbo.UserCarComponentStatuses', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static IReadOnlyList<VehicleComponentStatusVm> Build(
            Guid userCarRowId,
            IReadOnlyList<MaintenanceHistoryItemVm> historyNewestFirst,
            int? currentMileageKm)
        {
            var dbOverrides = LoadOverrides(userCarRowId);
            var list = new List<VehicleComponentStatusVm>();

            foreach (var def in Components.OrderBy(c => c.Sort))
            {
                if (dbOverrides.TryGetValue(def.Code, out var row))
                {
                    list.Add(MapOverride(def, row));
                    continue;
                }

                list.Add(Evaluate(def, historyNewestFirst, currentMileageKm));
            }

            return list;
        }

        public static (int good, int watch, int due, int unknown) CountByLevel(IReadOnlyList<VehicleComponentStatusVm> items)
        {
            int g = 0, w = 0, d = 0, u = 0;
            foreach (var i in items ?? Array.Empty<VehicleComponentStatusVm>())
            {
                switch (i.StatusLevel)
                {
                    case "Good": g++; break;
                    case "Watch": w++; break;
                    case "Due": d++; break;
                    default: u++; break;
                }
            }
            return (g, w, d, u);
        }

        public static void PersistComputed(Guid userCarRowId, IReadOnlyList<VehicleComponentStatusVm> items)
        {
            if (!StatusTableExists() || userCarRowId == Guid.Empty || items == null)
                return;

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    foreach (var item in items)
                    {
                        var level = LevelToByte(item.StatusLevel);
                        db.Database.ExecuteSqlCommand(@"
IF NOT EXISTS (SELECT 1 FROM dbo.UserCarComponentStatuses WHERE UserCarRowId = @w AND ComponentCode = @c)
    INSERT INTO dbo.UserCarComponentStatuses (RowId, UserCarRowId, ComponentCode, StatusLevel, ShortHint, UpdatedAt)
    VALUES (NEWID(), @w, @c, @s, @h, GETDATE());",
                            new SqlParameter("@w", userCarRowId),
                            new SqlParameter("@c", item.Code ?? string.Empty),
                            new SqlParameter("@s", level),
                            new SqlParameter("@h", (object)(item.Hint ?? string.Empty) ?? DBNull.Value));
                    }
                }
            }
            catch
            {
            }
        }

        static byte LevelToByte(string level)
        {
            switch (level)
            {
                case "Good": return 0;
                case "Watch": return 1;
                case "Due": return 2;
                default: return 3;
            }
        }

        public static string BuildOverallHint(int good, int watch, int due, int unknown)
        {
            if (due > 0)
                return $"Есть узлы, которые по пробегу и истории лучше проверить ({due} «заменить»). Остальное — ориентир, не диагноз.";
            if (watch > 0)
                return $"В целом можно ездить: {good} узлов в норме, {watch} — скоро на контроль.";
            if (good > 0 && unknown == 0)
                return "По имеющимся данным критичных пунктов нет — следите за регламентом в сервисной книжке.";
            if (unknown > 0)
                return "Части узлов без записей в истории — после визита в сервис картина станет точнее.";
            return "Добавьте пробег и записи в истории — появятся оценки по узлам.";
        }

        static Dictionary<string, StatusOverrideRow> LoadOverrides(Guid userCarRowId)
        {
            var map = new Dictionary<string, StatusOverrideRow>(StringComparer.OrdinalIgnoreCase);
            if (!StatusTableExists() || userCarRowId == Guid.Empty)
                return map;

            try
            {
                var rows = AppConnect.model1.Database.SqlQuery<StatusOverrideRow>(@"
SELECT ComponentCode, StatusLevel, LastServiceDate, LastMileageKm, RemainingKmHint, ShortHint
FROM dbo.UserCarComponentStatuses WHERE UserCarRowId = @p0;", userCarRowId).ToList();

                foreach (var r in rows)
                {
                    if (!string.IsNullOrWhiteSpace(r.ComponentCode))
                        map[r.ComponentCode.Trim()] = r;
                }
            }
            catch
            {
            }

            return map;
        }

        static VehicleComponentStatusVm MapOverride(ComponentDef def, StatusOverrideRow row)
        {
            var level = LevelFromByte(row.StatusLevel);
            return new VehicleComponentStatusVm
            {
                Code = def.Code,
                Name = def.Name,
                SortOrder = def.Sort,
                StatusLevel = level,
                StatusLabel = LabelForLevel(level, row.RemainingKmHint),
                Hint = string.IsNullOrWhiteSpace(row.ShortHint)
                    ? FormatLastVisit(row.LastServiceDate, row.LastMileageKm)
                    : row.ShortHint.Trim(),
                HasHistory = row.LastServiceDate.HasValue || row.LastMileageKm.HasValue
            };
        }

        static VehicleComponentStatusVm Evaluate(
            ComponentDef def,
            IReadOnlyList<MaintenanceHistoryItemVm> history,
            int? currentMileageKm)
        {
            var matches = (history ?? Array.Empty<MaintenanceHistoryItemVm>())
                .Where(h => Matches(def, h))
                .OrderByDescending(h => h.ServiceDate)
                .ThenByDescending(h => h.MileageKm ?? int.MinValue)
                .ToList();

            if (matches.Count == 0)
            {
                return new VehicleComponentStatusVm
                {
                    Code = def.Code,
                    Name = def.Name,
                    SortOrder = def.Sort,
                    StatusLevel = "Unknown",
                    StatusLabel = "Нет данных",
                    Hint = "В истории нет визитов по этому узлу",
                    HasHistory = false
                };
            }

            var last = matches[0];
            var lastKm = last.MileageKm;
            var hasSeverity = last.SeverityAfter.HasValue;
            if (hasSeverity)
            {
                var level = LevelFromByte(last.SeverityAfter.Value);
                return new VehicleComponentStatusVm
                {
                    Code = def.Code,
                    Name = def.Name,
                    SortOrder = def.Sort,
                    StatusLevel = level,
                    StatusLabel = LabelForLevel(level, null),
                    Hint = BuildHintFromEvent(last),
                    HasHistory = true
                };
            }

            if (!currentMileageKm.HasValue || !lastKm.HasValue)
            {
                return new VehicleComponentStatusVm
                {
                    Code = def.Code,
                    Name = def.Name,
                    SortOrder = def.Sort,
                    StatusLevel = "Watch",
                    StatusLabel = "Есть история",
                    Hint = $"Последний визит: {last.DateLabel}. Укажите пробег — оценим «скоро/пора».",
                    HasHistory = true
                };
            }

            var kmSince = currentMileageKm.Value - lastKm.Value;
            if (kmSince < 0)
            {
                return new VehicleComponentStatusVm
                {
                    Code = def.Code,
                    Name = def.Name,
                    SortOrder = def.Sort,
                    StatusLevel = "Watch",
                    StatusLabel = "Проверьте пробег",
                    Hint = "Пробег в записи больше текущего ориентира",
                    HasHistory = true
                };
            }

            var remaining = def.IntervalKm - kmSince;
            string level2;
            string label;
            if (remaining > 8_000)
            {
                level2 = "Good";
                label = "Можно ездить";
            }
            else if (remaining > 0)
            {
                level2 = "Watch";
                label = $"Скоро · ~{remaining:N0} км";
            }
            else
            {
                level2 = "Due";
                label = "Пора заменить";
            }

            return new VehicleComponentStatusVm
            {
                Code = def.Code,
                Name = def.Name,
                SortOrder = def.Sort,
                StatusLevel = level2,
                StatusLabel = label,
                Hint = $"После визита {last.DateLabel} прошло ~{kmSince:N0} км (ориентир {def.IntervalKm:N0} км)",
                HasHistory = true
            };
        }

        static bool Matches(ComponentDef def, MaintenanceHistoryItemVm h)
        {
            if (!string.IsNullOrWhiteSpace(h.ComponentCode) &&
                string.Equals(h.ComponentCode.Trim(), def.Code, StringComparison.OrdinalIgnoreCase))
                return true;

            var blob = $"{h.Title}\n{h.Notes}".ToLowerInvariant();
            return def.Keys.Any(k => blob.Contains(k));
        }

        static string BuildHintFromEvent(MaintenanceHistoryItemVm last)
        {
            var parts = new List<string> { last.WhenWhereLine };
            if (!string.IsNullOrWhiteSpace(last.WorkshopName))
                parts.Add(last.WorkshopName.Trim());
            if (!string.IsNullOrWhiteSpace(last.Title))
                parts.Add(last.Title.Trim());
            return string.Join(" · ", parts.Where(p => !string.IsNullOrEmpty(p)));
        }

        static string FormatLastVisit(DateTime? dt, int? km)
        {
            if (!dt.HasValue && !km.HasValue)
                return "Данные из сервиса";
            if (dt.HasValue && km.HasValue)
                return $"Визит {dt.Value:dd.MM.yyyy}, {km.Value:N0} км";
            if (dt.HasValue)
                return $"Визит {dt.Value:dd.MM.yyyy}";
            return $"{km.Value:N0} км";
        }

        static string LabelForLevel(string level, int? remainingKm)
        {
            switch (level)
            {
                case "Good": return "Норма";
                case "Watch":
                    return remainingKm.HasValue && remainingKm.Value > 0
                        ? $"Скоро · ~{remainingKm.Value:N0} км"
                        : "Скоро";
                case "Due": return "Заменить";
                default: return "Нет данных";
            }
        }

        static string LevelFromByte(byte? b)
        {
            switch (b)
            {
                case 0: return "Good";
                case 1: return "Watch";
                case 2: return "Due";
                default: return "Unknown";
            }
        }

        sealed class StatusOverrideRow
        {
            public string ComponentCode { get; set; }
            public byte StatusLevel { get; set; }
            public DateTime? LastServiceDate { get; set; }
            public int? LastMileageKm { get; set; }
            public int? RemainingKmHint { get; set; }
            public string ShortHint { get; set; }
        }
    }
}
