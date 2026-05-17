using DriveCareCore.Data.BD;
using DriveCarePro.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCarePro.Services.ServiceBooking
{
    internal static class ServiceClientLookupService
    {
        public static string NormalizePhoneDigits(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var sb = new StringBuilder(16);
            foreach (var ch in phone)
            {
                if (char.IsDigit(ch))
                    sb.Append(ch);
            }

            var d = sb.ToString();
            if (d.Length == 11 && (d[0] == '7' || d[0] == '8'))
                return d.Substring(1);
            return d;
        }

        public static async Task<UserLookupResult> FindUserAsync(string email, string phone)
        {
            var emailTrim = (email ?? string.Empty).Trim();
            var phoneDigits = NormalizePhoneDigits(phone);

            if (string.IsNullOrEmpty(emailTrim) && string.IsNullOrEmpty(phoneDigits))
                return UserLookupResult.Fail("Укажите email или номер телефона.");

            if (!string.IsNullOrEmpty(emailTrim) && !ContactValidation.IsValidEmail(emailTrim))
                return UserLookupResult.Fail("Некорректный email.");

            if (!string.IsNullOrEmpty(phoneDigits) && phoneDigits.Length < 10)
                return UserLookupResult.Fail("Некорректный номер телефона.");

            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                User user = null;
                if (!string.IsNullOrEmpty(emailTrim))
                {
                    user = await db.Users.FirstOrDefaultAsync(u => u.Email == emailTrim).ConfigureAwait(false);
                }

                if (user == null && !string.IsNullOrEmpty(phoneDigits))
                {
                    var users = await db.Users.Where(u => u.Phone != null && u.Phone != "").ToListAsync().ConfigureAwait(false);
                    user = users.FirstOrDefault(u => NormalizePhoneDigits(u.Phone) == phoneDigits);
                }

                if (user == null)
                    return UserLookupResult.NotFound();

                var cars = await LoadUserCarsAsync(db, user.RowId).ConfigureAwait(false);
                return UserLookupResult.Found(user, cars);
            }).ConfigureAwait(false);
        }

        public static async Task<List<UserCarOption>> LoadUserCarsAsync(DriveCareDBEntities db, Guid userId)
        {
            var query = from uc in db.UserCars
                        where uc.UserId == userId
                        join c in db.Cars on uc.CarId equals c.RowId
                        join m in db.Models on c.ModelId equals m.RowId
                        join b in db.Brands on m.BrandId equals b.RowId
                        select new { uc, c, m, b };

            var rows = await query.ToListAsync().ConfigureAwait(false);
            var list = new List<UserCarOption>();

            foreach (var x in rows)
            {
                var color = await (
                    from cc in db.CarColors
                    join col in db.Colors on cc.ColorId equals col.RowId
                    where cc.CarId == x.c.RowId && cc.EndDate == null
                    orderby cc.StartDate descending
                    select col.Name
                ).FirstOrDefaultAsync().ConfigureAwait(false);

                var brand = (x.b.Name ?? string.Empty).Trim();
                var model = (x.m.Name ?? string.Empty).Trim();
                var year = x.c.Year.HasValue ? " " + x.c.Year.Value : string.Empty;
                var display = (brand + " " + model + year).Trim();
                if (string.IsNullOrEmpty(display))
                    display = (x.uc.Description ?? x.c.Description ?? "Автомобиль").Trim();

                list.Add(new UserCarOption
                {
                    CarId = x.c.RowId,
                    UserCarId = x.uc.RowId,
                    DisplayName = display,
                    Year = x.c.Year,
                    Color = color ?? string.Empty,
                    Vin = ExtractTag(x.c.Description, "VIN:"),
                    PlateNumber = ExtractTag(x.c.Description, "Гос:")
                });
            }

            return list.OrderBy(c => c.DisplayName).ToList();
        }

        public static async Task<UserCarOption> LoadCarOptionAsync(Guid carId, Guid userId)
        {
            return await DatabaseExecutor.WithDbAsync(async db =>
            {
                var cars = await LoadUserCarsAsync(db, userId).ConfigureAwait(false);
                return cars.FirstOrDefault(c => c.CarId == carId);
            }).ConfigureAwait(false);
        }

        private static string ExtractTag(string description, string prefix)
        {
            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(prefix))
                return string.Empty;

            foreach (var line in description.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = line.Trim();
                if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return t.Substring(prefix.Length).Trim();
            }

            return string.Empty;
        }

        public sealed class UserLookupResult
        {
            public bool Success { get; private set; }
            public bool IsFound { get; private set; }
            public string ErrorMessage { get; private set; }
            public User User { get; private set; }
            public List<UserCarOption> Cars { get; private set; } = new List<UserCarOption>();

            public static UserLookupResult Found(User user, List<UserCarOption> cars) =>
                new UserLookupResult { Success = true, IsFound = true, User = user, Cars = cars ?? new List<UserCarOption>() };

            public static UserLookupResult NotFound() =>
                new UserLookupResult { Success = true, IsFound = false };

            public static UserLookupResult Fail(string message) =>
                new UserLookupResult { Success = false, ErrorMessage = message };
        }
    }
}
