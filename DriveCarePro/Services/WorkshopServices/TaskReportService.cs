using DriveCareCore.WorkOrders;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    internal static class TaskReportService
    {
        public static async Task<List<TaskServiceLineRow>> LoadServiceLinesAsync(Guid taskId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(db =>
                    db.Database.SqlQuery<TaskServiceLineRow>(
                        @"SELECT RowId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount
                          FROM TaskServiceLines WHERE TaskId = @p0 ORDER BY SortOrder",
                        taskId).ToListAsync()).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new List<TaskServiceLineRow>();
            }
        }

        public static async Task<List<TaskPartLineRow>> LoadPartLinesAsync(Guid taskId)
        {
            try
            {
                return await DatabaseExecutor.WithDbAsync(db => TaskPartLineSql.LoadAsync(db, taskId))
                    .ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new List<TaskPartLineRow>();
            }
        }

        public static Task<List<TaskServiceLineRow>> LoadServiceLinesAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId) =>
            LoadServiceLinesCoreAsync(db, taskId);

        public static Task<List<TaskPartLineRow>> LoadPartLinesAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId) =>
            TaskPartLineSql.LoadAsync(db, taskId);

        public static Task AppendPartLinesAsync(Guid taskId, IEnumerable<TaskPartLineRow> newLines) =>
            AppendPartLinesAsync(null, taskId, newLines);

        public static async Task AppendPartLinesAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId,
            IEnumerable<TaskPartLineRow> newLines)
        {
            var incoming = (newLines ?? Enumerable.Empty<TaskPartLineRow>())
                .Where(p => !string.IsNullOrWhiteSpace(p.PartName))
                .ToList();
            if (incoming.Count == 0)
                return;

            foreach (var p in incoming)
                p.RecalculateAmount();

            if (db != null)
            {
                var existing = await TaskPartLineSql.LoadAsync(db, taskId).ConfigureAwait(false);
                var services = await LoadServiceLinesCoreAsync(db, taskId).ConfigureAwait(false);
                var merged = existing.Concat(incoming).ToList();
                var report = await db.Database.SqlQuery<string>(
                    "SELECT ReportText FROM Tasks WHERE RowId = @p0", taskId).FirstOrDefaultAsync().ConfigureAwait(false);
                var freeNote = ExtractFreeTextFromReport(report, services, existing);
                await SaveReportAsync(db, taskId, services, merged, freeNote).ConfigureAwait(false);
                return;
            }

            var existingOuter = await LoadPartLinesAsync(taskId).ConfigureAwait(false);
            var servicesOuter = await LoadServiceLinesAsync(taskId).ConfigureAwait(false);
            var mergedOuter = existingOuter.Concat(incoming).ToList();

            var freeNoteOuter = string.Empty;
            try
            {
                await DatabaseExecutor.WithDbAsync(async outerDb =>
                {
                    var report = await outerDb.Database.SqlQuery<string>(
                        "SELECT ReportText FROM Tasks WHERE RowId = @p0", taskId).FirstOrDefaultAsync().ConfigureAwait(false);
                    freeNoteOuter = ExtractFreeTextFromReport(report, servicesOuter, existingOuter);
                }).ConfigureAwait(false);
            }
            catch
            {
            }

            await SaveReportAsync(taskId, servicesOuter, mergedOuter, freeNoteOuter).ConfigureAwait(false);
        }

        private static Task<List<TaskServiceLineRow>> LoadServiceLinesCoreAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId) =>
            db.Database.SqlQuery<TaskServiceLineRow>(
                @"SELECT RowId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount
                  FROM TaskServiceLines WHERE TaskId = @p0 ORDER BY SortOrder",
                taskId).ToListAsync();

        private static string ExtractFreeTextFromReport(
            string reportText,
            IList<TaskServiceLineRow> services,
            IList<TaskPartLineRow> parts)
        {
            if (services.Count == 0 && parts.Count == 0)
                return reportText ?? string.Empty;

            var built = BuildReportText(services, parts, string.Empty);
            var full = (reportText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(built))
                return full;
            if (full.StartsWith(built, StringComparison.Ordinal))
                return full.Substring(built.Length).Trim();
            return full;
        }

        public static Task<string> LoadFreeTextNoteAsync(Guid taskId) =>
            DatabaseExecutor.WithDbAsync(db => LoadFreeTextNoteAsync(db, taskId));

        public static async Task<string> LoadFreeTextNoteAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId)
        {
            var services = await LoadServiceLinesAsync(db, taskId).ConfigureAwait(false);
            var parts = await LoadPartLinesAsync(db, taskId).ConfigureAwait(false);
            var report = await db.Database.SqlQuery<string>(
                "SELECT ReportText FROM Tasks WHERE RowId = @p0", taskId).FirstOrDefaultAsync().ConfigureAwait(false);
            return ExtractFreeTextFromReport(report, services, parts);
        }

        public static Task SaveReportAsync(
            Guid taskId,
            IEnumerable<TaskServiceLineRow> services,
            IEnumerable<TaskPartLineRow> parts,
            string freeTextNote) =>
            DatabaseExecutor.WithDbAsync(db => SaveReportAsync(db, taskId, services, parts, freeTextNote));

        public static async Task SaveReportAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId,
            IEnumerable<TaskServiceLineRow> services,
            IEnumerable<TaskPartLineRow> parts,
            string freeTextNote)
        {
            var serviceList = (services ?? Enumerable.Empty<TaskServiceLineRow>())
                .Where(s => !string.IsNullOrWhiteSpace(s.ServiceName))
                .ToList();
            var partList = (parts ?? Enumerable.Empty<TaskPartLineRow>())
                .Where(p => !string.IsNullOrWhiteSpace(p.PartName))
                .ToList();

            foreach (var s in serviceList)
                s.RecalculateAmount();
            foreach (var p in partList)
                p.RecalculateAmount();

            try
            {
                await db.Database.ExecuteSqlCommandAsync("DELETE FROM TaskServiceLines WHERE TaskId = @p0", taskId)
                    .ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
            }

            try
            {
                await db.Database.ExecuteSqlCommandAsync("DELETE FROM TaskPartLines WHERE TaskId = @p0", taskId)
                    .ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
            }

            var sort = 0;
            foreach (var s in serviceList)
            {
                try
                {
                    await db.Database.ExecuteSqlCommandAsync(
                        @"INSERT INTO TaskServiceLines
                          (RowId, TaskId, WorkshopServiceId, ServiceName, Quantity, UnitName, UnitPrice, DiscountPercent, LineAmount, SortOrder)
                          VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)",
                        Guid.NewGuid(), taskId,
                        (object)s.WorkshopServiceId ?? DBNull.Value,
                        s.ServiceName.Trim(),
                        s.Quantity, s.UnitName ?? "усл.", s.UnitPrice, s.DiscountPercent, s.LineAmount, sort++)
                        .ConfigureAwait(false);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    break;
                }
            }

            sort = 0;
            foreach (var p in partList)
            {
                await InsertPartLineAsync(db, taskId, p, sort++).ConfigureAwait(false);
            }

            var report = BuildReportText(serviceList, partList, freeTextNote);
            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE Tasks SET ReportText = @p1 WHERE RowId = @p0", taskId, (object)report ?? string.Empty)
                .ConfigureAwait(false);
        }

        public static string BuildReportText(
            IList<TaskServiceLineRow> services,
            IList<TaskPartLineRow> parts,
            string freeTextNote)
        {
            var sb = new StringBuilder();
            if (services.Count > 0)
            {
                sb.AppendLine("Выполненные услуги:");
                foreach (var s in services)
                {
                    sb.Append("— ").Append(s.ServiceName).Append(" — ").Append(s.Quantity.ToString("0.###"))
                        .Append(' ').Append(s.UnitName).Append(" × ").Append(s.UnitPrice.ToString("0.00"));
                    if (s.DiscountPercent > 0)
                        sb.Append(" (скидка ").Append(s.DiscountPercent.ToString("0.##")).Append("%)");
                    sb.Append(" = ").AppendLine(s.LineAmount.ToString("0.00"));
                }
                sb.AppendLine("Итого услуги: ").AppendLine(services.Sum(x => x.LineAmount).ToString("0.00"));
            }

            if (parts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Детали / материалы:");
                foreach (var p in parts)
                {
                    sb.Append("— ").Append(p.PartName).Append(" — ").Append(p.Quantity.ToString("0.###"))
                        .Append(' ').Append(p.UnitName);
                    if (p.UnitPrice > 0)
                        sb.Append(" × ").Append(p.UnitPrice.ToString("0.00")).Append(" = ").Append(p.LineAmount.ToString("0.00"));
                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(freeTextNote))
            {
                sb.AppendLine();
                sb.AppendLine(freeTextNote.Trim());
            }

            return sb.ToString().Trim();
        }

        private static Task InsertPartLineAsync(
            DriveCareCore.Data.BD.DriveCareDBEntities db,
            Guid taskId,
            TaskPartLineRow p,
            int sortOrder) =>
            TaskPartLineSql.InsertAsync(db, taskId, p, sortOrder);

        public static List<RepairWorkOrderWorkLine> ToWorkOrderLines(IEnumerable<TaskServiceLineRow> lines) =>
            (lines ?? Enumerable.Empty<TaskServiceLineRow>())
                .Where(l => !string.IsNullOrWhiteSpace(l.ServiceName))
                .Select((l, i) => new RepairWorkOrderWorkLine
                {
                    Code = (i + 1).ToString(),
                    Name = l.ServiceName,
                    Multiplicity = l.Quantity.ToString("0.###"),
                    PricePerHour = l.UnitPrice.ToString("0.00"),
                    Amount = l.LineAmount.ToString("0.00"),
                    Cost = l.LineAmount.ToString("0.00")
                })
                .ToList();

        public static List<RepairWorkOrderPartLine> ToWorkOrderPartLines(IEnumerable<TaskPartLineRow> lines) =>
            (lines ?? Enumerable.Empty<TaskPartLineRow>())
                .Where(l => !string.IsNullOrWhiteSpace(l.PartName))
                .Select((l, i) =>
                {
                    l.RecalculateAmount();
                    return new RepairWorkOrderPartLine
                    {
                        Number = (i + 1).ToString(),
                        Name = l.PartName.Trim(),
                        Unit = string.IsNullOrWhiteSpace(l.UnitName) ? "шт." : l.UnitName.Trim(),
                        Quantity = l.Quantity.ToString("0.###"),
                        Price = l.UnitPrice.ToString("0.00"),
                        Discount = string.Empty,
                        Amount = l.LineAmount.ToString("0.00")
                    };
                })
                .ToList();
    }
}
