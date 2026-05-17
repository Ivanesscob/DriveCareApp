using DriveCareCore.Data.BD;

using System;

using System.Collections.Generic;

using System.ComponentModel;

using System.Data.Entity;

using System.Linq;

using System.Runtime.CompilerServices;




namespace DriveCarePro.Services

{

    internal static class EmployeeManagementService

    {

        public sealed class RolePickItem : INotifyPropertyChanged

        {

            private bool _isSelected;



            public Guid RoleId { get; set; }

            public string Name { get; set; }

            public string ScopeHint { get; set; }



            public bool IsSelected

            {

                get => _isSelected;

                set

                {

                    if (_isSelected == value)

                        return;

                    _isSelected = value;

                    OnPropertyChanged();

                    OnPropertyChanged(nameof(DisplayText));

                }

            }



            public string DisplayText =>

                (IsSelected ? "✓ " : string.Empty) +

                (string.IsNullOrWhiteSpace(ScopeHint) || ScopeHint == "—"

                    ? Name

                    : Name + " (" + ScopeHint + ")");



            public event PropertyChangedEventHandler PropertyChanged;



            private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        }



        public sealed class EmployeeEditModel

        {

            public Guid? EmployeeId { get; set; }

            public string LastName { get; set; }

            public string FirstName { get; set; }

            public string MidName { get; set; }

            public string Login { get; set; }

            public string Password { get; set; }

            public string Email { get; set; }

            public string Phone { get; set; }

            public string Description { get; set; }

            public DateTime? BirthDate { get; set; }

            public DateTime? HireDate { get; set; }

            public bool IsActive { get; set; } = true;

            public Guid? WorkshopId { get; set; }

            public List<Guid> RoleIds { get; set; } = new List<Guid>();

        }



        public sealed class SaveResult

        {

            public bool Success { get; set; }

            public string ErrorMessage { get; set; }

            public Guid? EmployeeId { get; set; }

        }



        public static List<RolePickItem> LoadAssignableRoles(OwnerOrganizationScope scope) =>

            LoadAssignableRolesAsync(scope).GetAwaiter().GetResult();



        public static async System.Threading.Tasks.Task<List<RolePickItem>> LoadAssignableRolesAsync(OwnerOrganizationScope scope)

        {

            if (scope == null)

                return new List<RolePickItem>();



            return await DatabaseExecutor.WithDbAsync(async db =>

            {

                var roles = await scope.RolesForOrganization(db)

                    .Where(r => r.IsActive != false)

                    .OrderBy(r => r.Name)

                    .ToListAsync()

                    .ConfigureAwait(false);



                return roles.Select(r =>

                {

                    var hint = scope.IsCompanyWideRole(r)

                        ? "вся компания"

                        : scope.IsWorkshopRole(r)

                            ? "салон"

                            : "—";

                    return new RolePickItem

                    {

                        RoleId = r.RowId,

                        Name = string.IsNullOrWhiteSpace(r.Name) ? "—" : r.Name.Trim(),

                        ScopeHint = hint,

                        IsSelected = false

                    };

                }).ToList();

            }).ConfigureAwait(false);

        }



        public static EmployeeEditModel LoadEmployee(Guid employeeId, OwnerOrganizationScope scope) =>

            LoadEmployeeAsync(employeeId, scope).GetAwaiter().GetResult();



        public static async System.Threading.Tasks.Task<EmployeeEditModel> LoadEmployeeAsync(Guid employeeId, OwnerOrganizationScope scope)

        {

            return await DatabaseExecutor.WithDbAsync(async db =>

            {

                var emp = await scope.EmployeesInOrganization(db)

                    .FirstOrDefaultAsync(e => e.RowId == employeeId)

                    .ConfigureAwait(false);

                if (emp == null)

                    return null;



                var roleIds = await db.EmployeeRolesMaps

                    .Where(m => m.EmployeeId == employeeId)

                    .Select(m => m.RoleId)

                    .ToListAsync()

                    .ConfigureAwait(false);



                return new EmployeeEditModel

                {

                    EmployeeId = emp.RowId,

                    LastName = emp.LastName ?? string.Empty,

                    FirstName = emp.FirstName ?? string.Empty,

                    MidName = emp.MidName ?? string.Empty,

                    Login = emp.Login ?? string.Empty,

                    Email = emp.Email ?? string.Empty,

                    Phone = emp.Phone ?? string.Empty,

                    Description = emp.Description ?? string.Empty,

                    BirthDate = emp.BirthDate,

                    HireDate = emp.HireDate,

                    IsActive = emp.IsActive != false,

                    WorkshopId = emp.WorkshopId,

                    RoleIds = roleIds

                };

            }).ConfigureAwait(false);

        }



        public static SaveResult Save(EmployeeEditModel model, OwnerOrganizationScope scope, bool isNew) =>

            SaveAsync(model, scope, isNew).GetAwaiter().GetResult();



        public static System.Threading.Tasks.Task<SaveResult> SaveAsync(EmployeeEditModel model, OwnerOrganizationScope scope, bool isNew) =>

            DatabaseExecutor.WithDbAsync(db => SaveCoreAsync(db, model, scope, isNew));



        public static SaveResult AssignRoles(Guid employeeId, IList<Guid> roleIds, OwnerOrganizationScope scope) =>

            AssignRolesAsync(employeeId, roleIds, scope).GetAwaiter().GetResult();



        public static System.Threading.Tasks.Task<SaveResult> AssignRolesAsync(Guid employeeId, IList<Guid> roleIds, OwnerOrganizationScope scope) =>

            DatabaseExecutor.WithDbAsync(db => AssignRolesCoreAsync(db, employeeId, roleIds, scope));



        public static List<RolePickItem> LoadRolesForEmployee(Guid employeeId, OwnerOrganizationScope scope) =>

            LoadRolesForEmployeeAsync(employeeId, scope).GetAwaiter().GetResult();



        public static async System.Threading.Tasks.Task<List<RolePickItem>> LoadRolesForEmployeeAsync(Guid employeeId, OwnerOrganizationScope scope)

        {

            var items = await LoadAssignableRolesAsync(scope).ConfigureAwait(false);

            if (items.Count == 0)

                return items;



            var assigned = await DatabaseExecutor.WithDbAsync(db =>

                db.EmployeeRolesMaps

                    .Where(m => m.EmployeeId == employeeId)

                    .Select(m => m.RoleId)

                    .ToListAsync()).ConfigureAwait(false);



            var assignedSet = new HashSet<Guid>(assigned);

            foreach (var item in items)

                item.IsSelected = assignedSet.Contains(item.RoleId);



            return items;

        }



        public static string TryDelete(Guid employeeId, OwnerOrganizationScope scope) =>

            TryDeleteAsync(employeeId, scope).GetAwaiter().GetResult();



        public static System.Threading.Tasks.Task<string> TryDeleteAsync(Guid employeeId, OwnerOrganizationScope scope) =>

            DatabaseExecutor.WithDbAsync(db => TryDeleteCoreAsync(db, employeeId, scope));



        private static async System.Threading.Tasks.Task<SaveResult> SaveCoreAsync(

            DriveCareDBEntities db,

            EmployeeEditModel model,

            OwnerOrganizationScope scope,

            bool isNew)

        {

            if (model == null || scope == null)

                return Fail("Нет данных для сохранения.");



            var lastName = (model.LastName ?? string.Empty).Trim();

            var firstName = (model.FirstName ?? string.Empty).Trim();

            var login = (model.Login ?? string.Empty).Trim();



            if (string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(firstName))

                return Fail("Укажите фамилию и имя.");

            if (string.IsNullOrEmpty(login))

                return Fail("Укажите логин.");

            if (!model.WorkshopId.HasValue || !scope.WorkshopIds.Contains(model.WorkshopId.Value))

                return Fail("Выберите мастерскую вашей организации.");



            var password = (model.Password ?? string.Empty).Trim();

            if (isNew && string.IsNullOrEmpty(password))

                return Fail("Укажите пароль для нового сотрудника.");



            try

            {

                var loginTaken = await db.Employees.AnyAsync(e =>

                    e.Login == login &&

                    (!model.EmployeeId.HasValue || e.RowId != model.EmployeeId.Value)).ConfigureAwait(false);

                if (loginTaken)

                    return Fail("Сотрудник с таким логином уже существует.");



                Employee emp;

                if (isNew)

                {

                    emp = new Employee

                    {

                        RowId = Guid.NewGuid(),

                        Password = password

                    };

                    db.Employees.Add(emp);

                }

                else

                {

                    emp = await scope.EmployeesInOrganization(db)

                        .FirstOrDefaultAsync(e => e.RowId == model.EmployeeId.Value)

                        .ConfigureAwait(false);

                    if (emp == null)

                        return Fail("Сотрудник не найден.");

                    if (!string.IsNullOrEmpty(password))

                        emp.Password = password;

                }



                emp.LastName = lastName;

                emp.FirstName = firstName;

                emp.MidName = (model.MidName ?? string.Empty).Trim();

                emp.Login = login;

                emp.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();

                emp.Phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();

                emp.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

                emp.BirthDate = model.BirthDate;

                emp.HireDate = model.HireDate ?? (isNew ? DateTime.Now : emp.HireDate);

                emp.IsActive = model.IsActive;

                emp.WorkshopId = model.WorkshopId;



                await db.SaveChangesAsync().ConfigureAwait(false);

                await SyncRolesAsync(db, emp.RowId, model.RoleIds ?? new List<Guid>(), scope).ConfigureAwait(false);



                return new SaveResult { Success = true, EmployeeId = emp.RowId };

            }

            catch (Exception ex)

            {

                return Fail("Не удалось сохранить: " + ex.Message);

            }

        }



        private static async System.Threading.Tasks.Task<SaveResult> AssignRolesCoreAsync(

            DriveCareDBEntities db,

            Guid employeeId,

            IList<Guid> roleIds,

            OwnerOrganizationScope scope)

        {

            if (scope == null)

                return Fail("Нет доступа к организации.");



            try

            {

                var emp = await scope.EmployeesInOrganization(db)

                    .FirstOrDefaultAsync(e => e.RowId == employeeId)

                    .ConfigureAwait(false);

                if (emp == null)

                    return Fail("Сотрудник не найден.");



                await SyncRolesAsync(db, employeeId, roleIds ?? new List<Guid>(), scope).ConfigureAwait(false);

                return new SaveResult { Success = true, EmployeeId = employeeId };

            }

            catch (Exception ex)

            {

                return Fail("Не удалось сохранить роли: " + ex.Message);

            }

        }



        private static async System.Threading.Tasks.Task<string> TryDeleteCoreAsync(

            DriveCareDBEntities db,

            Guid employeeId,

            OwnerOrganizationScope scope)

        {

            if (scope == null)

                return "Нет доступа к организации.";



            try

            {

                var emp = await scope.EmployeesInOrganization(db)

                    .FirstOrDefaultAsync(e => e.RowId == employeeId)

                    .ConfigureAwait(false);

                if (emp == null)

                    return "Сотрудник не найден.";



                if (AppState.IsLoggedInEmployee(employeeId))

                    return "Нельзя удалить свою учётную запись.";

                if (await HasOwnerRoleAsync(db, employeeId).ConfigureAwait(false))

                    return "Нельзя удалить сотрудника с ролью владельца.";



                var maps = await db.EmployeeRolesMaps

                    .Where(m => m.EmployeeId == employeeId)

                    .ToListAsync()

                    .ConfigureAwait(false);

                foreach (var m in maps)

                    db.EmployeeRolesMaps.Remove(m);



                db.Employees.Remove(emp);

                await db.SaveChangesAsync().ConfigureAwait(false);

                return null;

            }

            catch (Exception ex)

            {

                return "Не удалось удалить: " + ex.Message;

            }

        }



        private static async System.Threading.Tasks.Task<bool> HasOwnerRoleAsync(DriveCareDBEntities db, Guid employeeId)
        {
            var roleNames = await (
                    from m in db.EmployeeRolesMaps
                    where m.EmployeeId == employeeId
                    join r in db.Roles on m.RoleId equals r.RowId
                    select r.Name)
                .ToListAsync()
                .ConfigureAwait(false);

            return roleNames.Any(AppState.IsOwnerRoleName);
        }

        private static async System.Threading.Tasks.Task SyncRolesAsync(

            DriveCareDBEntities db,

            Guid employeeId,

            ICollection<Guid> selected,

            OwnerOrganizationScope scope)

        {

            var allowed = (await scope.RolesForOrganization(db).Select(r => r.RowId).ToListAsync().ConfigureAwait(false))

                .ToHashSet();

            var target = selected.Where(allowed.Contains).Distinct().ToHashSet();



            var existing = await db.EmployeeRolesMaps

                .Where(m => m.EmployeeId == employeeId)

                .ToListAsync()

                .ConfigureAwait(false);



            foreach (var row in existing.Where(r => !target.Contains(r.RoleId)))

                db.EmployeeRolesMaps.Remove(row);



            var existingIds = existing.Select(r => r.RoleId).ToHashSet();

            foreach (var roleId in target.Where(id => !existingIds.Contains(id)))

            {

                db.EmployeeRolesMaps.Add(new EmployeeRolesMap

                {

                    RowId = Guid.NewGuid(),

                    EmployeeId = employeeId,

                    RoleId = roleId

                });

            }



            await db.SaveChangesAsync().ConfigureAwait(false);

        }



        private static SaveResult Fail(string message) =>

            new SaveResult { Success = false, ErrorMessage = message };

    }

}

