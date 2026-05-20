using DriveCare.Data;
using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;

namespace DriveCare.Services
{
    internal static class ServiceMaintenanceRepository
    {
        /// <summary>Таблица истории по привязке UserCars (основной источник для экрана обслуживания).</summary>
        public static bool TableExists()
        {
            try
            {
                const string sql = @"
SELECT CASE WHEN OBJECT_ID(N'dbo.UserCarMaintenanceHistory', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                var v = AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault();
                return v == 1;
            }
            catch
            {
                return false;
            }
        }

        private static bool RepairHistoryTableExists()
        {
            try
            {
                const string sql = @"
SELECT CASE WHEN OBJECT_ID(N'dbo.RepairHistory', N'U') IS NOT NULL THEN 1 ELSE 0 END;";
                var v = AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault();
                return v == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Объединяет UserCarMaintenanceHistory и RepairHistory (по CarId через UserCars), без дублей, новые сверху.
        /// </summary>
        public static IReadOnlyList<MaintenanceHistoryItemVm> LoadHistory(Guid userCarRowId)
        {
            var list = new List<MaintenanceHistoryItemVm>();

            try
            {
                if (TableExists())
                    list.AddRange(LoadUserCarMaintenance(userCarRowId));
            }
            catch
            {
            }

            try
            {
                if (RepairHistoryTableExists())
                {
                    foreach (var r in LoadRepairHistoryForUserCar(userCarRowId))
                    {
                        if (!list.Any(m => SameEvent(m, r)))
                            list.Add(r);
                    }
                }
            }
            catch
            {
            }

            return list
                .OrderByDescending(x => x.ServiceDate)
                .ThenByDescending(x => x.MileageKm ?? int.MinValue)
                .ToList();
        }

        private static bool SameEvent(MaintenanceHistoryItemVm a, MaintenanceHistoryItemVm b)
        {
            if (a.ServiceDate.Date != b.ServiceDate.Date)
                return false;
            if (a.MileageKm != b.MileageKm)
                return false;
            var ta = (a.Title ?? string.Empty).Trim();
            var tb = (b.Title ?? string.Empty).Trim();
            return string.Equals(ta, tb, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<MaintenanceHistoryItemVm> LoadUserCarMaintenance(Guid userCarRowId)
        {
            const string sql = @"
SELECT RowId, UserCarRowId, ServiceDate, MileageKm, Title, Notes
FROM dbo.UserCarMaintenanceHistory
WHERE UserCarRowId = @uc
ORDER BY ServiceDate DESC, MileageKm DESC;";

            var p = new SqlParameter("@uc", userCarRowId);
            var rows = AppConnect.model1.Database.SqlQuery<MaintenanceSqlRow>(sql, p).ToList();

            return rows.Select(MapRow).ToList();
        }

        private static IReadOnlyList<MaintenanceHistoryItemVm> LoadRepairHistoryForUserCar(Guid userCarRowId)
        {
            const string sql = @"
SELECT
    rh.RepairDate AS ServiceDate,
    rh.Mileage AS MileageKm,
    rh.Title,
    rh.Description AS Notes
FROM dbo.RepairHistory rh
INNER JOIN dbo.UserCars uc ON uc.CarId = rh.CarId AND uc.RowId = @uc
ORDER BY rh.RepairDate DESC, rh.Mileage DESC;";

            var p = new SqlParameter("@uc", userCarRowId);
            var rows = AppConnect.model1.Database.SqlQuery<RepairHistoryMaintenanceSqlRow>(sql, p).ToList();

            return rows.Select(r => new MaintenanceHistoryItemVm
            {
                ServiceDate = r.ServiceDate,
                MileageKm = r.MileageKm,
                Title = string.IsNullOrWhiteSpace(r.Title) ? "Ремонт" : r.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim()
            }).ToList();
        }

        private static MaintenanceHistoryItemVm MapRow(MaintenanceSqlRow r)
        {
            return new MaintenanceHistoryItemVm
            {
                ServiceDate = r.ServiceDate,
                MileageKm = r.MileageKm,
                Title = string.IsNullOrWhiteSpace(r.Title) ? "ТО" : r.Title.Trim(),
                Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim()
            };
        }

        /// <summary>Максимальный пробег из истории (UserCarMaintenanceHistory + RepairHistory).</summary>
        public static int GetMaxRecordedMileageKm(Guid userCarRowId)
        {
            var history = LoadHistory(userCarRowId);
            if (history.Count == 0)
                return 0;
            return history.Where(h => h.MileageKm.HasValue).Select(h => h.MileageKm.Value).DefaultIfEmpty(0).Max();
        }

        /// <summary>Запись реального пробега с одометра на сегодня.</summary>
        public static (bool ok, string error) TryInsertOdometerReading(Guid userCarRowId, int mileageKm)
        {
            if (userCarRowId == Guid.Empty)
                return (false, "Не выбран автомобиль.");
            if (mileageKm < 1)
                return (false, "Укажите пробег больше нуля.");
            if (!TableExists())
                return (false, "Таблица истории не найдена. Выполните SQL UserCarMaintenanceHistory_Tables.sql на сервере.");

            try
            {
                const string sql = @"
INSERT INTO dbo.UserCarMaintenanceHistory
    (RowId, UserCarRowId, ServiceDate, MileageKm, Title, Notes)
VALUES (@id, @uc, @dt, @km, N'Пробег с одометра', NULL);";

                AppConnect.model1.Database.ExecuteSqlCommand(
                    sql,
                    new SqlParameter("@id", Guid.NewGuid()),
                    new SqlParameter("@uc", userCarRowId),
                    new SqlParameter("@dt", DateTime.Now),
                    new SqlParameter("@km", mileageKm));

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
