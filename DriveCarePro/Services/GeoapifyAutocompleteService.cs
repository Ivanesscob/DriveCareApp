using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
        public string ResultType { get; set; } = string.Empty;
        public double RankScore { get; set; }
    }

    /// <summary>Подсказки адреса Geoapify (до уровня дома).</summary>
    public static class GeoapifyAutocompleteService
    {
        private const string ApiKey = "6cecc8b686624b8ba329a80705d57a60";
        private static readonly HttpClient Client = CreateClient();

        static GeoapifyAutocompleteService()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // ignore
            }
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json; charset=utf-8");
            client.Timeout = TimeSpan.FromSeconds(20);
            return client;
        }

        public static async Task<IReadOnlyList<GeoapifyAddressSuggestion>> SearchAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2)
                return Array.Empty<GeoapifyAddressSuggestion>();

            var query = NormalizeQuery(text.Trim());

            var list = await FetchAndParseAsync(query, useCountryBias: true, cancellationToken)
                .ConfigureAwait(false);

            if (list.Count == 0)
            {
                list = await FetchAndParseAsync(query, useCountryBias: false, cancellationToken)
                    .ConfigureAwait(false);
            }

            return SortAndTrim(list, 12);
        }

        /// <summary>Расширяем популярные сокращения и исправляем частый ввод без дефиса.</summary>
        private static string NormalizeQuery(string text)
        {
            var t = text.Trim();
            var lower = t.ToLowerInvariant();

            if (lower == "спб")
                return "Санкт-Петербург";
            if (lower.StartsWith("спб ", StringComparison.Ordinal))
                return "Санкт-Петербург, " + t.Substring(4).TrimStart();

            if (lower == "питер" || lower == "питерburg")
                return "Санкт-Петербург";
            if (lower.StartsWith("питер ", StringComparison.Ordinal))
                return "Санкт-Петербург, " + t.Substring(6).TrimStart();

            if (lower == "ленинград")
                return "Санкт-Петербург";
            if (lower.StartsWith("ленинград ", StringComparison.Ordinal))
                return "Санкт-Петербург, " + t.Substring(10).TrimStart();

            if (lower == "мск")
                return "Москва";
            if (lower.StartsWith("мск ", StringComparison.Ordinal))
                return "Москва, " + t.Substring(4).TrimStart();

            // «санкт петербург», «санкт-петербург невский» → единый формат
            if (lower.StartsWith("санкт", StringComparison.Ordinal) && lower.Contains("петербург"))
            {
                var piterIdx = lower.IndexOf("петербург", StringComparison.Ordinal);
                var after = piterIdx >= 0
                    ? t.Substring(piterIdx + "петербург".Length).Trim(' ', ',', '-', '–')
                    : string.Empty;
                return string.IsNullOrWhiteSpace(after)
                    ? "Санкт-Петербург"
                    : "Санкт-Петербург, " + after;
            }

            // частая опечатка «зпетербург»
            if (lower.Contains("зпетербург"))
                t = t.Replace("зпетербург", "Петербург").Replace("Зпетербург", "Петербург");

            return CollapseSpaces(t);
        }

        private static string CollapseSpaces(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;
            var sb = new StringBuilder(text.Length);
            var prevSpace = false;
            foreach (var ch in text.Trim())
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                        prevSpace = true;
                    }
                }
                else
                {
                    sb.Append(ch);
                    prevSpace = false;
                }
            }
            return sb.ToString();
        }

        private static async Task<List<GeoapifyAddressSuggestion>> FetchAndParseAsync(
            string query,
            bool useCountryBias,
            CancellationToken cancellationToken)
        {
            var encoded = Uri.EscapeDataString(query);
            var url = new StringBuilder("https://api.geoapify.com/v1/geocode/autocomplete?")
                .Append("text=").Append(encoded)
                .Append("&apiKey=").Append(ApiKey)
                .Append("&lang=ru")
                .Append("&limit=20")
                .Append("&format=json");

            if (useCountryBias)
                url.Append("&bias=countrycode:ru,by,kz,ua");

            using (var response = await Client.GetAsync(url.ToString(), cancellationToken).ConfigureAwait(false))
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var detail = TruncateForMessage(json, 200);
                    throw new InvalidOperationException(
                        $"Geoapify: {(int)response.StatusCode} {response.ReasonPhrase}"
                        + (string.IsNullOrWhiteSpace(detail) ? string.Empty : " — " + detail));
                }

                return Parse(json);
            }
        }

        private static IReadOnlyList<GeoapifyAddressSuggestion> SortAndTrim(
            List<GeoapifyAddressSuggestion> list,
            int maxCount)
        {
            if (list == null || list.Count == 0)
                return Array.Empty<GeoapifyAddressSuggestion>();

            return list
                .OrderBy(s => GetTypeSortOrder(s.ResultType))
                .ThenByDescending(s => s.RankScore)
                .ThenBy(s => s.Label?.Length ?? 0)
                .Take(maxCount)
                .ToList();
        }

        private static int GetTypeSortOrder(string resultType)
        {
            switch ((resultType ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "country": return 0;
                case "state": return 1;
                case "city": return 2;
                case "locality": return 3;
                case "postcode": return 4;
                case "street": return 5;
                case "building": return 6;
                case "suburb": return 7;
                case "district": return 8;
                case "amenity": return 15;
                default: return 10;
            }
        }

        private static List<GeoapifyAddressSuggestion> Parse(string json)
        {
            var list = new List<GeoapifyAddressSuggestion>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                            TryAddFromResultItem(list, seen, item);
                        return list;
                    }

                    if (root.TryGetProperty("features", out var features) &&
                        features.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var feature in features.EnumerateArray())
                        {
                            if (feature.TryGetProperty("properties", out var props))
                                TryAddFromResultItem(list, seen, props);
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

        private static void TryAddFromResultItem(
            List<GeoapifyAddressSuggestion> list,
            HashSet<string> seen,
            JsonElement item)
        {
            var formatted = GetString(item, "formatted");
            var resultType = GetString(item, "result_type") ?? string.Empty;
            var street = FirstNonEmpty(
                GetString(item, "street"),
                GetString(item, "address_line1"));
            var housenumber = GetString(item, "housenumber");
            var city = FirstNonEmpty(
                GetString(item, "city"),
                GetString(item, "town"),
                GetString(item, "village"),
                GetString(item, "municipality"));
            var state = GetString(item, "state");
            if (string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(state) &&
                (resultType == "city" || resultType == "state" || resultType == "locality"))
                city = state;

            var name = GetString(item, "name");

            if (string.IsNullOrWhiteSpace(street) && !string.IsNullOrWhiteSpace(name) &&
                resultType != "city" && resultType != "state")
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

            var dedupeKey = (resultType + "|" + label).ToLowerInvariant();
            if (!seen.Add(dedupeKey))
                return;

            double? lat = TryGetDouble(item, "lat");
            double? lon = TryGetDouble(item, "lon");
            var rankScore = TryGetRankScore(item);

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
                Longitude = lon,
                ResultType = resultType,
                RankScore = rankScore
            });
        }

        private static double TryGetRankScore(JsonElement item)
        {
            if (!item.TryGetProperty("rank", out var rank) || rank.ValueKind != JsonValueKind.Object)
                return 0;

            if (rank.TryGetProperty("importance", out var imp) &&
                imp.ValueKind == JsonValueKind.Number &&
                imp.TryGetDouble(out var importance))
                return importance;

            if (rank.TryGetProperty("confidence", out var conf) &&
                conf.ValueKind == JsonValueKind.Number &&
                conf.TryGetDouble(out var confidence))
                return confidence;

            return 0;
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

        private static string TruncateForMessage(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;
            var t = text.Trim().Replace("\r", " ").Replace("\n", " ");
            return t.Length <= maxLen ? t : t.Substring(0, maxLen) + "…";
        }
    }
}
