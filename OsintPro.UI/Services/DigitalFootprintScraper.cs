using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public class DigitalFootprintScraper
    {
        private static readonly Regex MarketplaceLinkPattern = new(
            @"https?://[a-zA-Z0-9./\-_=&?]+(?:olx\.ua|prom\.ua|auto\.ria\.com|rozetka\.com\.ua|besplatka\.ua|izi\.ua)[a-zA-Z0-9./\-_=&?]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static async Task<string> ParseFootprintAsync(
            string contact,
            string name,
            CancellationToken token,
            string dob = "",
            SearchSession session = null)
        {
            if (string.IsNullOrWhiteSpace(contact) && string.IsNullOrWhiteSpace(name))
                return "✅ Немає даних для пошуку.";

            try
            {
                string queryParts = "";
                if (!string.IsNullOrWhiteSpace(contact))
                    queryParts += $"\"{contact}\" ";
                if (!string.IsNullOrWhiteSpace(name))
                    queryParts += $"OR \"{name}\" ";
                if (DobHelper.IsValid(dob))
                    queryParts += $" \"{dob}\" ";

                string dorkQuery = $"{queryParts.Trim()} site:olx.ua OR site:prom.ua OR site:auto.ria.com OR site:rozetka.com.ua OR site:besplatka.ua OR site:izi.ua";
                return await SearchUniversalDorksAsync(dorkQuery, token, session);
            }
            catch (TaskCanceledException)
            {
                return "🛑 Пошук скасовано.";
            }
            catch (Exception ex)
            {
                Sentry.SentrySdk.CaptureException(ex);
                return $"❌ Помилка: {ex.Message}";
            }
        }

        private static async Task<string> SearchUniversalDorksAsync(
            string dorkQuery,
            CancellationToken token,
            SearchSession session = null)
        {
            var hits = await DorkSearchService.SearchAsync(
                dorkQuery,
                MarketplaceLinkPattern,
                IsValidMarketplaceLink,
                new DorkSearchOptions
                {
                    MaxResults = 10,
                    MaxParallelEngines = 3,
                    PageTimeoutMs = 15000,
                    PostLoadDelayMs = 1200,
                    StopAfterFirstEngineWithHits = true,
                    EngineTemplates = new[]
                    {
                        "https://search.yahoo.com/search?p={0}",
                        "https://html.duckduckgo.com/html/?q={0}",
                        "https://www.bing.com/search?q={0}",
                        "https://search.brave.com/search?q={0}",
                        "https://www.google.com/search?q={0}"
                    }
                },
                token,
                session);

            if (hits.Count == 0)
                return "✅ Слідів на маркетплейсах не знайдено.";

            var report = new StringBuilder();
            foreach (var hit in hits)
            {
                string platform = ResolvePlatform(hit.LinkKey);
                if (platform == null)
                    continue;

                string title = string.IsNullOrEmpty(hit.Title) ? "Оголошення / Профіль" : hit.Title;
                if (title.Length > 80)
                    title = title.Substring(0, 80) + "...";

                string snippet = Regex.Replace(hit.Snippet ?? "", @"\s+", " ").Trim();
                if (snippet.Length > 150)
                    snippet = snippet.Substring(0, 150) + "...";
                if (snippet == title)
                    snippet = "Деталі у посиланні";

                report.AppendLine($"🌐 Платформа: {platform}");
                report.AppendLine($"Заголовок: {title}");
                report.AppendLine($"Знайдено: {snippet}");
                report.AppendLine($"Посилання: {hit.Link}\n");
            }

            string finalReport = report.ToString().Trim();
            return string.IsNullOrEmpty(finalReport)
                ? "✅ Слідів на маркетплейсах не знайдено."
                : finalReport;
        }

        private static bool IsValidMarketplaceLink(string cleanLink)
        {
            if (!Uri.TryCreate(cleanLink, UriKind.Absolute, out Uri parsedUri))
                return false;

            string path = parsedUri.AbsolutePath.ToLowerInvariant();
            if (path == "/" || path == "/uk/" || path == "/ru/" || path == "/ua/")
                return false;
            if (path.StartsWith("/list/") || path.Contains("/search"))
                return false;

            return path.Length > 5;
        }

        private static string ResolvePlatform(string linkKey)
        {
            if (linkKey.Contains("olx.ua")) return "🛒 OLX";
            if (linkKey.Contains("prom.ua")) return "🛍️ Prom.ua";
            if (linkKey.Contains("auto.ria.com")) return "🚗 Auto.RIA";
            if (linkKey.Contains("rozetka.com.ua")) return "📦 Rozetka";
            if (linkKey.Contains("besplatka.ua")) return "🏷️ Besplatka";
            if (linkKey.Contains("izi.ua")) return "🧩 IZI.ua";
            return null;
        }
    }
}