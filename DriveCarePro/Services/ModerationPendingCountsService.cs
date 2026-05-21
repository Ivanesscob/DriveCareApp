using DriveCareCore.Data.BD;
using DriveCareCore.Data.Services;
using DriveCareCore.Maps;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace DriveCarePro.Services
{
    public sealed class ModerationPendingCounts
    {
        public int CarSales { get; set; }
        public int WorkshopTypes { get; set; }
        public int Total => CarSales + WorkshopTypes;
    }

    public static class ModerationPendingCountsService
    {
        public static async Task<ModerationPendingCounts> LoadAsync()
        {
            var result = new ModerationPendingCounts();
            try
            {
                result.WorkshopTypes = WorkshopBusinessTypeModerationService.CountPending();
            }
            catch
            {
            }

            try
            {
                result.CarSales = await CountCarSalesPendingAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            return result;
        }

        static async Task<int> CountCarSalesPendingAsync()
        {
            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                var approved = CarSaleModerationStatuses.ResolveCarSaleStatusIdByName(
                    db, CarSaleModerationStatuses.ApprovedModeration);

                var query = db.CarSales.AsQueryable();
                if (approved.HasValue)
                    query = query.Where(c => !c.StatusId.HasValue || c.StatusId != approved.Value);
                else
                    query = query.Where(c => c.StatusId.HasValue);

                return await query.CountAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
