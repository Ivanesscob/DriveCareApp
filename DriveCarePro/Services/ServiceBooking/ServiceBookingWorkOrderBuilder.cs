using DriveCareCore.Data.BD;

using DriveCareCore.WorkOrders;

using DriveCarePro.Services.RepairWorkOrder;

using DriveCarePro.Services;

using DriveCarePro.Services.ServiceDocuments;

using DriveCarePro.Services.WorkshopServices;

using System;

using System.Collections.Generic;

using System.Data.Entity;

using System.Globalization;

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

            OwnerOrganizationScope scope,

            Guid? preferredTaskId = null)

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



            if (repair.Mileage.HasValue && repair.Mileage.Value > 0)

                model.Mileage = repair.Mileage.Value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU"));



            var car = await db.Cars.FirstOrDefaultAsync(c => c.RowId == repair.CarId).ConfigureAwait(false);

            if (car != null)

            {

                model.CarDescription = CarDisplayHelper.FormatCar(db, car);

                model.Year = car.Year?.ToString() ?? string.Empty;

            }



            var taskId = preferredTaskId;

            if (!taskId.HasValue)

            {

                taskId = await db.Database.SqlQuery<Guid?>(

                    "SELECT TOP 1 RowId FROM Tasks WHERE RepairHistoryId = @p0 ORDER BY CreatedAt DESC",

                    repairHistoryId).FirstOrDefaultAsync().ConfigureAwait(false);

            }



            if (taskId.HasValue)

            {

                var taskFilterArg = taskId.Value;

                var taskExtra = await db.Database.SqlQuery<TaskBookingExtra>(

                    @"SELECT TOP 1 RepairHistoryId, ClientName, ClientPhone, ClientEmail, VisitReason, SpecialNotes, ServiceKind

                      FROM Tasks WHERE RowId = @p0",

                    taskFilterArg).FirstOrDefaultAsync().ConfigureAwait(false);



                if (taskExtra != null)

                {

                    if (!string.IsNullOrWhiteSpace(taskExtra.VisitReason))

                        model.VisitReason = taskExtra.VisitReason;

                    if (!string.IsNullOrWhiteSpace(taskExtra.SpecialNotes))

                        model.SpecialNotes = taskExtra.SpecialNotes;

                    if (!string.IsNullOrWhiteSpace(taskExtra.ServiceKind))

                        model.RepairType = taskExtra.ServiceKind;

                    if (!string.IsNullOrWhiteSpace(taskExtra.ClientName))

                        model.ClientName = taskExtra.ClientName;

                    if (!string.IsNullOrWhiteSpace(taskExtra.ClientPhone))

                        model.ClientPhone = taskExtra.ClientPhone;

                }



                var clientUserId = await db.Database.SqlQuery<Guid?>(

                    "SELECT ClientUserId FROM Tasks WHERE RowId = @p0", taskId.Value)

                    .FirstOrDefaultAsync().ConfigureAwait(false);



                if (string.IsNullOrWhiteSpace(model.ClientName) && clientUserId.HasValue && clientUserId.Value != Guid.Empty)

                {

                    var user = await db.Users.AsNoTracking()

                        .FirstOrDefaultAsync(u => u.RowId == clientUserId.Value)

                        .ConfigureAwait(false);

                    if (user != null)

                    {

                        model.ClientName = FormatUserDisplayName(user);

                        if (string.IsNullOrWhiteSpace(model.ClientPhone))

                            model.ClientPhone = user.Phone ?? string.Empty;

                    }

                }

            }



            if (string.IsNullOrWhiteSpace(model.ClientName) && repair.EmployeeId.HasValue)

            {

                var emp = await db.Employees.FirstOrDefaultAsync(e => e.RowId == repair.EmployeeId.Value).ConfigureAwait(false);

                if (emp != null)

                    model.ClientName = AppState.FormatEmployeeDisplayName(emp);

            }



            if (taskId.HasValue)

                await ApplyMergedLinesForTaskAsync(db, taskId.Value, model, scope).ConfigureAwait(false);



            model.RecalculateTotals();

            return model;

        }



        static string FormatUserDisplayName(User user)
        {
            if (user == null)
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(user.Description))
                return user.Description.Trim();
            if (!string.IsNullOrWhiteSpace(user.Login))
                return user.Login.Trim();
            if (!string.IsNullOrWhiteSpace(user.Email))
                return user.Email.Trim();
            return string.Empty;
        }



        /// <summary>Услуги и запчасти из ServiceDocuments и всей цепочки поручений.</summary>

        public static async Task ApplyMergedLinesForTaskAsync(

            DriveCareDBEntities db,

            Guid taskId,

            RepairWorkOrderModel model,

            OwnerOrganizationScope scope = null)

        {

            if (model == null)

                return;



            var preview = await ServiceDocumentService.TryLoadPreviewAsync(db, taskId, syncFromChainFirst: true)

                .ConfigureAwait(false);

            if (preview == null)

                return;



            if (preview.Services != null && preview.Services.Count > 0)

                ApplyServiceLinesToModel(model, preview.Services, scope);



            if (preview.Parts != null && preview.Parts.Count > 0)

                ApplyPartLinesToModel(model, preview.Parts);

        }



        public static void ApplyServiceLinesToModel(

            RepairWorkOrderModel model,

            IList<TaskServiceLineRow> serviceRows,

            OwnerOrganizationScope scope = null)

        {

            if (model == null)

                return;



            var rows = (serviceRows ?? Array.Empty<TaskServiceLineRow>())

                .Where(r => !string.IsNullOrWhiteSpace(r?.ServiceName))

                .ToList();

            if (rows.Count == 0)

                return;



            foreach (var row in rows)

                row.RecalculateAmount();



            var workLines = TaskReportService.ToWorkOrderLines(rows);

            var executor = ResolveExecutorName(scope);

            if (!string.IsNullOrWhiteSpace(executor))

            {

                foreach (var line in workLines)

                    line.Executor = executor;

            }



            model.WorkLines = workLines;

        }



        public static void ApplyServiceLinesToModel(RepairWorkOrderModel model, IList<TaskServiceLineRow> serviceRows) =>

            ApplyServiceLinesToModel(model, serviceRows, null);



        static string ResolveExecutorName(OwnerOrganizationScope scope)

        {

            if (scope?.WorkshopIds != null && scope.WorkshopIds.Count > 0)

            {

                try

                {

                    using (var db = new DriveCareDBEntities())

                    {

                        var wsId = scope.WorkshopIds[0];

                        var name = db.Workshops.AsNoTracking()

                            .Where(w => w.RowId == wsId)

                            .Select(w => w.Name)

                            .FirstOrDefault();

                        if (!string.IsNullOrWhiteSpace(name))

                            return name.Trim();

                    }

                }

                catch

                {

                }

            }



            var employee = AppState.CurrentEmployee;

            if (employee?.WorkshopId != null)

            {

                try

                {

                    using (var db = new DriveCareDBEntities())

                    {

                        var name = db.Workshops.AsNoTracking()

                            .Where(w => w.RowId == employee.WorkshopId.Value)

                            .Select(w => w.Name)

                            .FirstOrDefault();

                        if (!string.IsNullOrWhiteSpace(name))

                            return name.Trim();

                    }

                }

                catch

                {

                }

            }



            return employee != null ? AppState.FormatEmployeeDisplayName(employee) : string.Empty;

        }



        public static void ApplyPartLinesToModel(RepairWorkOrderModel model, IList<TaskPartLineRow> partRows)

        {

            if (model == null)

                return;



            var partLines = TaskReportService.ToWorkOrderPartLines(partRows);

            if (partLines.Count == 0)

                return;



            model.PartLines = partLines;

        }

    }

}


