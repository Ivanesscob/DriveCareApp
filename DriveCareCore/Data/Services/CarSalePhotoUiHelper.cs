using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DriveCareCore.Data.Services
{
    /// <summary>Разбор списка фото объявления и загрузка кадра для WPF (как в клиенте DriveCare).</summary>
    public static class CarSalePhotoUiHelper
    {
        private const char PhotoListSeparator = '|';

        public static IEnumerable<string> ParsePhotoTokens(string raw)
        {
            var source = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(source))
                yield break;

            var xmlMatches = Regex.Matches(source, "<photo>(.*?)</photo>", RegexOptions.IgnoreCase);
            if (xmlMatches.Count > 0)
            {
                foreach (Match match in xmlMatches)
                {
                    var xmlToken = match.Groups.Count > 1 ? match.Groups[1].Value.Trim() : string.Empty;
                    if (!string.IsNullOrWhiteSpace(xmlToken))
                        yield return xmlToken;
                }
                yield break;
            }

            foreach (var token in source.Split(new[] { PhotoListSeparator, ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = token.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    yield return trimmed;
            }
        }

        public static ImageSource ResolveSaleImage(string photoPathFromDb)
        {
            var raw = (photoPathFromDb ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            try
            {
                if (File.Exists(raw))
                    return LoadBitmap(raw);

                var downloadedByRaw = PhotoTcpStorageService.DownloadPhotoByName(raw);
                if (!string.IsNullOrWhiteSpace(downloadedByRaw))
                    return LoadBitmap(downloadedByRaw);

                var serverFileName = Path.GetFileName(raw);
                if (string.IsNullOrWhiteSpace(serverFileName))
                    return null;

                var downloadedByName = PhotoTcpStorageService.DownloadPhotoByName(serverFileName);
                return LoadBitmap(downloadedByName);
            }
            catch
            {
                return null;
            }
        }

        private static ImageSource LoadBitmap(string localPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                    return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(localPath, UriKind.Absolute);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }
    }
}
