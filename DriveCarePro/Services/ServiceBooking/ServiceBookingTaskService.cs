using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskEntity = DriveCareCore.Data.BD.Task;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.ServiceBooking
{
    internal static class ServiceBookingTaskService
    {
        public static async Task<Guid?> CreateForBookingAsync(
            DriveCareDBEntities db,
            ServiceBookingContext ctx,
            Guid carId,
            Guid repairHistoryId)
        {
            var employee = AppState.CurrentEmployee;
            if (employee == null)
                return null;

            var statusId = await db.Statuses.Select(s => s.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
            if (statusId == Guid.Empty)
                return null;

            var title = BuildTitle(ctx);
            var description = BuildDescription(ctx);

            var task = new TaskEntity
            {
                RowId = Guid.NewGuid(),
                Title = title,
                Description = description,
                EmployeeId = employee.RowId,
                StatusId = statusId,
                CreatedAt = DateTime.Now,
                StartDate = DateTime.Now,
                IsCompleted = false,
                CarId = carId,
                ClientUserId = ctx.FoundUser?.RowId
            };

            db.Tasks.Add(task);
            await db.SaveChangesAsync().ConfigureAwait(false);

            await TryWriteExtendedFieldsAsync(db, task.RowId, ctx, repairHistoryId).ConfigureAwait(false);
            return task.RowId;
        }

        public static async Task<TaskBookingExtra> TryLoadExtraAsync(DriveCareDBEntities db, Guid taskId)
        {
            try
            {
                return await db.Database.SqlQuery<TaskBookingExtra>(
                    @"SELECT RepairHistoryId, ClientName, ClientPhone, ClientEmail, VisitReason, SpecialNotes, ServiceKind
                      FROM Tasks WHERE RowId = @p0",
                    taskId).FirstOrDefaultAsync().ConfigureAwait(false);
            }
            catch (SqlException)
            {
                return null;
            }
        }

        private static async Task TryWriteExtendedFieldsAsync(
            DriveCareDBEntities db,
            Guid taskId,
            ServiceBookingContext ctx,
            Guid repairHistoryId)
        {
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
                    taskId,
                    repairHistoryId,
                    (object)ctx.ClientFullName ?? DBNull.Value,
                    (object)ctx.ClientPhone ?? DBNull.Value,
                    (object)ctx.ClientEmail ?? DBNull.Value,
                    (object)ctx.VisitReason ?? DBNull.Value,
                    (object)ctx.SpecialNotes ?? DBNull.Value,
                    ctx.RepairTypeDisplay ?? string.Empty).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 207 || ex.Number == 208)
            {
                // Колонки ещё не добавлены — данные уже в Title/Description.
            }
        }

        private static string BuildTitle(ServiceBookingContext ctx)
        {
            var car = string.IsNullOrWhiteSpace(ctx.CarDescription) ? "авто" : ctx.CarDescription.Trim();
            var client = string.IsNullOrWhiteSpace(ctx.ClientFullName) ? "клиент" : ctx.ClientFullName.Trim();
            var title = $"{ctx.RepairTypeDisplay}: {car} — {client}";
            return title.Length > 250 ? title.Substring(0, 250) : title;
        }

        private static string BuildDescription(ServiceBookingContext ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Запись на сервис. Задание назначено вам.");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(ctx.ClientFullName))
                sb.AppendLine("Клиент: " + ctx.ClientFullName.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.ClientPhone))
                sb.AppendLine("Телефон: " + ctx.ClientPhone.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.ClientEmail))
                sb.AppendLine("Email: " + ctx.ClientEmail.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.CarDescription))
                sb.AppendLine("Автомобиль: " + ctx.CarDescription.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.PlateNumber))
                sb.AppendLine("Гос. номер: " + ctx.PlateNumber.Trim());
            if (!string.IsNullOrWhiteSpace(ctx.Vin))
                sb.AppendLine("VIN: " + ctx.Vin.Trim());
            sb.AppendLine();
            sb.AppendLine("Что сделать с машиной:");
            sb.AppendLine(ctx.VisitReason?.Trim() ?? "—");
            if (!string.IsNullOrWhiteSpace(ctx.SpecialNotes))
            {
                sb.AppendLine();
                sb.AppendLine("Особые данные:");
                sb.AppendLine(ctx.SpecialNotes.Trim());
            }

            return sb.ToString().Trim();
        }
    }

    public sealed class TaskBookingExtra
    {
        public Guid? RepairHistoryId { get; set; }
        public string ClientName { get; set; }
        public string ClientPhone { get; set; }
        public string ClientEmail { get; set; }
        public string VisitReason { get; set; }
        public string SpecialNotes { get; set; }
        public string ServiceKind { get; set; }
    }
}
