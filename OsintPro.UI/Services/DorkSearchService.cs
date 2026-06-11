using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace OsintPro.UI.Services
{
    public sealed class DorkSearchHit
    {
        public string Link { get; init; } = "";
        public string Title { get; init; } = "";
        public string Snippet { get; init; } = "";
        public string LinkKey { get; init; } = "";
    }

    public sealed class DorkSearchOptions
    {
        public int MaxResults { get; init; } = 10;
        public int MaxParallelEngines { get; init; } = 3;
        public int PageTimeoutMs { get; init; } = 12000;
        public int PostLoadDelayMs { get; init; } = 1200;
        public bool StopAfterFirstEngineWithHits { get; init; } = true;
        public IReadOnlyList<string> EngineTemplates { get; init; }
    }

    public static class DorkSearchService
    {
        private static readonly Regex DomainRegex = new(@"^https?://(www\.)?", RegexOptions.Compiled);

        private static readonly string[] FastEngineTemplates =
        {
            "https://www.bing.com/search?q={0}",
            "https://html.duckduckgo.com/html/?q={0}",
            "https://search.brave.com/search?q={0}",
            "https://search.yahoo.com/search?p={0}",
            "https://www.mojeek.com/search?q={0}",
            "https://swisscows.com/web?query={0}",
            "https://www.qwant.com/?q={0}",
            "https://www.google.com/search?q={0}"
        };

        private const string ExtractLinksScript = @"() => Array.from(document.querySelectorAll('a')).map(a => {
            let parentText = a.parentElement ? a.parentElement.innerText.replace(/\n/g, ' ') : '';
            return (a.href || '') + '|||' + (a.innerText.trim() || '') + '|||' + parentText;
        })";

        public static async Task<List<DorkSearchHit>> SearchAsync(
            string dorkQuery,
            Regex linkPattern,
            Func<string, bool> isValidLink,
            DorkSearchOptions options,
            CancellationToken cancellationToken,
            SearchSession session = null)
        {
            if (string.IsNullOrWhiteSpace(dorkQuery))
                return new List<DorkSearchHit>();

            options ??= new DorkSearchOptions();
            string encodedQuery = Uri.EscapeDataString(dorkQuery);
            var engineTemplates = options.EngineTemplates ?? FastEngineTemplates;
            var engineUrls = engineTemplates
                .Select(template => string.Format(template, encodedQuery))
                .ToList();

            var hits = new ConcurrentBag<DorkSearchHit>();
            var seenKeys = new ConcurrentDictionary<string, byte>();
            int hitCount = 0;
            using var engineGate = new SemaphoreSlim(options.MaxParallelEngines, options.MaxParallelEngines);
            using var earlyStopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = earlyStopCts.Token;

            IBrowserContext context = null;
            BrowserLease browserLease = null;
            bool ownsContext = session == null;
            try
            {
                token.ThrowIfCancellationRequested();
                if (session != null)
                {
                    context = session.Context;
                }
                else
                {
                    browserLease = await BrowserManager.AcquireBrowserLeaseAsync(token);
                    context = await BrowserManager.CreateStealthContextAsync(browserLease.Browser, token);
                }

                var engineTasks = engineUrls.Select(engineUrl => ProbeEngineAsync(
                    context,
                    engineUrl,
                    linkPattern,
                    isValidLink,
                    options,
                    hits,
                    seenKeys,
                    () => Interlocked.CompareExchange(ref hitCount, 0, 0),
                    () => Interlocked.Increment(ref hitCount),
                    engineGate,
                    earlyStopCts,
                    token)).ToList();

                await Task.WhenAll(engineTasks);
            }
            finally
            {
                if (ownsContext && context != null)
                    await context.CloseAsync();
                browserLease?.Dispose();
            }

            return hits.Take(options.MaxResults).ToList();
        }

        private static async Task ProbeEngineAsync(
            IBrowserContext context,
            string engineUrl,
            Regex linkPattern,
            Func<string, bool> isValidLink,
            DorkSearchOptions options,
            ConcurrentBag<DorkSearchHit> hits,
            ConcurrentDictionary<string, byte> seenKeys,
            Func<int> getHitCount,
            Func<int> incrementHitCount,
            SemaphoreSlim engineGate,
            CancellationTokenSource earlyStopCts,
            CancellationToken token)
        {
            await engineGate.WaitAsync(token);
            IPage page = null;
            try
            {
                if (token.IsCancellationRequested || getHitCount() >= options.MaxResults)
                    return;

                page = await context.NewPageAsync();

                try
                {
                    await RetryHelper.ExecuteAsync(async ct =>
                    {
                        await page.GotoAsync(engineUrl, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = options.PageTimeoutMs
                        });
                        return true;
                    }, token, maxAttempts: 2, initialDelayMs: 600);

                    if (options.PostLoadDelayMs > 0)
                        await Task.Delay(options.PostLoadDelayMs, token);

                    try
                    {
                        await page.ClickAsync("button.agree, button[name='agree'], button#L2AGLb",
                            new PageClickOptions { Timeout = 1200 });
                    }
                    catch { }

                    var rawData = await page.EvaluateAsync<string[]>(ExtractLinksScript);
                    int addedThisEngine = 0;

                    foreach (var data in rawData)
                    {
                        if (token.IsCancellationRequested || getHitCount() >= options.MaxResults)
                            break;

                        var parts = data.Split(new[] { "|||" }, StringSplitOptions.None);
                        if (parts.Length == 0)
                            continue;

                        string href = parts[0];
                        string title = parts.Length > 1 ? parts[1] : "";
                        string snippet = parts.Length > 2 ? parts[2] : "";
                        string unescaped = Uri.UnescapeDataString(href);
                        var match = linkPattern.Match(unescaped);
                        if (!match.Success)
                            continue;

                        string cleanLink = match.Value;
                        if (!isValidLink(cleanLink))
                            continue;

                        string linkKey = NormalizeLinkKey(cleanLink);
                        if (!seenKeys.TryAdd(linkKey, 0))
                            continue;

                        hits.Add(new DorkSearchHit
                        {
                            Link = cleanLink,
                            Title = title,
                            Snippet = snippet,
                            LinkKey = linkKey
                        });

                        incrementHitCount();
                        addedThisEngine++;
                        if (getHitCount() >= options.MaxResults)
                            break;
                    }

                    if (options.StopAfterFirstEngineWithHits && addedThisEngine > 0)
                        earlyStopCts.Cancel();
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch { }
            }
            finally
            {
                if (page != null)
                    await page.CloseAsync();
                engineGate.Release();
            }
        }

        public static string NormalizeLinkKey(string link)
        {
            return DomainRegex.Replace(link, "").Split('?')[0].Trim('/').ToLowerInvariant();
        }
    }
}