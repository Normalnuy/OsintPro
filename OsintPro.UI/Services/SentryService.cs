using System;

namespace OsintPro.UI.Services
{
    public static class SentryService
    {
        private static bool _initialized;

        public static void InitializeFromSettings()
        {
            var settings = AppSettings.Current;
            string dsn = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("JUSTIN_SENTRY_DSN"))
                ? Environment.GetEnvironmentVariable("JUSTIN_SENTRY_DSN")
                : settings.SentryDsn;

            if (string.IsNullOrWhiteSpace(dsn))
            {
                if (_initialized)
                {
                    try { Sentry.SentrySdk.Close(); } catch { }
                    _initialized = false;
                }
                return;
            }

            if (_initialized)
            {
                try { Sentry.SentrySdk.Close(); } catch { }
                _initialized = false;
            }

            Sentry.SentrySdk.Init(o =>
            {
                o.Dsn = dsn;
                o.TracesSampleRate = settings.SentryTracesSampleRate;
                o.AutoSessionTracking = true;
                o.SendDefaultPii = false;
            });
            _initialized = true;
        }

        public static void Reload() => InitializeFromSettings();
    }
}