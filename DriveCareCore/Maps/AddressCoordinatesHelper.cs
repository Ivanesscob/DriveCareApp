using DriveCareCore.Data.BD;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace DriveCareCore.Maps
{
    public static class AddressCoordinatesHelper
    {
        public static bool ColumnsExist()
        {
            try
            {
                const string sql = @"
SELECT CASE WHEN COL_LENGTH(N'dbo.Addresses', N'Latitude') IS NOT NULL
             AND COL_LENGTH(N'dbo.Addresses', N'Longitude') IS NOT NULL THEN 1 ELSE 0 END;";
                return AppConnect.model1.Database.SqlQuery<int>(sql).FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        public static Task SaveAsync(Guid addressId, double latitude, double longitude) =>
            WithDb(db => SaveAsync(db, addressId, latitude, longitude));

        public static async Task SaveAsync(DriveCareDBEntities db, Guid addressId, double latitude, double longitude)
        {
            if (addressId == Guid.Empty || !ColumnsExist())
                return;

            var lat = CoordinateHelper.NormalizeLatitude(latitude) ?? latitude;
            var lon = CoordinateHelper.NormalizeLongitude(longitude) ?? longitude;

            await db.Database.ExecuteSqlCommandAsync(
                "UPDATE dbo.Addresses SET Latitude = @lat, Longitude = @lon WHERE RowId = @id",
                new SqlParameter("@lat", lat),
                new SqlParameter("@lon", lon),
                new SqlParameter("@id", addressId)).ConfigureAwait(false);
        }

        public static async Task SaveFromAddressLineAsync(Guid addressId, string addressLine)
        {
            if (addressId == Guid.Empty || string.IsNullOrWhiteSpace(addressLine))
                return;

            var geo = await YandexGeocoderService.TryGeocodeSpbAsync(addressLine).ConfigureAwait(false);
            if (!geo.HasValue)
                return;

            await SaveAsync(addressId, geo.Value.lat, geo.Value.lon).ConfigureAwait(false);
        }

        static async Task WithDb(Func<DriveCareDBEntities, Task> work)
        {
            using (var db = new DriveCareDBEntities())
                await work(db).ConfigureAwait(false);
        }
    }
}
