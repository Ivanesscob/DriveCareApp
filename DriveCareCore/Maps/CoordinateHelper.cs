using System;
using System.Globalization;

namespace DriveCareCore.Maps
{
    public static class CoordinateHelper
    {
        /// <summary>Парсит широту/долготу: допускается точка или запятая (59,93).</summary>
        public static bool TryParse(string text, out double value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var s = text.Trim().Replace(',', '.');
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static double? NormalizeLatitude(double? lat)
        {
            if (!lat.HasValue)
                return null;
            var v = lat.Value;
            return v >= -90 && v <= 90 ? v : (double?)null;
        }

        public static double? NormalizeLongitude(double? lon)
        {
            if (!lon.HasValue)
                return null;
            var v = lon.Value;
            return v >= -180 && v <= 180 ? v : (double?)null;
        }
    }
}
