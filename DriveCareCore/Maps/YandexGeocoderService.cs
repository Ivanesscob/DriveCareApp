using Newtonsoft.Json.Linq;
using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace DriveCareCore.Maps
{
    public static class YandexGeocoderService
    {
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

        public static async Task<(double lat, double lon)?> TryGeocodeSpbAsync(string addressLine)
        {
            if (string.IsNullOrWhiteSpace(addressLine))
                return null;

            var query = addressLine.Trim();
            if (query.IndexOf("санкт", StringComparison.OrdinalIgnoreCase) < 0 &&
                query.IndexOf("спб", StringComparison.OrdinalIgnoreCase) < 0 &&
                query.IndexOf("петербург", StringComparison.OrdinalIgnoreCase) < 0)
            {
                query = "Санкт-Петербург, " + query;
            }

            var url =
                "https://geocode-maps.yandex.ru/1.x/?apikey=" + HttpUtility.UrlEncode(YandexMapsConfig.ApiKey) +
                "&format=json&results=1&geocode=" + HttpUtility.UrlEncode(query);

            try
            {
                var json = await Http.GetStringAsync(url).ConfigureAwait(false);
                var root = JObject.Parse(json);
                var pos = root["response"]?["GeoObjectCollection"]?["featureMember"]?[0]?["GeoObject"]?["Point"]?["pos"]?.ToString();
                if (string.IsNullOrWhiteSpace(pos))
                    return null;

                var parts = pos.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    return null;

                if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                    return null;
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                    return null;

                return (lat, lon);
            }
            catch
            {
                return null;
            }
        }
    }
}
