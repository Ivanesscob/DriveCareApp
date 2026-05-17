using System;
using System.IO;
using System.Text.Json;

namespace DriveCarePro.Services
{
    /// <summary>Сохранённая сессия входа сотрудника Pro (перезапуск приложения).</summary>
    public sealed class ProSessionData
    {
        public Guid EmployeeId { get; set; }
    }

    public static class ProSessionStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        private static string SessionPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveCarePro",
                "session.json");

        public static void Save(Guid employeeId)
        {
            try
            {
                var path = SessionPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var data = new ProSessionData { EmployeeId = employeeId };
                File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
            }
            catch
            {
            }
        }

        public static bool TryLoad(out Guid employeeId)
        {
            employeeId = Guid.Empty;
            try
            {
                var path = SessionPath;
                if (!File.Exists(path))
                    return false;
                var data = JsonSerializer.Deserialize<ProSessionData>(File.ReadAllText(path), JsonOptions);
                if (data == null || data.EmployeeId == Guid.Empty)
                    return false;
                employeeId = data.EmployeeId;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(SessionPath))
                    File.Delete(SessionPath);
            }
            catch
            {
            }
        }
    }
}
