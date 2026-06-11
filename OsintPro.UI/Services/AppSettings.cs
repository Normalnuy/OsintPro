using System;
using System.IO;
using System.Text.Json;

namespace OsintPro.UI.Services
{
    public sealed class AppSettings
    {
        public bool EnableSearchCache { get; set; } = true;
        public int CacheTtlHours { get; set; } = 2;
        public string SentryDsn { get; set; } = "";
        public double SentryTracesSampleRate { get; set; } = 0.1;

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JustinOSINT",
            "settings.json");

        private static AppSettings _current;

        public static AppSettings Current => _current ??= Load();

        public static void Reload() => _current = Load();

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var defaults = new AppSettings();
                    defaults.Save();
                    return defaults;
                }

                string json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                if (settings.CacheTtlHours < 1) settings.CacheTtlHours = 1;
                if (settings.CacheTtlHours > 168) settings.CacheTtlHours = 168;
                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}