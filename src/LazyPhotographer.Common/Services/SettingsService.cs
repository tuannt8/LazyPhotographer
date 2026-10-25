using System;
using System.IO;
using System.Text.Json;
using LazyPhotographer.Common.Models;

namespace LazyPhotographer.Common.Services
{
    public static class SettingsService
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LazyPhotographer");

        private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

        private static AppSettings? _current;
        public static AppSettings Current => _current ??= Load();

        public static event Action<AppSettings>? SettingsChanged;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        ValidateSettings(settings);
                        _current = settings;
                        return settings;
                    }
                }
            }
            catch
            {
                // Fall back to default
            }

            _current = new AppSettings();
            return _current;
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                ValidateSettings(settings);
                _current = settings;

                if (!Directory.Exists(SettingsDirectory))
                {
                    Directory.CreateDirectory(SettingsDirectory);
                }

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);

                SettingsChanged?.Invoke(settings);
            }
            catch
            {
                // Best effort save
            }
        }

        private static void ValidateSettings(AppSettings s)
        {
            s.ThumbnailCacheCapacity = Math.Clamp(s.ThumbnailCacheCapacity, 20, 1000);
            s.HighResCacheCapacity = Math.Clamp(s.HighResCacheCapacity, 5, 100);
            s.HighResMaxDimension = Math.Clamp(s.HighResMaxDimension, 1280, 4096);
        }
    }
}
