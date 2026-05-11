using System;
using System.IO;
using System.Text.Json;

namespace DriveCarePro.Services
{
    /// <summary>Последняя тема интерфейса на этом ПК (экран входа и до входа сотрудника).</summary>
    internal static class AppearanceStore
    {
        private const string FileName = "appearance.json";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private sealed class Dto
        {
            public string UiTheme { get; set; } = "Dark";
        }

        private static string GetPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveCarePro",
                FileName);
        }

        public static AppUiTheme Load()
        {
            try
            {
                var path = GetPath();
                if (!File.Exists(path))
                    return AppUiTheme.Dark;
                var json = File.ReadAllText(path);
                var dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions);
                if (dto == null)
                    return AppUiTheme.Dark;
                return ThemeService.Parse(dto.UiTheme);
            }
            catch
            {
                return AppUiTheme.Dark;
            }
        }

        public static void Save(AppUiTheme theme)
        {
            try
            {
                var path = GetPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var dto = new Dto { UiTheme = theme.ToString() };
                File.WriteAllText(path, JsonSerializer.Serialize(dto, JsonOptions));
            }
            catch
            {
            }
        }
    }
}
