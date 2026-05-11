using System;
using System.IO;
using System.Text.Json;

namespace DriveCarePro.Services
{
    /// <summary>Настройки интерфейса сотрудника (файл на каждого пользователя Pro).</summary>
    public sealed class EmployeeAppSettings
    {
        public string UiTheme { get; set; } = "Dark";
    }

    public static class EmployeeSettingsStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public static string GetProfilePath(Guid employeeId)
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveCarePro",
                "profiles");
            return Path.Combine(root, $"{employeeId:N}.json");
        }

        public static EmployeeAppSettings Load(Guid employeeId)
        {
            try
            {
                var path = GetProfilePath(employeeId);
                if (!File.Exists(path))
                    return new EmployeeAppSettings();
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<EmployeeAppSettings>(json, JsonOptions)
                       ?? new EmployeeAppSettings();
            }
            catch
            {
                return new EmployeeAppSettings();
            }
        }

        public static void Save(Guid employeeId, EmployeeAppSettings settings)
        {
            if (settings == null)
                return;
            try
            {
                var path = GetProfilePath(employeeId);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
            }
            catch
            {
            }
        }
    }
}
