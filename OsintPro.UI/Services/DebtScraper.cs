using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Sentry;

namespace OsintPro.UI.Services
{
    public class DebtScraper
    {
        private const string ExtractRowsScript = @"() => {
            const rows = document.querySelectorAll('tr[ng-repeat-start]');
            const clean = t => (t || '').replace(/\s+/g, ' ').trim();
            return Array.from(rows).map(row => {
                const cells = row.querySelectorAll('td');
                if (cells.length < 5) return null;
                return {
                    debtor: clean(cells[0].innerText),
                    identifier: clean(cells[1].innerText),
                    creator: clean(cells[2].innerText),
                    category: clean(cells[4].innerText)
                };
            }).filter(Boolean);
        }";

        public static async Task<string> ParseDebtsDataAsync(
            string query,
            CancellationToken token,
            SearchMatchOptions matchOptions = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "✅ Немає даних для пошуку.";

            string cleanQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            bool isInn = SearchQueryMatcher.IsInn(cleanQuery);

            IBrowserContext context = null;
            IPage page = null;

            try
            {
                token.ThrowIfCancellationRequested();

                using var browserLease = await BrowserManager.AcquireBrowserLeaseAsync(token);
                context = await BrowserManager.CreateStealthContextAsync(browserLease.Browser, token);
                page = await context.NewPageAsync();

                await RetryHelper.ExecuteAsync(async ct =>
                {
                    await page.GotoAsync("https://erb.minjust.gov.ua/#/search-debtors",
                        new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30000 });
                    await page.WaitForSelectorAsync("form, #inputPersonSurname, #inputPersonCode",
                        new PageWaitForSelectorOptions { Timeout = 20000 });
                    return true;
                }, token, maxAttempts: 3, initialDelayMs: 1000);

                if (!await page.IsVisibleAsync("button:has-text('Шукати')"))
                    return "❌ Мін'юст (ЄРБ) тимчасово заблокував доступ (Cloudflare). Спробуйте пізніше.";

                if (isInn)
                {
                    await page.Locator("#inputPersonCode").First.FillAsync(cleanQuery);
                }
                else
                {
                    var parts = cleanQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                        await page.Locator("#inputPersonSurname").First.FillAsync(parts[0]);
                    if (parts.Length >= 2)
                        await page.Locator("#inputPersonName").First.FillAsync(parts[1]);
                    if (parts.Length >= 3)
                        await page.Locator("#inputPersonPatro").First.FillAsync(parts[2]);
                }

                var searchButton = page.Locator("button:has-text('Шукати')").First;
                bool foundRows = false;

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    await searchButton.ClickAsync(new LocatorClickOptions { Force = true });

                    try
                    {
                        await page.WaitForSelectorAsync(".cg-busy, .loader",
                            new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 8000 });
                    }
                    catch { }

                    try
                    {
                        await page.WaitForSelectorAsync("tr[ng-repeat-start], .alert, .no-results",
                            new PageWaitForSelectorOptions { Timeout = 8000 });
                        foundRows = await page.Locator("tr[ng-repeat-start]").CountAsync() > 0;
                        break;
                    }
                    catch (TimeoutException)
                    {
                        if (attempt == 2)
                            break;
                        await Task.Delay(1200, token);
                    }
                }

                if (!foundRows)
                    return "✅ В реєстрі боржників записів не знайдено.";

                var rawRows = await page.EvaluateAsync<DebtRow[]>(ExtractRowsScript);
                if (rawRows == null || rawRows.Length == 0)
                    return "✅ В реєстрі боржників записів не знайдено.";

                var filtered = rawRows
                    .Where(r => !string.IsNullOrWhiteSpace(r.Debtor) && !string.IsNullOrWhiteSpace(r.Category))
                    .Where(r => isInn
                        ? r.Identifier.Contains(cleanQuery, StringComparison.Ordinal)
                        : SearchQueryMatcher.MatchesPerson(r.Debtor, cleanQuery, matchOptions))
                    .GroupBy(r => $"{r.Debtor}|{r.Category}|{r.Creator}".ToLowerInvariant())
                    .Select(g => g.First())
                    .OrderByDescending(r => SearchQueryMatcher.GetMatchScore(r.Debtor, cleanQuery, matchOptions))
                    .Take(15)
                    .ToList();

                if (filtered.Count == 0)
                    return "✅ В реєстрі боржників записів не знайдено.";

                var report = new StringBuilder();
                foreach (var row in filtered)
                {
                    if (token.IsCancellationRequested)
                        return "🛑 Пошук скасовано.";

                    report.AppendLine($"❌ Боржник: {row.Debtor}");
                    report.AppendLine($"Ідентифікатор: {row.Identifier}");
                    report.AppendLine($"Категорія: {row.Category}");
                    report.AppendLine($"Видавець: {row.Creator}");
                    report.AppendLine();
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
                return $"❌ Помилка реєстру боржників: {ex.Message}";
            }
            finally
            {
                if (page != null)
                    await page.CloseAsync();
                if (context != null)
                    await context.CloseAsync();
            }
        }

        private sealed class DebtRow
        {
            public string Debtor { get; set; } = "";
            public string Identifier { get; set; } = "";
            public string Creator { get; set; } = "";
            public string Category { get; set; } = "";
        }
    }
}