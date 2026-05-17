using DriveCareCore.Data.BD;
using DriveCarePro.Services.RepairWorkOrder;
using DriveCarePro.Services;
using DriveCarePro.Services.WorkshopServices;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.ServiceBooking
{
    /// <summary>Собирает заказ-наряд из записи в БД (ремонт + задание).</summary>
    internal static class ServiceBookingWorkOrderBuilder
    {
        public static async Task<RepairWorkOrderModel> BuildFromRepairAsync(
            DriveCareDBEntities db,
            Guid repairHistoryId,
            OwnerOrganizationScope scope)
        {
            var repair = await db.RepairHistories.FirstOrDefaultAsync(r => r.RowId == repairHistoryId).ConfigureAwait(false);
            if (repair == null)
                return null;

            var model = await RepairWorkOrderDataService.LoadCompanyContextAsync(scope).ConfigureAwait(false);
            model.OrderDate = DateTime.Now.ToString("dd.MM.yyyy");
            model.OrderTime = DateTime.Now.ToString("HH:mm");
            model.RepairType = repair.Title ?? "Ремонт";
            model.VisitReason = repair.Description ?? string.Empty;
            model.SpecialNotes = string.Empty;

            var car = await db.Cars.FirstOrDefaultAsync(c => c.RowId == repair.CarId).ConfigureAwait(false);
            if (car != null)
            {
                model.CarDescription = CarDisplayHelper.FormatCar(db, car);
                model.Year = car.Year?.ToString() ?? string.Empty;
            }

            var taskExtra = await db.Database.SqlQuery<TaskBookingExtra>(
                @"SELECT TOP 1 RepairHistoryId, ClientName, ClientPhone, ClientEmail, VisitReason, SpecialNotes, ServiceKind
                  FROM Tasks WHERE RepairHistoryId = @p0",
                repairHistoryId).FirstOrDefaultAsync().ConfigureAwait(false);

            if (taskExtra != null)
            {
                if (!string.IsNullOrWhiteSpace(taskExtra.VisitReason))
                    model.VisitReason = taskExtra.VisitReason;
                if (!string.IsNullOrWhiteSpace(taskExtra.SpecialNotes))
                    model.SpecialNotes = taskExtra.SpecialNotes;
                if (!string.IsNullOrWhiteSpace(taskExtra.ServiceKind))
                    model.RepairType = taskExtra.ServiceKind;
                model.ClientName = taskExtra.ClientName ?? string.Empty;
                model.ClientPhone = taskExtra.ClientPhone ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(model.ClientName) && repair.EmployeeId.HasValue)
            {
                var emp = await db.Employees.FirstOrDefaultAsync(e => e.RowId == repair.EmployeeId.Value).ConfigureAwait(false);
                if (emp != null)
                    model.ClientName = AppState.FormatEmployeeDisplayName(emp);
            }

            var taskId = await db.Database.SqlQuery<Guid?>(
                "SELECT TOP 1 RowId FROM Tasks WHERE RepairHistoryId = @p0", repairHistoryId).FirstOrDefaultAsync().ConfigureAwait(false);
            if (taskId.HasValue)
            {
                var lines = await TaskReportService.LoadServiceLinesAsync(taskId.Value).ConfigureAwait(false);
                var workLines = TaskReportService.ToWorkOrderLines(lines);
                if (workLines.Count > 0)
                    model.WorkLines = workLines;
            }

            return model;
        }
    }
}
