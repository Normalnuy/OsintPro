using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public class SocialScraper
    {
        private static readonly Regex SocialLinkPattern = new(
            @"https?://[a-zA-Z0-9./\-_=&?]+(?:instagram\.com|facebook\.com|linkedin\.com|t\.me|tiktok\.com)[a-zA-Z0-9./\-_=&?]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, string> DirectUrls = new()
        {
            { "📸 Instagram", "https://www.instagram.com/{0}/" },
            { "✈️ Telegram", "https://t.me/{0}" },
            { "🐦 X (Twitter)", "https://twitter.com/{0}" },
            { "💻 GitHub", "https://github.com/{0}" },
            { "🎵 TikTok", "https://www.tiktok.com/@{0}" },
            { "👽 Reddit", "https://www.reddit.com/user/{0}" }
        };

        public static async Task<string> ParseSocialDataAsync(
            string query,
            string fullName,
            CancellationToken token,
            string dob = "",
            SearchSession session = null)
        {
            query = query?.Trim() ?? "";
            fullName = fullName?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(fullName))
                return "✅ Немає даних для пошуку.";

            var allResults = new List<string>();

            bool isNickname = IsNicknameQuery(query);
            if (isNickname && !string.IsNullOrWhiteSpace(query))
            {
                string cleanNick = query.TrimStart('@');
                var tasks = DirectUrls.Select(kvp => CheckDirectProfileAsync(kvp.Key, kvp.Value, cleanNick, token));
                var directResults = await Task.WhenAll(tasks);
                var validDirect = directResults.Where(r => !string.IsNullOrEmpty(r)).ToList();

                if (validDirect.Count > 0)
                    allResults.Add(string.Join("\n\n", validDirect).Trim());
            }

            string queryForDorks = !string.IsNullOrWhiteSpace(fullName) ? fullName : (!isNickname ? query : "");
            if (!string.IsNullOrWhiteSpace(queryForDorks))
            {
                string cascadeResult = await SearchExtremeCascadeAsync(queryForDorks, token, dob, session);
                if (!cascadeResult.Contains("Згадок у соцмережах не знайдено"))
                    allResults.Add(cascadeResult);
            }

            return allResults.Count > 0
                ? string.Join("\n\n", allResults)
                : "✅ Згадок у соцмережах не знайдено.";
        }

        private static async Task<string> SearchExtremeCascadeAsync(
            string query,
            CancellationToken token,
            string dob = "",
            SearchSession session = null)
        {
            string dobPart = DobHelper.IsValid(dob) ? $" \"{dob}\"" : "";
            string dorkQuery = $"\"{query}\"{dobPart} site:instagram.com OR site:facebook.com OR site:linkedin.com OR site:t.me OR site:tiktok.com";

            var hits = await DorkSearchService.SearchAsync(
                dorkQuery,
                SocialLinkPattern,
                link => !IsInvalidLink(link),
                new DorkSearchOptions
                {
                    MaxResults = 12,
                    MaxParallelEngines = 3,
                    PageTimeoutMs = 12000,
                    PostLoadDelayMs = 1200,
                    StopAfterFirstEngineWithHits = false
                },
                token,
                session);

            if (hits.Count == 0)
                return "✅ Згадок у соцмережах не знайдено.";

            var report = new StringBuilder();
            foreach (var hit in hits)
            {
                report.AppendLine($"📱 Платформа: {GetPlatformName(hit.LinkKey)}");
                report.AppendLine($"Профіль: {(string.IsNullOrEmpty(hit.Title) ? "Знайдено" : hit.Title)}");
                report.AppendLine($"Посилання: {hit.Link}\n");
            }

            return report.ToString().Trim();
        }

        private static bool IsInvalidLink(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                return true;

            string p = uri.AbsolutePath.ToLowerInvariant();
            return p == "/" || p == "/uk/" || p == "/ru/" || p.Contains("/login") || p.Contains("/search") || p.Length < 4;
        }

        private static string GetPlatformName(string key)
        {
            if (key.Contains("instagram")) return "📸 Instagram";
            if (key.Contains("facebook")) return "📘 Facebook";
            if (key.Contains("linkedin")) return "💼 LinkedIn";
            if (key.Contains("t.me")) return "✈️ Telegram";
            return "🎵 TikTok";
        }

        private static bool IsNicknameQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return false;

            string trimmed = query.TrimStart('@').Trim();
            if (trimmed.Contains(' '))
                return false;

            return Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_.-]{3,}$");
        }

        private static async Task<string> CheckDirectProfileAsync(string platform, string urlTemplate, string nickname, CancellationToken token)
        {
            string url = string.Format(urlTemplate, nickname);
            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
                using var headResponse = await AppHttp.Shared.SendAsync(headRequest, token);

                if (headResponse.StatusCode == System.Net.HttpStatusCode.NotFound ||
                    headResponse.StatusCode == System.Net.HttpStatusCode.Gone)
                    return null;

                if (platform is "📸 Instagram" or "✈️ Telegram")
                {
                    using var getRequest = new HttpRequestMessage(HttpMethod.Get, url);
                    using var response = await AppHttp.Shared.SendAsync(getRequest, HttpCompletionOption.ResponseHeadersRead, token);
                    if (!response.IsSuccessStatusCode)
                        return null;

                    string text = await response.Content.ReadAsStringAsync(token);
                    if (platform == "✈️ Telegram" && !text.Contains("tgme_page_title"))
                        return null;
                    if (platform == "📸 Instagram" && text.Contains("Login • Instagram"))
                        return null;
                }
                else if (!headResponse.IsSuccessStatusCode)
                {
                    return null;
                }

                return $"📱 Платформа: {platform}\nПрофіль: @{nickname}\nПосилання: {url}\nПосада/Збіг: Знайдено за прямим посиланням";
            }
            catch
            {
                return null;
            }
        }
    }
}