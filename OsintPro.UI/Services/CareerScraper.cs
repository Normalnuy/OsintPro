using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public class CareerScraper
    {
        private static readonly Regex CareerLinkPattern = new(
            @"https?://[a-zA-Z0-9./\-_=&?]+(?:work\.ua|robota\.ua|dou\.ua|djinni\.co|grc\.ua)[a-zA-Z0-9./\-_=&?]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static async Task<string> SearchResumesAsync(string name, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "";

            string dorkQuery = $"\"{name}\" site:work.ua/resumes OR site:robota.ua/candidates OR site:dou.ua/users OR site:djinni.co/q/ OR site:grc.ua";

            var hits = await DorkSearchService.SearchAsync(
                dorkQuery,
                CareerLinkPattern,
                link => link.Length >= 25,
                new DorkSearchOptions
                {
                    MaxResults = 8,
                    MaxParallelEngines = 3,
                    PageTimeoutMs = 12000,
                    PostLoadDelayMs = 1200,
                    StopAfterFirstEngineWithHits = true,
                    EngineTemplates = new[]
                    {
                        "https://search.yahoo.com/search?p={0}",
                        "https://www.bing.com/search?q={0}",
                        "https://search.brave.com/search?q={0}",
                        "https://www.mojeek.com/search?q={0}"
                    }
                },
                token);

            if (hits.Count == 0)
                return "";

            var report = new StringBuilder();
            foreach (var hit in hits)
            {
                report.AppendLine($"💼 Платформа: {GetPlatform(hit.LinkKey)}");
                report.AppendLine($"Посилання: {hit.Link}\n");
            }

            return report.ToString().Trim();
        }

        private static string GetPlatform(string key)
        {
            if (key.Contains("work.ua")) return "Work.ua";
            if (key.Contains("robota.ua")) return "Robota.ua";
            if (key.Contains("djinni")) return "Djinni";
            return "Кар'єрний портал";
        }
    }
}