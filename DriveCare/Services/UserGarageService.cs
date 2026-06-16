using DriveCareCore;
using DriveCareCore.Data.BD;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace DriveCare.Services
{
    public sealed class UserGarageCarItem
    {
        public Guid UserCarId { get; set; }
        public Guid CarId { get; set; }
        public Guid ModelId { get; set; }
        public Guid CarTypeId { get; set; }
        public Guid FuelTypeId { get; set; }
        public string BrandName { get; set; }
        public string ModelName { get; set; }
        public string CarTypeName { get; set; }
        public string FuelTypeName { get; set; }
        public int? Year { get; set; }
        public string Vin { get; set; }
        public string PlateNumber { get; set; }
        public string UserCarNote { get; set; }
        public string DisplayName { get; set; }
    }

    public sealed class CatalogEntry
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public Guid? BrandId { get; set; }
    }

    public sealed class UserGarageSaveInput
    {
        public Guid? UserCarId { get; set; }
        public Guid UserId { get; set; }
        public Guid ModelId { get; set; }
        public Guid CarTypeId { get; set; }
        public Guid FuelTypeId { get; set; }
        public int? Year { get; set; }
        public string Vin { get; set; }
        public string PlateNumber { get; set; }
        public string UserCarNote { get; set; }
    }

    public static class UserGarageService
    {
        public static bool CarsHaveVinPlateColumns()
        {
            try
            {
                const string sql = @"SELECT CASE WHEN COL_LENGTH(N'dbo.Cars', N'Vin') IS NOT NULL
                    AND COL_LENGTH(N'dbo.Cars', N'PlateNumber') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static List<UserGarageCarItem> LoadForUser(Guid userId)
        {
            if (userId == Guid.Empty)
                return new List<UserGarageCarItem>();

            var hasVin = CarsHaveVinPlateColumns();
            var vinCols = hasVin ? ", c.Vin, c.PlateNumber" : ", CAST(NULL AS NVARCHAR(50)) AS Vin, CAST(NULL AS NVARCHAR(20)) AS PlateNumber";

            try
            {
                var sql = @"
SELECT uc.RowId AS UserCarId, uc.CarId, uc.Description AS UserCarNote,
       c.ModelId, c.CarTypeId, c.FuelTypeId, c.Year" + vinCols + @",
       ISNULL(b.Name, N'') AS BrandName, ISNULL(m.Name, N'') AS ModelName,
       ISNULL(ct.Name, N'') AS CarTypeName, ISNULL(ft.Name, N'') AS FuelTypeName
FROM dbo.UserCars uc
INNER JOIN dbo.Cars c ON c.RowId = uc.CarId
LEFT JOIN dbo.Models m ON m.RowId = c.ModelId
LEFT JOIN dbo.Brands b ON b.RowId = m.BrandId
LEFT JOIN dbo.CarTypes ct ON ct.RowId = c.CarTypeId
LEFT JOIN dbo.FuelTypes ft ON ft.RowId = c.FuelTypeId
WHERE uc.UserId = @p0
ORDER BY b.Name, m.Name;";

                var rows = AppConnect.model1.Database.SqlQuery<GarageRow>(sql, userId).ToList();
                var seen = new HashSet<Guid>();
                var list = new List<UserGarageCarItem>();

                foreach (var r in rows)
                {
                    if (!seen.Add(r.CarId))
                        continue;
                    list.Add(Map(r));
                }

                return list;
            }
            catch
            {
                return new List<UserGarageCarItem>();
            }
        }

        public static UserGarageCarItem LoadByUserCarId(Guid userCarId, Guid userId)
        {
            return LoadForUser(userId).FirstOrDefault(c => c.UserCarId == userCarId);
        }

        public static List<CatalogEntry> LoadBrands() =>
            QueryCatalog("SELECT RowId AS Id, Name, CAST(NULL AS UNIQUEIDENTIFIER) AS BrandId FROM dbo.Brands ORDER BY Name");

        public static List<CatalogEntry> LoadModels(Guid? brandId)
        {
            if (!brandId.HasValue || brandId.Value == Guid.Empty)
                return QueryCatalog("SELECT RowId AS Id, Name, BrandId FROM dbo.Models ORDER BY Name");
            return QueryCatalog(
                "SELECT RowId AS Id, Name, BrandId FROM dbo.Models WHERE BrandId = @p0 ORDER BY Name",
                brandId.Value);
        }

        public static List<CatalogEntry> LoadCarTypes() =>
            QueryCatalog("SELECT RowId AS Id, Name, CAST(NULL AS UNIQUEIDENTIFIER) AS BrandId FROM dbo.CarTypes ORDER BY Name");

        public static List<CatalogEntry> LoadFuelTypes() =>
            QueryCatalog("SELECT RowId AS Id, Name, CAST(NULL AS UNIQUEIDENTIFIER) AS BrandId FROM dbo.FuelTypes ORDER BY Name");

        public static (bool ok, string error, Guid? userCarId) Save(UserGarageSaveInput input)
        {
            if (input == null || input.UserId == Guid.Empty)
                return (false, "Пользователь не указан.", null);
            if (input.ModelId == Guid.Empty || input.CarTypeId == Guid.Empty || input.FuelTypeId == Guid.Empty)
                return (false, "Выберите марку, модель, тип кузова и топливо.", null);

            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    if (input.UserCarId.HasValue && input.UserCarId.Value != Guid.Empty)
                    {
                        var uc = db.UserCars.FirstOrDefault(x =>
                            x.RowId == input.UserCarId.Value && x.UserId == input.UserId);
                        if (uc == null)
                            return (false, "Автомобиль не найден.", null);

                        var car = db.Cars.FirstOrDefault(c => c.RowId == uc.CarId);
                        if (car == null)
                            return (false, "Запись автомобиля не найдена.", null);

                        ApplyCarFields(car, input);
                        uc.Description = TrimOrNull(input.UserCarNote, 255);
                        db.SaveChanges();
                        ApplyVinPlateSql(db, car.RowId, input.Vin, input.PlateNumber);
                        return (true, null, uc.RowId);
                    }

                    var newCar = new Car
                    {
                        RowId = Guid.NewGuid(),
                        ModelId = input.ModelId,
                        CarTypeId = input.CarTypeId,
                        FuelTypeId = input.FuelTypeId,
                        Year = input.Year,
                        Description = BuildCarDescription(input)
                    };
                    db.Cars.Add(newCar);

                    var link = new UserCar
                    {
                        RowId = Guid.NewGuid(),
                        UserId = input.UserId,
                        CarId = newCar.RowId,
                        Description = TrimOrNull(input.UserCarNote, 255)
                    };
                    db.UserCars.Add(link);
                    db.SaveChanges();
                    ApplyVinPlateSql(db, newCar.RowId, input.Vin, input.PlateNumber);
                    return (true, null, link.RowId);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public static (bool ok, string error) Delete(Guid userCarId, Guid userId)
        {
            try
            {
                using (var db = new DriveCareDBEntities())
                {
                    var uc = db.UserCars.FirstOrDefault(x => x.RowId == userCarId && x.UserId == userId);
                    if (uc == null)
                        return (false, "Автомобиль не найден.");
                    db.UserCars.Remove(uc);
                    db.SaveChanges();
                    return (true, null);
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        static void ApplyCarFields(Car car, UserGarageSaveInput input)
        {
            car.ModelId = input.ModelId;
            car.CarTypeId = input.CarTypeId;
            car.FuelTypeId = input.FuelTypeId;
            car.Year = input.Year;
            car.Description = BuildCarDescription(input);
        }

        static string BuildCarDescription(UserGarageSaveInput input)
        {
            var parts = new List<string>();
            var mRow = AppConnect.model1.Models.Include("Brand")
                .FirstOrDefault(m => m.RowId == input.ModelId);
            if (mRow != null)
            {
                if (mRow.Brand != null && !string.IsNullOrWhiteSpace(mRow.Brand.Name))
                    parts.Add(mRow.Brand.Name.Trim());
                if (!string.IsNullOrWhiteSpace(mRow.Name))
                    parts.Add(mRow.Name.Trim());
            }
            if (input.Year.HasValue)
                parts.Add(input.Year.Value.ToString());
            return parts.Count > 0 ? string.Join(" ", parts) : null;
        }

        static void ApplyVinPlateSql(DriveCareDBEntities db, Guid carId, string vin, string plate)
        {
            if (!CarsHaveVinPlateColumns() || carId == Guid.Empty)
                return;
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.Cars SET Vin = @p0, PlateNumber = @p1 WHERE RowId = @p2;",
                TrimOrNull(vin, 50) ?? (object)DBNull.Value,
                TrimOrNull(plate, 20) ?? (object)DBNull.Value,
                carId);
        }

        static List<CatalogEntry> QueryCatalog(string sql, params object[] args) =>
            AppConnect.model1.Database.SqlQuery<CatalogEntry>(sql, args).ToList();

        static UserGarageCarItem Map(GarageRow r)
        {
            var brand = (r.BrandName ?? string.Empty).Trim();
            var model = (r.ModelName ?? string.Empty).Trim();
            string title;
            if (!string.IsNullOrEmpty(brand) && !string.IsNullOrEmpty(model))
                title = brand + " " + model;
            else if (!string.IsNullOrEmpty(model))
                title = model;
            else if (!string.IsNullOrEmpty(brand))
                title = brand;
            else
                title = "Автомобиль";

            if (r.Year.HasValue)
                title += " · " + r.Year.Value;

            return new UserGarageCarItem
            {
                UserCarId = r.UserCarId,
                CarId = r.CarId,
                ModelId = r.ModelId,
                CarTypeId = r.CarTypeId,
                FuelTypeId = r.FuelTypeId,
                BrandName = brand,
                ModelName = model,
                CarTypeName = (r.CarTypeName ?? string.Empty).Trim(),
                FuelTypeName = (r.FuelTypeName ?? string.Empty).Trim(),
                Year = r.Year,
                Vin = (r.Vin ?? string.Empty).Trim(),
                PlateNumber = (r.PlateNumber ?? string.Empty).Trim(),
                UserCarNote = (r.UserCarNote ?? string.Empty).Trim(),
                DisplayName = title
            };
        }

        static string TrimOrNull(string s, int max)
        {
            if (string.IsNullOrWhiteSpace(s))
                return null;
            var t = s.Trim();
            return t.Length > max ? t.Substring(0, max) : t;
        }

        sealed class GarageRow
        {
            public Guid UserCarId { get; set; }
            public Guid CarId { get; set; }
            public Guid ModelId { get; set; }
            public Guid CarTypeId { get; set; }
            public Guid FuelTypeId { get; set; }
            public int? Year { get; set; }
            public string Vin { get; set; }
            public string PlateNumber { get; set; }
            public string UserCarNote { get; set; }
            public string BrandName { get; set; }
            public string ModelName { get; set; }
            public string CarTypeName { get; set; }
            public string FuelTypeName { get; set; }
        }
    }
}
