using System;
using System.Windows;
using OsintPro.UI.Services;

namespace OsintPro.UI.Views
{
    public partial class SettingsWindow : Window
    {
        public bool SettingsSaved { get; private set; }

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = AppSettings.Current;
            EnableCacheCheck.IsChecked = s.EnableSearchCache;
            CacheTtlBox.Text = s.CacheTtlHours.ToString();
            SentryDsnBox.Text = s.SentryDsn ?? "";
            SentryRateBox.Text = s.SentryTracesSampleRate.ToString("0.##");
            UpdateCacheStats();
        }

        private void UpdateCacheStats()
        {
            var (bytes, files) = SearchResultCache.GetStats();
            double mb = bytes / 1024.0 / 1024.0;
            CacheStatsText.Text = $"Розмір кешу: {mb:0.##} МБ ({files} файлів)";
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            if (!AppDialogs.Confirm(this, "Кеш", "Очистити весь кеш пошуку?"))
                return;

            SearchResultCache.ClearAll();
            UpdateCacheStats();
            AppDialogs.Success(this, "Кеш", "Кеш очищено.");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(CacheTtlBox.Text.Trim(), out int ttl) || ttl < 1 || ttl > 168)
            {
                AppDialogs.Warning(this, "Помилка", "TTL кешу має бути від 1 до 168 годин.");
                return;
            }

            if (!double.TryParse(SentryRateBox.Text.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double rate) || rate < 0 || rate > 1)
            {
                AppDialogs.Warning(this, "Помилка", "Traces sample rate має бути від 0.0 до 1.0");
                return;
            }

            var settings = AppSettings.Current;
            settings.EnableSearchCache = EnableCacheCheck.IsChecked == true;
            settings.CacheTtlHours = ttl;
            settings.SentryDsn = SentryDsnBox.Text.Trim();
            settings.SentryTracesSampleRate = rate;
            settings.Save();
            AppSettings.Reload();
            SentryService.Reload();
            SettingsSaved = true;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}