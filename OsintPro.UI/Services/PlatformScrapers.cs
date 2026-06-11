using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public static class PlatformScrapers
    {
        public static async Task<string> SearchTelegramAsync(string query, CancellationToken token, SearchSession session = null)
        {
            if (string.IsNullOrWhiteSpace(query)) return "";
            string clean = query.TrimStart('@');
            var direct = await CheckUrlAsync($"https://t.me/{clean}", "✈️ Telegram", token);
            string dork = await SearchPlatformDorkAsync($"\"{clean}\" site:t.me", "t.me", "✈️ Telegram", token, session);
            return JoinParts(direct, dork);
        }

        public static async Task<string> SearchVkAsync(string query, string fullName, CancellationToken token, SearchSession session = null)
        {
            string q = !string.IsNullOrWhiteSpace(query) ? query.TrimStart('@') : fullName?.Trim();
            if (string.IsNullOrWhiteSpace(q)) return "";

            var direct = await CheckUrlAsync($"https://vk.com/{q}", "🔵 VK", token);
            string dork = await SearchPlatformDorkAsync($"\"{q}\" site:vk.com", "vk.com", "🔵 VK", token, session);
            return JoinParts(direct, dork);
        }

        public static async Task<string> SearchLinkedInAsync(string query, string fullName, CancellationToken token, SearchSession session = null)
        {
            string q = !string.IsNullOrWhiteSpace(query) ? query.TrimStart('@') : fullName?.Trim();
            if (string.IsNullOrWhiteSpace(q)) return "";

            var direct = await CheckUrlAsync($"https://www.linkedin.com/in/{q}", "💼 LinkedIn", token);
            string dork = await SearchPlatformDorkAsync($"\"{q}\" site:linkedin.com", "linkedin.com", "💼 LinkedIn", token, session);
            return JoinParts(direct, dork);
        }

        private static async Task<string> CheckUrlAsync(string url, string platform, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                using var response = await AppHttp.Shared.SendAsync(
                    new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, url), token);
                if ((int)response.StatusCode is 200 or 301 or 302 or 303 or 307 or 308)
                {
                    return $"🌐 Платформа: {platform}\nЗаголовок: Пряме посилання\nЗнайдено: Профіль існує\nПосилання: {url}";
                }
            }
            catch (TaskCanceledException) { throw; }
            catch { }
            return "";
        }

        private static async Task<string> SearchPlatformDorkAsync(
            string dorkQuery, string domain, string platform, CancellationToken token, SearchSession session)
        {
            var pattern = new Regex(
                $@"https?://[a-zA-Z0-9./\-_=&?]+{Regex.Escape(domain)}[a-zA-Z0-9./\-_=&?]*",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            var hits = await DorkSearchService.SearchAsync(
                dorkQuery,
                pattern,
                link => link.Contains(domain, StringComparison.OrdinalIgnoreCase),
                new DorkSearchOptions
                {
                    MaxResults = 5,
                    MaxParallelEngines = 2,
                    PageTimeoutMs = 12000,
                    PostLoadDelayMs = 1000,
                    StopAfterFirstEngineWithHits = true,
                    EngineTemplates = new[]
                    {
                        "https://html.duckduckgo.com/html/?q={0}",
                        "https://www.bing.com/search?q={0}"
                    }
                },
                token,
                session);

            if (hits.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var hit in hits.Take(4))
            {
                string title = string.IsNullOrWhiteSpace(hit.Title) ? "Профіль / згадка" : hit.Title;
                if (title.Length > 70) title = title[..70] + "...";
                sb.AppendLine($"🌐 Платформа: {platform}");
                sb.AppendLine($"Заголовок: {title}");
                sb.AppendLine($"Знайдено: Dork-пошук");
                sb.AppendLine($"Посилання: {hit.Link}");
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }

        private static string JoinParts(params string[] parts) =>
            string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
    }
}