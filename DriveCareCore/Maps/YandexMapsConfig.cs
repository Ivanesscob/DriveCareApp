using System.Configuration;

namespace DriveCareCore.Maps
{
    public static class YandexMapsConfig
    {
        public const string DefaultApiKey = "491c42cf-56dc-4977-8064-d8421b9a5d04";

        /// <summary>Центр карты: Санкт-Петербург [широта, долгота].</summary>
        public const double DefaultCenterLat = 59.9386;
        public const double DefaultCenterLon = 30.3141;
        public const int DefaultZoom = 10;

        public static string ApiKey
        {
            get
            {
                var fromConfig = ConfigurationManager.AppSettings["YandexMapsApiKey"];
                return string.IsNullOrWhiteSpace(fromConfig) ? DefaultApiKey : fromConfig.Trim();
            }
        }
    }
}
