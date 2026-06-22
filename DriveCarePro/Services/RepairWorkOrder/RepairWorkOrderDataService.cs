using DriveCareCore.Data.BD;
using DriveCareCore.WorkOrders;
using DriveCarePro.Services;
using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.RepairWorkOrder
{
    internal static class RepairWorkOrderDataService
    {
        public static async Task<RepairWorkOrderModel> LoadCompanyContextAsync(OwnerOrganizationScope scope)
        {
            var model = new RepairWorkOrderModel();
            if (scope == null)
                return model;

            var employee = AppState.CurrentEmployee;
            if (employee?.WorkshopId == null)
                return model;

            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                var workshop = await db.Workshops
                    .FirstOrDefaultAsync(w => w.RowId == employee.WorkshopId.Value)
                    .ConfigureAwait(false);

                if (workshop == null)
                    return model;

                var company = await db.Companies
                    .FirstOrDefaultAsync(c => c.RowId == workshop.CompanyId)
                    .ConfigureAwait(false);

                string addressText = string.Empty;
                if (workshop.AddressId.HasValue)
                {
                    var addr = await db.Addresses
                        .FirstOrDefaultAsync(a => a.RowId == workshop.AddressId.Value)
                        .ConfigureAwait(false);
                    if (addr != null)
                    {
                        addressText = !string.IsNullOrWhiteSpace(addr.FullAddress)
                            ? addr.FullAddress.Trim()
                            : string.Join(", ", new[] { addr.City, addr.Street, addr.House, addr.Apartment }
                                .Where(s => !string.IsNullOrWhiteSpace(s))
                                .Select(s => s.Trim()));
                    }
                }

                model.CompanyName = company?.Name?.Trim() ?? workshop.Name?.Trim() ?? string.Empty;
                model.CompanyLegalAddress = addressText;
                model.CompanyPhone = string.Empty;
                model.OrderDate = DateTime.Now.ToString("dd.MM.yyyy");
                model.OrderTime = DateTime.Now.ToString("HH:mm");
                return model;
            }).ConfigureAwait(false);
        }
    }
}
