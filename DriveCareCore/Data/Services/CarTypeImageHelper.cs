using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace DriveCareCore.Services
{
    /// <summary>
    /// Сопоставляет <see cref="CarTypes.Name"/> с PNG в Data/Pics/TypeCarPics/ (ресурс DriveCareCore).
    /// </summary>
    public static class CarTypeImageHelper
    {
        private const string PackPrefix = "pack://application:,,,/DriveCareCore;component/Data/Pics/TypeCarPics/";

        private static readonly (string[] Keys, string FileName)[] Map =
        {
            (new[] { "sedan", "седан" }, "SedanPic.png"),
            (new[] { "crossover", "кроссовер", "cuv", "suv" }, "CrossoverPic.png"),
            (new[] { "hatch", "хэтч", "хэтчбек", "hetch" }, "HetchBackPic.png"),
            (new[] { "moto", "мото", "мопед", "motorcycle", "motocycle", "bike" }, "MotocyclePic.png"),
        };

        public static BitmapImage GetImageForCarTypeName(string carTypeName)
        {
            var file = ResolveFileName(carTypeName);
            return LoadFromPack(file);
        }

        private static string ResolveFileName(string carTypeName)
        {
            if (string.IsNullOrWhiteSpace(carTypeName))
                return "SedanPic.png";

            var n = carTypeName.Trim().ToLowerInvariant();
            foreach (var row in Map)
            {
                if (row.Keys.Any(k => n.Contains(k)))
                    return row.FileName;
            }

            return "SedanPic.png";
        }

        private static BitmapImage LoadFromPack(string fileName)
        {
            var uri = new Uri(PackPrefix + fileName, UriKind.Absolute);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = uri;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }
}
