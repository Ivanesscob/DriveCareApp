using DriveCareCore.Data.BD;
using System;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.ServiceBooking
{
    internal static class ServiceBookingPersistenceService
    {
        public static async Task<SaveBookingResult> SaveBookingAsync(ServiceBookingContext ctx)
        {
            if (ctx?.Scope == null)
                return SaveBookingResult.Fail("Нет контекста организации.");

            try
            {
                return await DatabaseExecutor.WithDbAsync(db => SaveBookingCoreAsync(db, ctx)).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return SaveBookingResult.Fail(
                    "Не найдены таблицы WorkshopServiceClients / WorkshopGuestCars. " +
                    "Выполните скрипт DriveCareCore\\Data\\BD\\Sql\\WorkshopServiceBooking_Tables.sql");
            }
            catch (Exception ex)
            {
                return SaveBookingResult.Fail("Не удалось сохранить запись: " + ex.Message);
            }
        }

        private static async Task<SaveBookingResult> SaveBookingCoreAsync(DriveCareDBEntities db, ServiceBookingContext ctx)
        {
            var workshopId = ctx.WorkshopId;
            if (workshopId == Guid.Empty && AppState.CurrentEmployee?.WorkshopId != null)
                workshopId = AppState.CurrentEmployee.WorkshopId.Value;

            Guid carId;
            Guid? userCarId = null;
            var linkToUser = false;

            switch (ctx.ClientPath)
            {
                case ServiceClientPath.ExistingUserWithSelectedCar:
                    carId = ctx.SelectedCarId.Value;
                    userCarId = ctx.SelectedUserCarId;
                    linkToUser = true;
                    break;

                case ServiceClientPath.ExistingUserWithNewCar:
                case ServiceClientPath.NewUserRegistered:
                    carId = await CreateCarAsync(db, ctx).ConfigureAwait(false);
                    if (ctx.FoundUser != null || ctx.ClientPath == ServiceClientPath.NewUserRegistered)
                    {
                        var userId = ctx.FoundUser?.RowId ?? await ResolveUserIdForNewRegistration(db, ctx).ConfigureAwait(false);
                        userCarId = await LinkCarToUserAsync(db, userId, carId, ctx.CarDescription).ConfigureAwait(false);
                        linkToUser = true;
                    }
                    break;

                default:
                    carId = await CreateCarAsync(db, ctx).ConfigureAwait(false);
                    break;
            }

            var clientId = await InsertServiceClientAsync(db, ctx, workshopId).ConfigureAwait(false);
            ctx.CreatedServiceClientId = clientId;

            var repairId = await CreateRepairHistoryAsync(db, ctx, carId).ConfigureAwait(false);
            ctx.CreatedRepairHistoryId = repairId;

            await InsertGuestCarAsync(db, ctx, workshopId, clientId, carId, repairId, userCarId, linkToUser).ConfigureAwait(false);

            var taskId = await ServiceBookingTaskService.CreateForBookingAsync(db, ctx, carId, repairId).ConfigureAwait(false);
            ctx.CreatedTaskId = taskId;

            return SaveBookingResult.Ok(repairId, carId, clientId, taskId);
        }

        private static async Task<Guid> ResolveUserIdForNewRegistration(DriveCareDBEntities db, ServiceBookingContext ctx)
        {
            if (ctx.FoundUser != null)
                return ctx.FoundUser.RowId;

            var email = (ctx.ClientEmail ?? string.Empty).Trim();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email).ConfigureAwait(false);
            return user?.RowId ?? Guid.Empty;
        }

        private static async Task<Guid> CreateCarAsync(DriveCareDBEntities db, ServiceBookingContext ctx)
        {
            var modelId = await db.Models.Select(m => m.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
            var carTypeId = await db.CarTypes.Select(t => t.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
            var fuelTypeId = await db.FuelTypes.Select(f => f.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
            if (modelId == Guid.Empty || carTypeId == Guid.Empty || fuelTypeId == Guid.Empty)
                throw new InvalidOperationException("В справочниках нет моделей/типов автомобилей.");

            int? year = null;
            if (int.TryParse(ctx.Year, out var y))
                year = y;

            var desc = BuildCarDescription(ctx);
            var car = new Car
            {
                RowId = Guid.NewGuid(),
                ModelId = modelId,
                CarTypeId = carTypeId,
                FuelTypeId = fuelTypeId,
                Year = year,
                Description = desc
            };
            db.Cars.Add(car);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return car.RowId;
        }

        private static string BuildCarDescription(ServiceBookingContext ctx)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(ctx.CarDescription))
                sb.Append(ctx.CarDescription.Trim());
            AppendTag(sb, "VIN:", ctx.Vin);
            AppendTag(sb, "Гос:", ctx.PlateNumber);
            AppendTag(sb, "Цвет:", ctx.Color);
            return sb.ToString();
        }

        private static string BuildRepairDescription(ServiceBookingContext ctx)
        {
            var reason = ctx.VisitReason?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(ctx.SpecialNotes))
                return reason;
            return reason + Environment.NewLine + Environment.NewLine +
                   "Особые данные: " + ctx.SpecialNotes.Trim();
        }

        private static void AppendTag(StringBuilder sb, string tag, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            if (sb.Length > 0)
                sb.Append("; ");
            sb.Append(tag).Append(' ').Append(value.Trim());
        }

        private static async Task<Guid> LinkCarToUserAsync(DriveCareDBEntities db, Guid userId, Guid carId, string description)
        {
            var uc = new UserCar
            {
                RowId = Guid.NewGuid(),
                UserId = userId,
                CarId = carId,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
            };
            db.UserCars.Add(uc);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return uc.RowId;
        }

        private static async Task<Guid> InsertServiceClientAsync(DriveCareDBEntities db, ServiceBookingContext ctx, Guid workshopId)
        {
            var id = Guid.NewGuid();
            var userId = ctx.FoundUser?.RowId;
            var isReg = ctx.ClientPath == ServiceClientPath.NewUserRegistered ||
                        ctx.ClientPath == ServiceClientPath.ExistingUserWithSelectedCar ||
                        ctx.ClientPath == ServiceClientPath.ExistingUserWithNewCar;

            await db.Database.ExecuteSqlCommandAsync(
                @"INSERT INTO WorkshopServiceClients (RowId, WorkshopId, UserId, FullName, Phone, Email, IsRegisteredUser, CreatedAt)
                  VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7)",
                id, workshopId, (object)userId ?? DBNull.Value,
                ctx.ClientFullName ?? string.Empty,
                (object)ctx.ClientPhone ?? string.Empty,
                (object)ctx.ClientEmail ?? string.Empty,
                isReg, DateTime.Now).ConfigureAwait(false);

            return id;
        }

        private static async Task<Guid> CreateRepairHistoryAsync(DriveCareDBEntities db, ServiceBookingContext ctx, Guid carId)
        {
            var statusId = await db.Statuses.Select(s => s.RowId).FirstOrDefaultAsync().ConfigureAwait(false);
            var categoryId = await db.RepairCategories.Select(c => c.RowId).FirstOrDefaultAsync().ConfigureAwait(false);

            int? mileage = null;
            if (int.TryParse(ctx.Mileage, out var m))
                mileage = m;

            var repair = new RepairHistory
            {
                RowId = Guid.NewGuid(),
                CarId = carId,
                EmployeeId = AppState.CurrentEmployee?.RowId,
                Title = ctx.RepairTypeDisplay,
                Description = BuildRepairDescription(ctx),
                RepairDate = DateTime.Now,
                Mileage = mileage,
                StatusId = statusId == Guid.Empty ? (Guid?)null : statusId,
                CategoryId = categoryId == Guid.Empty ? (Guid?)null : categoryId,
                CreatedAt = DateTime.Now
            };

            db.RepairHistories.Add(repair);
            await db.SaveChangesAsync().ConfigureAwait(false);
            return repair.RowId;
        }

        private static async Task InsertGuestCarAsync(
            DriveCareDBEntities db,
            ServiceBookingContext ctx,
            Guid workshopId,
            Guid clientId,
            Guid carId,
            Guid repairId,
            Guid? userCarId,
            bool linkToUser)
        {
            int? year = null;
            if (int.TryParse(ctx.Year, out var y))
                year = y;
            int? mileage = null;
            if (int.TryParse(ctx.Mileage, out var m))
                mileage = m;

            await db.Database.ExecuteSqlCommandAsync(
                @"INSERT INTO WorkshopGuestCars
                  (RowId, WorkshopId, ServiceClientId, CarId, RepairHistoryId, Vin, PlateNumber, BrandModelText, [Year], Color, Mileage, IsLinkedToUser, UserCarId, CreatedAt)
                  VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12,@p13)",
                Guid.NewGuid(), workshopId, clientId, carId, repairId,
                (object)ctx.Vin ?? string.Empty,
                (object)ctx.PlateNumber ?? string.Empty,
                (object)ctx.CarDescription ?? string.Empty,
                (object)year ?? DBNull.Value,
                (object)ctx.Color ?? string.Empty,
                (object)mileage ?? DBNull.Value,
                linkToUser,
                (object)userCarId ?? DBNull.Value,
                DateTime.Now).ConfigureAwait(false);
        }

        public static async Task<RegisterUserResult> RegisterUserAsync(string login, string email, string phone, string password)
        {
            login = (login ?? string.Empty).Trim();
            email = (email ?? string.Empty).Trim();
            phone = (phone ?? string.Empty).Trim();
            password = (password ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                return RegisterUserResult.Fail("Заполните логин, email и пароль.");

            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                if (await db.Users.AnyAsync(u => u.Login == login).ConfigureAwait(false))
                    return RegisterUserResult.Fail("Логин уже занят.");
                if (await db.Users.AnyAsync(u => u.Email == email).ConfigureAwait(false))
                    return RegisterUserResult.Fail("Email уже зарегистрирован.");

                var user = new User
                {
                    RowId = Guid.NewGuid(),
                    Login = login,
                    Password = password,
                    Email = email,
                    Phone = string.IsNullOrEmpty(phone) ? null : phone,
                    CreatedAt = DateTime.Now
                };
                db.Users.Add(user);
                await db.SaveChangesAsync().ConfigureAwait(false);
                return RegisterUserResult.Ok(user);
            }).ConfigureAwait(false);
        }

        public sealed class SaveBookingResult
        {
            public bool Success { get; private set; }
            public string ErrorMessage { get; private set; }
            public Guid RepairHistoryId { get; private set; }
            public Guid CarId { get; private set; }
            public Guid ServiceClientId { get; private set; }
            public Guid? TaskId { get; private set; }

            public static SaveBookingResult Ok(Guid repairId, Guid carId, Guid clientId, Guid? taskId) =>
                new SaveBookingResult
                {
                    Success = true,
                    RepairHistoryId = repairId,
                    CarId = carId,
                    ServiceClientId = clientId,
                    TaskId = taskId
                };

            public static SaveBookingResult Fail(string msg) =>
                new SaveBookingResult { Success = false, ErrorMessage = msg };
        }

        public sealed class RegisterUserResult
        {
            public bool Success { get; private set; }
            public string ErrorMessage { get; private set; }
            public User User { get; private set; }

            public static RegisterUserResult Ok(User user) =>
                new RegisterUserResult { Success = true, User = user };

            public static RegisterUserResult Fail(string msg) =>
                new RegisterUserResult { Success = false, ErrorMessage = msg };
        }
    }
}
