using DriveCareCore.Data.BD;
using System;
using System.Linq;

namespace DriveCarePro.Services
{
    internal static class CompanyCreationService
    {
        /// <summary>Роль «Владелец автосервиса».</summary>
        public static readonly Guid WorkshopOwnerRoleId =
            new Guid("AAEFDE24-DC8D-46EA-8A31-028CC44E41C7");

        public sealed class CreateCompanyInput
        {
            public string CompanyName { get; set; }
            public string CompanyDescription { get; set; }
            public string WorkshopName { get; set; }
            public string WorkshopDescription { get; set; }
            public Guid CountryId { get; set; }
            public string AddressLine { get; set; }
            public string ParsedCity { get; set; }
            public string ParsedStreet { get; set; }
            public string ParsedHouse { get; set; }
            public string Apartment { get; set; }
            public string LastName { get; set; }
            public string FirstName { get; set; }
            public string MidName { get; set; }
            public string Login { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public DateTime BirthDate { get; set; }
        }

        public sealed class CreateCompanyResult
        {
            public bool Success { get; set; }
            public string ErrorMessage { get; set; }
            public Guid CompanyId { get; set; }
            public Guid WorkshopId { get; set; }
            public Guid EmployeeId { get; set; }
        }

        public static CreateCompanyResult Create(CreateCompanyInput input)
        {
            if (input == null)
                return Fail("Нет данных для сохранения.");

            var db = AppConnect.model1;
            var login = (input.Login ?? string.Empty).Trim();
            var email = (input.Email ?? string.Empty).Trim();

            if (db.Employees.Any(e => e.Login == login))
                return Fail("Сотрудник с таким логином уже существует.");

            if (db.Employees.Any(e => e.Email != null && e.Email.Trim().Equals(email, StringComparison.OrdinalIgnoreCase)))
                return Fail("Сотрудник с таким email уже существует.");

            if (!db.Roles.Any(r => r.RowId == WorkshopOwnerRoleId))
                return Fail("Роль владельца автосервиса не найдена в базе. Проверьте справочник ролей.");

            var addressId = Guid.NewGuid();
            var companyId = Guid.NewGuid();
            var workshopId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();
            var roleMapId = Guid.NewGuid();

            var city = Truncate(NullIfEmpty(input.ParsedCity), 100);
            var street = Truncate(NullIfEmpty(input.ParsedStreet), 200);
            var house = Truncate(NullIfEmpty(input.ParsedHouse), 50);
            if (string.IsNullOrEmpty(city) && string.IsNullOrEmpty(street) && string.IsNullOrEmpty(house))
            {
                city = Truncate((input.AddressLine ?? string.Empty).Trim(), 100);
            }

            var address = new Address
            {
                RowId = addressId,
                CountryId = input.CountryId,
                City = city,
                Street = street,
                House = house,
                Apartment = Truncate(NullIfEmpty(input.Apartment), 50),
                Description = null
            };

            var company = new Company
            {
                RowId = companyId,
                Name = Truncate((input.CompanyName ?? string.Empty).Trim(), 200),
                Description = Truncate(NullIfEmpty(input.CompanyDescription), 255)
            };

            var workshop = new Workshop
            {
                RowId = workshopId,
                Name = Truncate((input.WorkshopName ?? string.Empty).Trim(), 200),
                CompanyId = companyId,
                AddressId = addressId,
                Description = Truncate(NullIfEmpty(input.WorkshopDescription), 255)
            };

            var employee = new Employee
            {
                RowId = employeeId,
                LastName = Truncate((input.LastName ?? string.Empty).Trim(), 100),
                FirstName = Truncate((input.FirstName ?? string.Empty).Trim(), 100),
                MidName = Truncate(NullIfEmpty(input.MidName), 100),
                Login = Truncate(login, 100),
                Password = input.Password ?? string.Empty,
                Email = Truncate(email, 100),
                Phone = Truncate((input.Phone ?? string.Empty).Trim(), 50),
                BirthDate = input.BirthDate.Date,
                HireDate = DateTime.Now,
                IsActive = true,
                WorkshopId = workshopId,
                Description = null
            };

            var roleMap = new EmployeeRolesMap
            {
                RowId = roleMapId,
                EmployeeId = employeeId,
                RoleId = WorkshopOwnerRoleId,
                Description = "Владелец при создании организации"
            };

            using (var tx = db.Database.BeginTransaction())
            {
                try
                {
                    db.Addresses.Add(address);
                    db.Companies.Add(company);
                    db.Workshops.Add(workshop);
                    db.Employees.Add(employee);
                    db.EmployeeRolesMaps.Add(roleMap);
                    db.SaveChanges();
                    tx.Commit();

                    return new CreateCompanyResult
                    {
                        Success = true,
                        CompanyId = companyId,
                        WorkshopId = workshopId,
                        EmployeeId = employeeId
                    };
                }
                catch (Exception ex)
                {
                    tx.Rollback();
                    return Fail(FormatDbError(ex));
                }
            }
        }

        private static CreateCompanyResult Fail(string message)
        {
            return new CreateCompanyResult { Success = false, ErrorMessage = message };
        }

        private static string FormatDbError(Exception ex)
        {
            var msg = ex.Message;
            var inner = ex.InnerException;
            while (inner != null)
            {
                msg = inner.Message;
                inner = inner.InnerException;
            }
            return "Не удалось сохранить в базу: " + msg;
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string Truncate(string value, int maxLen)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            return value.Length <= maxLen ? value : value.Substring(0, maxLen);
        }
    }
}
