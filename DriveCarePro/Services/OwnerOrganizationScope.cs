using DriveCareCore.Data.BD;

using System;

using System.Collections.Generic;

using System.Data.Entity;
using System.Linq;



namespace DriveCarePro.Services

{

    /// <summary>Компания и мастерские текущего владельца.</summary>

    public sealed class OwnerOrganizationScope

    {

        public Guid CompanyId { get; private set; }

        public string CompanyName { get; private set; }

        public IReadOnlyList<Guid> WorkshopIds { get; private set; }



        public static bool TryResolve(out OwnerOrganizationScope scope, out string errorMessage)
        {
            var result = TryResolveAsync().GetAwaiter().GetResult();
            scope = result.scope;
            errorMessage = result.error;
            return result.ok;
        }

        public static async System.Threading.Tasks.Task<(bool ok, OwnerOrganizationScope scope, string error)> TryResolveAsync()
        {
            var owner = AppState.CurrentEmployee;
            if (owner == null || !owner.WorkshopId.HasValue)
            {
                return (false, null, "У вас не указана мастерская. Назначьте WorkshopId владельцу в БД.");
            }

            try
            {
                return await DatabaseExecutor.WithDbAsync<(bool ok, OwnerOrganizationScope scope, string error)>(async db =>
                {
                    var ownerWorkshop = await db.Workshops
                        .FirstOrDefaultAsync(w => w.RowId == owner.WorkshopId.Value)
                        .ConfigureAwait(false);

                    if (ownerWorkshop == null)
                        return (false, null, "Мастерская владельца не найдена в базе.");

                    var companyId = ownerWorkshop.CompanyId;

                    var workshopIds = await db.Workshops
                        .Where(w => w.CompanyId == companyId)
                        .Select(w => w.RowId)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    var companyName = await db.Companies
                        .Where(c => c.RowId == companyId)
                        .Select(c => c.Name)
                        .FirstOrDefaultAsync()
                        .ConfigureAwait(false);

                    var scope = new OwnerOrganizationScope
                    {
                        CompanyId = companyId,
                        CompanyName = string.IsNullOrWhiteSpace(companyName) ? "—" : companyName.Trim(),
                        WorkshopIds = workshopIds
                    };

                    return (true, scope, null);
                }).ConfigureAwait(false);
            }
            catch (Exception ex) when (AppState.IsDatabaseConnectionError(ex))
            {
                return (false, null, AppState.BuildConnectionErrorMessage(ex));
            }
        }

        public IQueryable<Employee> EmployeesInOrganization(DriveCareDBEntities db)

        {

            var ids = WorkshopIds;

            return db.Employees.Where(e => e.WorkshopId.HasValue && ids.Contains(e.WorkshopId.Value));

        }



        /// <summary>Роли компании: на всю организацию (CompanyId) или на салон (WorkshopId).</summary>

        public IQueryable<Role> RolesForOrganization(DriveCareDBEntities db)

        {

            var companyId = CompanyId;

            var workshopIds = WorkshopIds;

            return db.Roles.Where(r =>

                (r.CompanyId == companyId && !r.WorkshopId.HasValue) ||

                (r.WorkshopId.HasValue && workshopIds.Contains(r.WorkshopId.Value)));

        }



        public bool IsSystemGlobalRole(Role role) =>

            role != null && !role.WorkshopId.HasValue && !role.CompanyId.HasValue;



        public bool IsCompanyWideRole(Role role) =>

            role != null && role.CompanyId == CompanyId && !role.WorkshopId.HasValue;



        public bool IsWorkshopRole(Role role) =>

            role != null && role.WorkshopId.HasValue && WorkshopIds.Contains(role.WorkshopId.Value);



        public bool CanOwnerManageRole(Role role) =>

            role != null && (IsCompanyWideRole(role) || IsWorkshopRole(role));

    }

}

