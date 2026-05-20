using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DriveCarePro.Services
{
    public sealed class GeoapifyAddressSuggestion
    {
        public string Label { get; set; }
        public string City { get; set; }
        public string Street { get; set; }
        public string House { get; set; }
        public string CountryName { get; set; }
        public string CountryCode { get; set; }
        public string Postcode { get; set; }
        public bool HasHouseNumber { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }

    /// <summary>Подсказки адреса Geoapify (до уровня дома).</summary>
    public static class GeoapifyAutocompleteService
    {
        private const string ApiKey = "6cecc8b686624b8ba329a80705d57a60";
        private static readonly HttpClient Client = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
            return client;
        }

        public static async Task<IReadOnlyList<GeoapifyAddressSuggestion>> SearchAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3)
                return Array.Empty<GeoapifyAddressSuggestion>();

            var query = Uri.EscapeDataString(text.Trim());
            // format=json → ответ с массивом "results" (не GeoJSON "features")
            var url = "https://api.geoapify.com/v1/geocode/autocomplete" +
                      $"?text={query}&apiKey={ApiKey}&lang=ru&limit=12&format=json" +
                      "&filter=countrycode:ru,by,kz,ua";

            using (var response = await Client.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(
                        $"Geoapify: {(int)response.StatusCode} {response.ReasonPhrase}");

                return Parse(json);
            }
        }

        private static IReadOnlyList<GeoapifyAddressSuggestion> Parse(string json)
        {
            var list = new List<GeoapifyAddressSuggestion>();
            if (string.IsNullOrWhiteSpace(json))
                return list;

            try
            {
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;

                    if (root.TryGetProperty("results", out var results) &&
                        results.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in results.EnumerateArray())
                            TryAddFromResultItem(list, item);
                        return list;
                    }

                    // Запасной вариант: GeoJSON features
                    if (root.TryGetProperty("features", out var features) &&
                        features.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var feature in features.EnumerateArray())
                        {
                            if (feature.TryGetProperty("properties", out var props))
                                TryAddFromResultItem(list, props);
                        }
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }

            return list;
        }

        private static void TryAddFromResultItem(List<GeoapifyAddressSuggestion> list, JsonElement item)
        {
            var formatted = GetString(item, "formatted");
            var street = FirstNonEmpty(
                GetString(item, "street"),
                GetString(item, "address_line1"));
            var housenumber = GetString(item, "housenumber");
            var city = FirstNonEmpty(
                GetString(item, "city"),
                GetString(item, "town"),
                GetString(item, "village"),
                GetString(item, "municipality"),
                GetString(item, "state"),
                GetString(item, "county"));
            var name = GetString(item, "name");

            if (string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(name))
                street = name;

            if (string.IsNullOrWhiteSpace(formatted) &&
                string.IsNullOrWhiteSpace(city) &&
                string.IsNullOrWhiteSpace(street))
                return;

            var label = !string.IsNullOrWhiteSpace(formatted)
                ? formatted.Trim()
                : BuildLabel(city, street, housenumber);

            if (string.IsNullOrWhiteSpace(label))
                return;

            double? lat = TryGetDouble(item, "lat");
            double? lon = TryGetDouble(item, "lon");

            list.Add(new GeoapifyAddressSuggestion
            {
                Label = label,
                City = city ?? string.Empty,
                Street = street ?? string.Empty,
                House = housenumber ?? string.Empty,
                CountryName = GetString(item, "country") ?? string.Empty,
                CountryCode = GetString(item, "country_code") ?? string.Empty,
                Postcode = GetString(item, "postcode") ?? string.Empty,
                HasHouseNumber = !string.IsNullOrWhiteSpace(housenumber),
                Latitude = lat,
                Longitude = lon
            });
        }

        private static string BuildLabel(string city, string street, string house)
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(city))
                parts.Add(city.Trim());
            if (!string.IsNullOrWhiteSpace(street))
                parts.Add(street.Trim());
            if (!string.IsNullOrWhiteSpace(house))
                parts.Add(house.Trim());
            return parts.Count > 0 ? string.Join(", ", parts) : string.Empty;
        }

        private static string GetString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var prop))
                return null;
            if (prop.ValueKind == JsonValueKind.Null)
                return null;
            return prop.GetString();
        }

        private static double? TryGetDouble(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var prop))
                return null;
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var d))
                return d;
            if (prop.ValueKind == JsonValueKind.String)
            {
                var s = prop.GetString();
                if (DriveCareCore.Maps.CoordinateHelper.TryParse(s, out var v))
                    return v;
            }
            return null;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                    return v.Trim();
            }
            return null;
        }
    }
}
