using System;
using System.IO;
using System.Text.Json;

namespace DriveCare.Services
{
    public sealed class UserSessionData
    {
        public Guid UserId { get; set; }
    }

    /// <summary>Сохранённая сессия клиента DriveCare (до выхода из профиля).</summary>
    public static class UserSessionStore
    {
        static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        static string SessionPath =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveCare",
                "session.json");

        public static void Save(Guid userId)
        {
            try
            {
                var path = SessionPath;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var data = new UserSessionData { UserId = userId };
                File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOptions));
            }
            catch
            {
            }
        }

        public static bool TryLoad(out Guid userId)
        {
            userId = Guid.Empty;
            try
            {
                var path = SessionPath;
                if (!File.Exists(path))
                    return false;
                var data = JsonSerializer.Deserialize<UserSessionData>(File.ReadAllText(path), JsonOptions);
                if (data == null || data.UserId == Guid.Empty)
                    return false;
                userId = data.UserId;
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
