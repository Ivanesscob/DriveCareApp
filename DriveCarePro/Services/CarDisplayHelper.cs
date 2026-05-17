using DriveCareCore.Data.BD;
using System;
using System.Linq;

namespace DriveCarePro.Services
{
    /// <summary>Отображение авто: приоритет — текст, введённый при записи (Description), а не первая модель из справочника.</summary>
    public static class CarDisplayHelper
    {
        public static string FormatCar(DriveCareDBEntities db, Car car) =>
            car == null ? "Не указано." : FormatCar(db, car.RowId);

        public static string FormatCar(DriveCareDBEntities db, Guid? carId)
        {
            if (!carId.HasValue)
                return "Не указано.";

            var car = db.Cars.AsNoTracking().FirstOrDefault(c => c.RowId == carId.Value);
            if (car == null)
                return "Автомобиль не найден в базе.";

            var year = car.Year.HasValue ? $" · {car.Year.Value} г." : string.Empty;
            var desc = car.Description?.Trim() ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(desc))
            {
                var mainPart = desc.Split(new[] { ';' }, 2)[0].Trim();
                if (mainPart.Length > 0 &&
                    !mainPart.StartsWith("VIN:", StringComparison.OrdinalIgnoreCase))
                    return mainPart + year;
            }

            var row = (from m in db.Models.AsNoTracking()
                       join b in db.Brands.AsNoTracking() on m.BrandId equals b.RowId
                       where m.RowId == car.ModelId
                       select new { Brand = b.Name, Model = m.Name }).FirstOrDefault();

            if (row == null)
                return string.IsNullOrWhiteSpace(desc) ? "Не указано." : desc;

            var brandModel = $"{(row.Brand ?? "").Trim()} {(row.Model ?? "").Trim()}".Trim();
            if (!string.IsNullOrWhiteSpace(desc))
                return $"{brandModel}{year}\n{desc}";

            return string.IsNullOrWhiteSpace(brandModel) ? "Не указано." : brandModel + year;
        }

        public static string FormatCarById(DriveCareDBEntities db, Guid? carId) =>
            FormatCar(db, carId);

        public static string ExtractTag(string description, string prefix)
        {
            if (string.IsNullOrWhiteSpace(description) || string.IsNullOrWhiteSpace(prefix))
                return string.Empty;

            foreach (var part in description.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = part.Trim();
                if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return t.Substring(prefix.Length).Trim();
            }

            return string.Empty;
        }

        public static string ExtractVin(string description) => ExtractTag(description, "VIN:");

        public static string ExtractPlate(string description) => ExtractTag(description, "Гос:");
    }
}
