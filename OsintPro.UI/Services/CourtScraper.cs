using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using System.Text;
using Sentry;

namespace OsintPro.UI.Services
{
    public class CaptchaSession
    {
        public IBrowserContext Context { get; set; }
        public IPage Page { get; set; }
    }

    public class CourtScraper
    {
        private static readonly Dictionary<string, CaptchaSession> _captchaSessions = new();

        private const string ExtractCasesScript = @"() => {
            const rows = document.querySelectorAll('#bank tbody tr');
            const clean = t => (t || '').replace(/\s+/g, ' ').trim();
            return Array.from(rows).map(row => {
                const cells = row.querySelectorAll('td');
                if (cells.length < 9) return null;
                return {
                    court: clean(cells[0].innerText),
                    caseNumber: clean(cells[1].innerText),
                    procNumber: clean(cells[2].innerText),
                    dateIncoming: clean(cells[3].innerText),
                    judge: clean(cells[4].innerText),
                    parties: clean(cells[5].innerText),
                    subject: clean(cells[6].innerText),
                    statusDate: clean(cells[7].innerText),
                    statusName: clean(cells[8].innerText)
                };
            }).filter(r => r && r.caseNumber);
        }";

        public static async Task ClearAllSessionsAsync()
        {
            var keys = new List<string>(_captchaSessions.Keys);
            foreach (var key in keys)
                await ClearSessionAsync(key);
        }

        public static async Task ClearSessionAsync(string sessionId)
        {
            if (_captchaSessions.TryGetValue(sessionId, out var session))
            {
                try
                {
                    if (session.Context != null)
                        await session.Context.CloseAsync();
                }
                catch (Exception ex) { SentrySdk.CaptureException(ex); }
                finally { _captchaSessions.Remove(sessionId); }
            }
        }

        public static async Task<string> ParseCourtCasesAsync(
            string query,
            CancellationToken token,
            SearchMatchOptions matchOptions = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return "✅ Немає даних для пошуку.";

            string cleanQuery = string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            IBrowserContext context = null;

            try
            {
                token.ThrowIfCancellationRequested();

                using var browserLease = await BrowserManager.AcquireHeadlessLeaseAsync(token);
                context = await browserLease.Browser.NewContextAsync();
                var page = await context.NewPageAsync();

                await RetryHelper.ExecuteAsync(async ct =>
                {
                    await page.GotoAsync("https://court.gov.ua/fair/",
                        new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20000 });
                    var searchInput = page.Locator("#srch").First;
                    await searchInput.WaitForAsync(new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 15000
                    });
                    return true;
                }, token, maxAttempts: 3, initialDelayMs: 1000);

                var searchInput = page.Locator("#srch").First;
                await searchInput.FillAsync(cleanQuery);

                var searchButton = page.Locator("#search").First;
                bool isDisabled = await searchButton.EvaluateAsync<bool>("el => el.hasAttribute('disabled')");

                if (isDisabled)
                {
                    try
                    {
                        var recaptchaFrame = page.FrameLocator("iframe[title*='reCAPTCHA']").First;
                        var checkbox = recaptchaFrame.Locator(".recaptcha-checkbox-border").First;
                        await checkbox.WaitForAsync(new LocatorWaitForOptions
                        {
                            State = WaitForSelectorState.Visible,
                            Timeout = 5000
                        });
                        await checkbox.ClickAsync();
                    }
                    catch { }

                    try
                    {
                        await page.WaitForFunctionAsync(
                            "document.getElementById('search') && !document.getElementById('search').hasAttribute('disabled')",
                            null,
                            new PageWaitForFunctionOptions { Timeout = 6000 });
                        isDisabled = false;
                    }
                    catch (TimeoutException)
                    {
                        isDisabled = true;
                    }
                }

                if (isDisabled)
                {
                    string sessionId = Guid.NewGuid().ToString();
                    _captchaSessions[sessionId] = new CaptchaSession { Context = context, Page = page };
                    context = null;
                    return $"⚠️КАПЧА⚠️|{sessionId}";
                }

                await searchButton.ClickAsync();
                try
                {
                    await page.WaitForSelectorAsync("#bank_processing",
                        new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 15000 });
                    await page.WaitForSelectorAsync("#bank tbody tr, .dataTables_empty",
                        new PageWaitForSelectorOptions { Timeout = 8000 });
                }
                catch (TimeoutException) { }

                return await ExtractCasesDataAsync(page, cleanQuery, token, matchOptions);
            }
            catch (TaskCanceledException)
            {
                return "🛑 Пошук скасовано.";
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return $"❌ Помилка судів: {ex.Message}";
            }
            finally
            {
                if (context != null)
                    await context.CloseAsync();
            }
        }

        public static async Task<string> ResolveCourtCaptchaAsync(string sessionId, string token_recaptcha)
        {
            if (!_captchaSessions.TryGetValue(sessionId, out var session))
                return "❌ Сесія капчі застаріла.";

            var page = session.Page;

            try
            {
                await page.EvaluateAsync(@"(captchaToken) => {
                    let recaptchaResponse = document.getElementById('g-recaptcha-response');
                    if (recaptchaResponse) {
                        recaptchaResponse.innerHTML = captchaToken;
                        recaptchaResponse.value = captchaToken;
                    }
                    let searchBtn = document.getElementById('search');
                    if (searchBtn) searchBtn.removeAttribute('disabled');
                }", token_recaptcha);

                await page.EvaluateAsync("document.getElementById('search').click()");

                try
                {
                    await page.WaitForSelectorAsync("#bank_processing",
                        new PageWaitForSelectorOptions { State = WaitForSelectorState.Hidden, Timeout = 15000 });
                    await page.WaitForSelectorAsync("#bank tbody tr, .dataTables_empty",
                        new PageWaitForSelectorOptions { Timeout = 8000 });
                }
                catch (TimeoutException) { }

                string query = await page.Locator("#srch").First.InputValueAsync();
                return await ExtractCasesDataAsync(page, query, CancellationToken.None, SearchMatchOptions.Soft);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return $"❌ Помилка після проходження капчі: {ex.Message}";
            }
            finally
            {
                if (session.Context != null)
                    await session.Context.CloseAsync();
                _captchaSessions.Remove(sessionId);
            }
        }

        private static async Task<string> ExtractCasesDataAsync(
            IPage page,
            string query,
            CancellationToken token,
            SearchMatchOptions matchOptions = default)
        {
            try
            {
                var emptyTable = await page.QuerySelectorAsync(".dataTables_empty");
                if (emptyTable != null)
                    return "✅ Судових справ не знайдено.";

                var rawCases = await page.EvaluateAsync<CourtRow[]>(ExtractCasesScript);
                if (rawCases == null || rawCases.Length == 0)
                    return "✅ Судових справ не знайдено.";

                var filtered = rawCases
                    .Where(c => SearchQueryMatcher.MatchesPerson(c.Parties, query, matchOptions) ||
                                SearchQueryMatcher.MatchesPerson(c.Subject, query, matchOptions) ||
                                c.Parties.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => SearchQueryMatcher.GetMatchScore(c.Parties, query, matchOptions))
                    .Take(15)
                    .ToList();

                if (filtered.Count == 0 && !matchOptions.Strict && string.IsNullOrWhiteSpace(matchOptions.Dob))
                    filtered = rawCases.Take(15).ToList();

                var report = new StringBuilder();
                foreach (var row in filtered)
                {
                    if (token.IsCancellationRequested)
                        return "🛑 Пошук скасовано.";

                    report.AppendLine($"📂 Справа №{row.CaseNumber}");
                    report.AppendLine($"Суд: {row.Court}");
                    report.AppendLine($"Провадження: {row.ProcNumber}");
                    report.AppendLine($"Дата надходження: {row.DateIncoming}");
                    report.AppendLine($"Склад суду: {row.Judge}");
                    report.AppendLine($"Сторони: {row.Parties}");
                    report.AppendLine($"Предмет позову: {row.Subject}");
                    report.AppendLine($"Статус: {row.StatusName} ({row.StatusDate})\n");
                }

                string finalReport = report.ToString().Trim();
                return string.IsNullOrEmpty(finalReport) ? "✅ Судових справ не знайдено." : finalReport;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                return $"❌ Помилка читання таблиці справ: {ex.Message}";
            }
        }

        private sealed class CourtRow
        {
            public string Court { get; set; } = "";
            public string CaseNumber { get; set; } = "";
            public string ProcNumber { get; set; } = "";
            public string DateIncoming { get; set; } = "";
            public string Judge { get; set; } = "";
            public string Parties { get; set; } = "";
            public string Subject { get; set; } = "";
            public string StatusDate { get; set; } = "";
            public string StatusName { get; set; } = "";
        }
    }
}