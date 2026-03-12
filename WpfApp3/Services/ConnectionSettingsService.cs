using System;
using System.IO;
using System.Text.Json;
using WpfApp3.Models;

namespace WpfApp3.Services
{
    public static class ConnectionSettingsService
    {
        private static readonly string FolderPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "E-Kalinga");

        private static readonly string FilePath =
            Path.Combine(FolderPath, "connection-settings.json");

        public static ConnectionSettings Load()
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                    Directory.CreateDirectory(FolderPath);

                if (!File.Exists(FilePath))
                {
                    var defaults = new ConnectionSettings();
                    Save(defaults);
                    return defaults;
                }

                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<ConnectionSettings>(json);

                return settings ?? new ConnectionSettings();
            }
            catch
            {
                return new ConnectionSettings();
            }
        }

        public static void Save(ConnectionSettings settings)
        {
            if (!Directory.Exists(FolderPath))
                Directory.CreateDirectory(FolderPath);

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }
    }
}