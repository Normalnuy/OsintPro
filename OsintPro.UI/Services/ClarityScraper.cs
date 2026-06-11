using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Sentry;

namespace OsintPro.UI.Services
{
    public class ClarityScraper
    {
        private const string ProfileExtractScript = @"() => {
            let res = [];
            let realTitle = '';

            let flexRows = document.querySelectorAll('.flex-wrap.p-5');
            flexRows.forEach(row => {
                if (row.closest('#fop-risks-widget') || row.closest('.risks-widget') || row.innerText.includes('Інформація з ЄДР:')) return;

                let labelEl = row.querySelector('.flex-25, .flex-sm-100:first-child');
                let valEl = row.querySelector('.flex-75');

                let label = labelEl ? labelEl.innerText.trim() : '';
                let val = valEl ? valEl.innerText.trim() : '';

                if (val) {
                    val = val.replace(/Увійдіть або зареєструйтесь, щоб побачити повні дані/g, '').trim();
                    val = val.replace(/Придбайте повний доступ/g, '').trim();
                    val = val.replace(/\*{2,}/g, '').trim();
                    val = val.replace(/\n+/g, ' ').trim();
                    val = val.replace(/,\s*,/g, ',').trim();
                }

                if (label === 'Ім\'я' || label === 'Повне найменування' || label === 'ПІБ') {
                    if (!realTitle) realTitle = val;
                }

                if (label && val) {
                    res.push(label + ': ' + val);
                }
            });

            if (res.length === 0) {
                let tableRows = document.querySelectorAll('table tbody tr');
                tableRows.forEach(row => {
                    let cells = row.querySelectorAll('td, th');
                    if(cells.length >= 2) {
                        let c1 = cells[0].innerText.trim().replace(/\n/g, ' ');
                        let c2 = cells[1].innerText.trim().replace(/\n/g, ' ');

                        if (c2 && !c2.includes('Увійдіть або зареєструйтесь')) {
                            res.push(c1 + ': ' + c2);
                            if (c1 === 'Ім\'я' || c1 === 'Повне найменування' || c1 === 'ПІБ') {
                                if (!realTitle) realTitle = c2;
                            }
                        }
                    }
                });
            }

            if (!realTitle) {
                let h1 = document.querySelector('h1');
                if (h1) {
                    let h1Text = h1.innerText.trim();
                    if (!h1Text.includes('Відкрити інформацію') && !h1Text.includes('Інформація з ЄДР')) {
                        realTitle = h1Text;
                    }
                }
            }

            return (realTitle || 'Невідомо') + '|||' + res.join('\n');
        }";

        public static async Task<string> SearchAllAsync(string query, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "✅ Немає даних для пошуку.";

            string cleanQuery = query.Trim();
            IBrowserContext context = null;

            try
            {
                token.ThrowIfCancellationRequested();
                using var browserLease = await BrowserManager.AcquireBrowserLeaseAsync(token);
                context = await BrowserManager.CreateStealthContextAsync(browserLease.Browser, token);

                var edrPage = await context.NewPageAsync();
                var personsPage = await context.NewPageAsync();

                var edrLinksTask = CollectEdrLinksAsync(edrPage, cleanQuery, token);
                var personLinksTask = CollectPersonLinksAsync(personsPage, cleanQuery, token);
                await Task.WhenAll(edrLinksTask, personLinksTask);

                var linksToVisit = edrLinksTask.Result
                    .Concat(personLinksTask.Result)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(6)
                    .ToList();

                await edrPage.CloseAsync();
                await personsPage.CloseAsync();

                if (linksToVisit.Count == 0)
                    return "✅ У Clarity Project записів не знайдено.";

                var detailPage = await context.NewPageAsync();
                var results = new List<string>();
                foreach (var link in linksToVisit)
                {
                    if (token.IsCancellationRequested)
                        break;

                    string block = await ParseProfileAsync(detailPage, link, cleanQuery, token);
                    if (!string.IsNullOrWhiteSpace(block))
                        results.Add(block);
                }

                return results.Count > 0
                    ? string.Join("\n@@BLOCK_SEPARATOR@@\n", results)
                    : "✅ У Clarity Project записів не знайдено.";
            }
            catch (TaskCanceledException)
            {
                return "🛑 Пошук скасовано.";
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return $"❌ Помилка Clarity: {ex.Message}";
            }
            finally
            {
                if (context != null)
                    await context.CloseAsync();
            }
        }

        private static async Task<List<string>> CollectEdrLinksAsync(IPage page, string cleanQuery, CancellationToken token)
        {
            var links = new List<string>();
            string edrUrl = $"https://clarity-project.info/edrs?query={Uri.EscapeDataString(cleanQuery)}";

            await page.GotoAsync(edrUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            await WaitForCloudflareAsync(page, token);
            string currentUrl = page.Url;

            if (currentUrl.Contains("/fop/") || currentUrl.Contains("/edr/"))
            {
                links.Add(currentUrl);
                return links;
            }

            try
            {
                await page.WaitForSelectorAsync(".item.card a, table.table tbody tr a",
                    new PageWaitForSelectorOptions { Timeout = 8000 });
                var linkElements = await page.QuerySelectorAllAsync(".item.card a, table.table tbody tr a");

                foreach (var el in linkElements)
                {
                    string href = await el.GetAttributeAsync("href");
                    if (!string.IsNullOrEmpty(href) && (href.Contains("/fop/") || href.Contains("/edr/")))
                    {
                        links.Add("https://clarity-project.info" + href);
                        if (links.Count >= 3)
                            break;
                    }
                }
            }
            catch { }

            return links;
        }

        private static async Task<List<string>> CollectPersonLinksAsync(IPage page, string cleanQuery, CancellationToken token)
        {
            var links = new List<string>();
            string personUrl = $"https://clarity-project.info/persons?search={Uri.EscapeDataString(cleanQuery)}";

            await page.GotoAsync(personUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000
            });

            await WaitForCloudflareAsync(page, token);
            string currentUrl = page.Url;

            if (currentUrl.Contains("/person/"))
            {
                links.Add(currentUrl);
                return links;
            }

            try
            {
                await page.WaitForSelectorAsync(".entity-list-item a, table.table tbody tr a",
                    new PageWaitForSelectorOptions { Timeout = 8000 });
                var linkElements = await page.QuerySelectorAllAsync(".entity-list-item a, table.table tbody tr a");

                foreach (var el in linkElements)
                {
                    string href = await el.GetAttributeAsync("href");
                    if (!string.IsNullOrEmpty(href) && href.Contains("/person/"))
                    {
                        links.Add("https://clarity-project.info" + href);
                        if (links.Count >= 3)
                            break;
                    }
                }
            }
            catch { }

            return links;
        }

        private static async Task WaitForCloudflareAsync(IPage page, CancellationToken token)
        {
            try
            {
                await page.WaitForFunctionAsync(
                    "() => document.title !== 'Just a moment...'",
                    null,
                    new PageWaitForFunctionOptions { Timeout = 8000 });
            }
            catch
            {
                await Task.Delay(1500, token);
            }
        }

        private static async Task<string> ParseProfileAsync(IPage page, string link, string cleanQuery, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return null;

            await page.GotoAsync(link, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 20000
            });

            try
            {
                await page.WaitForSelectorAsync(".flex-wrap.p-5, table tbody tr, h1",
                    new PageWaitForSelectorOptions { Timeout = 5000 });
            }
            catch { }

            string sub1 = "Бізнес";
            string sub2 = "-";

            if (link.Contains("/fop/")) { sub1 = "ФОП"; sub2 = "Власник"; }
            else if (link.Contains("/edr/")) { sub1 = "Юридична особа"; sub2 = "Компанія"; }
            else if (link.Contains("/person/")) { sub1 = "Фізична особа"; sub2 = "Зв'язки з бізнесом"; }

            string rawData = await page.EvaluateAsync<string>(ProfileExtractScript);
            string[] parts = rawData.Split(new[] { "|||" }, StringSplitOptions.None);
            string extractedTitle = parts[0].Trim();
            string details = parts.Length > 1 ? parts[1].Trim() : "Деталі відсутні";

            if (extractedTitle == "Невідомо" || string.IsNullOrEmpty(extractedTitle))
                return null;

            if (!SearchQueryMatcher.MatchesPerson(extractedTitle, cleanQuery) &&
                SearchQueryMatcher.GetMatchScore(extractedTitle, cleanQuery) < 60)
                return null;

            bool exactMatch = SearchQueryMatcher.GetMatchScore(extractedTitle, cleanQuery) >= 100;
            return $"TITLE::{extractedTitle}\nSUB1::{sub1}\nSUB2::{sub2}\nEXACT::{(exactMatch ? "YES" : "NO")}\nDETAILS::\n{details}";
        }
    }
}