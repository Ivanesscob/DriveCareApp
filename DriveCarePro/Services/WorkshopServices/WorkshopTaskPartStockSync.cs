using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.WorkshopServices
{
    /// <summary>Списание/возврат склада при добавлении и удалении строк в задании.</summary>
    internal static class WorkshopTaskPartStockSync
    {
        public static TaskPartLineRow ToStockRow(TaskPartLineVm vm, decimal quantity) =>
            new TaskPartLineRow
            {
                WorkshopPartId = vm.WorkshopPartId ?? vm.ReservedPartId,
                PartName = vm.PartName,
                Quantity = quantity,
                UnitName = vm.UnitName
            };

        public static async Task<(bool ok, string error)> ReturnReservationAsync(
            Guid workshopId,
            TaskPartLineVm vm)
        {
            if (vm.ReservedQuantity <= 0 || !vm.ReservedPartId.HasValue)
                return (true, null);

            var row = new TaskPartLineRow
            {
                WorkshopPartId = vm.ReservedPartId,
                PartName = vm.PartName,
                Quantity = vm.ReservedQuantity,
                UnitName = vm.UnitName
            };

            var result = await DatabaseExecutor.WithDbAsync(db =>
                WorkshopStockService.ReturnStockAsync(db, workshopId, new[] { row })).ConfigureAwait(false);

            if (result.ok)
                vm.ClearReservation();

            return result;
        }

        /// <summary>Возврат только изменений текущей сессии (не трогает количество из БД при открытии).</summary>
        public static async Task<(bool ok, string error)> ReturnSessionDeltaAsync(
            Guid workshopId,
            TaskPartLineVm vm)
        {
            if (!vm.ReservedPartId.HasValue || vm.ReservedQuantity <= 0)
                return (true, null);

            var delta = vm.ReservedQuantity - vm.LoadedQuantity;
            if (delta <= 0)
                return (true, null);

            var row = new TaskPartLineRow
            {
                WorkshopPartId = vm.ReservedPartId,
                PartName = vm.PartName,
                Quantity = delta,
                UnitName = vm.UnitName
            };

            var result = await DatabaseExecutor.WithDbAsync(db =>
                WorkshopStockService.ReturnStockAsync(db, workshopId, new[] { row })).ConfigureAwait(false);

            if (result.ok)
                vm.SetReservation(vm.ReservedPartId.Value, vm.LoadedQuantity, inCurrentSession: false);

            return result;
        }

        public static async Task<(bool ok, string error)> SyncLineAsync(
            Guid workshopId,
            TaskPartLineVm vm)
        {
            if (vm.IsPurchaseLine || workshopId == Guid.Empty)
                return (true, null);

            var partId = vm.WorkshopPartId;
            var qty = vm.Quantity;

            if (!partId.HasValue || partId.Value == Guid.Empty || qty <= 0)
            {
                return await ReturnReservationAsync(workshopId, vm).ConfigureAwait(false);
            }

            if (vm.ReservedPartId.HasValue && vm.ReservedPartId.Value != partId.Value)
            {
                var (retOk, retErr) = await ReturnReservationAsync(workshopId, vm).ConfigureAwait(false);
                if (!retOk)
                    return (false, retErr);
                vm.ClearReservation();
            }

            var delta = qty - vm.ReservedQuantity;
            if (delta == 0)
                return (true, null);

            if (delta > 0)
            {
                var row = ToStockRow(vm, delta);
                row.WorkshopPartId = partId;
                var (ok, err) = await DatabaseExecutor.WithDbAsync(db =>
                    WorkshopStockService.ConsumeStockAsync(db, workshopId, new[] { row })).ConfigureAwait(false);
                if (!ok)
                    return (false, err);
            }
            else
            {
                var returnQty = -delta;
                var row = new TaskPartLineRow
                {
                    WorkshopPartId = vm.ReservedPartId ?? partId,
                    PartName = vm.PartName,
                    Quantity = returnQty,
                    UnitName = vm.UnitName
                };
                var (ok, err) = await DatabaseExecutor.WithDbAsync(db =>
                    WorkshopStockService.ReturnStockAsync(db, workshopId, new[] { row })).ConfigureAwait(false);
                if (!ok)
                    return (false, err);
            }

            vm.SetReservation(partId.Value, qty, inCurrentSession: true);
            return (true, null);
        }
    }
}
