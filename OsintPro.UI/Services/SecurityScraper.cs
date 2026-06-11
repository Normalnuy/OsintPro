using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Sentry;

namespace OsintPro.UI.Services
{
    public class SecurityScraper
    {
        private const string ExtractResultsScript = @"() => {
            const items = document.querySelectorAll('#search-results .list-group-item');
            return Array.from(items).map(item => {
                const nameEl = item.querySelector('strong.text-danger');
                const details = Array.from(item.querySelectorAll('div.col-xl-10 > div'))
                    .map(d => d.innerText.trim())
                    .filter(Boolean);
                return {
                    name: nameEl ? nameEl.innerText.trim() : 'Фігурант',
                    href: item.getAttribute('href') || '',
                    details: details.join(' | ')
                };
            });
        }";

        public static async Task<string> ParseSecurityDataAsync(
            string query,
            CancellationToken token,
            SearchMatchOptions matchOptions = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "❕ Немає даних для пошуку.";

            string cleanQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            IBrowserContext context = null;

            try
            {
                token.ThrowIfCancellationRequested();

                using var browserLease = await BrowserManager.AcquireBrowserLeaseAsync(token);
                context = await BrowserManager.CreateStealthContextAsync(browserLease.Browser, token);
                var page = await context.NewPageAsync();

                await page.GotoAsync("https://lite.myrotvorets.center/", new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = 20000
                });

                string encodedQuery = Uri.EscapeDataString(cleanQuery);
                await page.EvaluateAsync($@"() => {{
                    window.location.hash = '#search/{encodedQuery};;;;';
                    window.dispatchEvent(new HashChangeEvent('hashchange'));
                }}");

                try
                {
                    await page.WaitForFunctionAsync(
                        @"() => {
                            const results = document.querySelectorAll('#search-results .list-group-item');
                            const empty = document.body.innerText.includes('не знайдено');
                            return results.length > 0 || empty;
                        }",
                        null,
                        new PageWaitForFunctionOptions { Timeout = 12000 });
                }
                catch
                {
                    return "❕ Чисто. В базі Миротворець записів не знайдено.";
                }

                var rawResults = await page.EvaluateAsync<SecurityRow[]>(ExtractResultsScript);
                if (rawResults == null || rawResults.Length == 0)
                    return "❕ Чисто. В базі Миротворець записів не знайдено.";

                var filtered = rawResults
                    .Where(r => SearchQueryMatcher.MatchesPerson(r.Name, cleanQuery, matchOptions))
                    .OrderByDescending(r => SearchQueryMatcher.GetMatchScore(r.Name, cleanQuery, matchOptions))
                    .Take(10)
                    .ToList();

                if (filtered.Count == 0)
                    return "❕ Чисто. В базі Миротворець записів не знайдено.";

                var report = new StringBuilder();
                foreach (var row in filtered)
                {
                    if (token.IsCancellationRequested)
                        return "🛑 Пошук скасовано.";

                    string fullLink = row.Href.StartsWith("#", StringComparison.Ordinal)
                        ? "https://lite.myrotvorets.center/" + row.Href
                        : row.Href;

                    report.AppendLine("🛡️ Платформа: Миротворець (База)");
                    report.AppendLine("Статус: ⬛ Знайдено в базі Чистилище");
                    report.AppendLine($"Заголовок: {row.Name.Replace("\n", " / ")}");

                    if (!string.IsNullOrWhiteSpace(row.Details))
                        report.AppendLine($"Деталі: {row.Details}");

                    report.AppendLine($"Посилання: {fullLink}\n");
                }

                return report.ToString().Trim();
            }
            catch (TaskCanceledException)
            {
                return "🛑 Пошук скасовано.";
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return $"❌ Помилка скрапінгу Миротворця: {ex.Message}";
            }
            finally
            {
                if (context != null)
                    await context.CloseAsync();
            }
        }

        private sealed class SecurityRow
        {
            public string Name { get; set; } = "";
            public string Href { get; set; } = "";
            public string Details { get; set; } = "";
        }
    }
}