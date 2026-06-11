using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public static class PhoneLookupScraper
    {
        private static readonly Regex PhoneLinkPattern = new(
            @"https?://[a-zA-Z0-9./\-_=&?]+(?:olx\.ua|prom\.ua|t\.me|vk\.com|facebook\.com|instagram\.com|linkedin\.com|auto\.ria\.com|tiktok\.com|twitter\.com|x\.com)[a-zA-Z0-9./\-_=&?]*",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, (string Country, string Flag)> CountryCodes = new()
        {
            { "380", ("Україна", "🇺🇦") },
            { "7", ("Росія / Казахстан", "🇷🇺") },
            { "48", ("Польща", "🇵🇱") },
            { "49", ("Німеччина", "🇩🇪") },
            { "1", ("США / Канада", "🇺🇸") },
            { "44", ("Велика Британія", "🇬🇧") },
            { "39", ("Італія", "🇮🇹") },
            { "33", ("Франція", "🇫🇷") },
            { "40", ("Румунія", "🇷🇴") },
            { "373", ("Молдова", "🇲🇩") },
            { "375", ("Білорусь", "🇧🇾") },
            { "370", ("Литва", "🇱🇹") },
            { "371", ("Латвія", "🇱🇻") },
            { "372", ("Естонія", "🇪🇪") },
            { "90", ("Туреччина", "🇹🇷") },
            { "971", ("ОАЕ", "🇦🇪") },
        };

        private static readonly Dictionary<string, string> UaOperators = new(StringComparer.Ordinal)
        {
            { "50", "Vodafone Ukraine" }, { "66", "Vodafone Ukraine" }, { "95", "Vodafone Ukraine" }, { "99", "Vodafone Ukraine" },
            { "67", "Kyivstar" }, { "68", "Kyivstar" }, { "96", "Kyivstar" }, { "97", "Kyivstar" }, { "98", "Kyivstar" },
            { "63", "lifecell" }, { "73", "lifecell" }, { "93", "lifecell" },
            { "91", "ТриМоб" }, { "92", "PEOPLEnet" }, { "94", "Intertelecom" },
            { "89", "Інтертелеком" }
        };

        public static bool IsPhoneNumber(string contact)
        {
            if (string.IsNullOrWhiteSpace(contact)) return false;
            string digits = Regex.Replace(contact, @"\D", "");
            return digits.Length is >= 10 and <= 15;
        }

        public static async Task<string> AnalyzeAsync(string phone, CancellationToken token, SearchSession session = null)
        {
            if (!IsPhoneNumber(phone))
                return "";

            string normalized = NormalizePhone(phone);
            string digits = OnlyDigits(normalized);
            var profile = BuildProfile(normalized, digits);
            var report = new StringBuilder();

            report.AppendLine("🌐 Платформа: Justin Phone OSINT");
            report.AppendLine($"Заголовок: {normalized}");
            report.AppendLine($"Знайдено: {profile.SummaryLine}");
            report.AppendLine($"Країна: {profile.CountryLine}");
            report.AppendLine($"Оператор: {profile.OperatorLine}");
            report.AppendLine($"Тип лінії: {profile.LineType}");
            if (!string.IsNullOrWhiteSpace(profile.RegionHint))
                report.AppendLine($"Регіон: {profile.RegionHint}");
            report.AppendLine($"Месенджери: {profile.MessengerLinks}");
            report.AppendLine($"Формати: {profile.Formats}");

            var socialHits = await SearchSocialMentionsAsync(normalized, digits, token, session);
            var marketHits = await SearchMarketMentionsAsync(normalized, digits, token, session);

            foreach (var hit in socialHits.Concat(marketHits).Take(10))
            {
                report.AppendLine();
                report.AppendLine($"🌐 Платформа: {hit.Platform}");
                report.AppendLine($"Заголовок: {hit.Title}");
                report.AppendLine($"Знайдено: {hit.Snippet}");
                report.AppendLine($"Посилання: {hit.Link}");
            }

            if (socialHits.Count == 0 && marketHits.Count == 0)
            {
                report.AppendLine();
                report.AppendLine("🌐 Платформа: OSINT-скан");
                report.AppendLine("Заголовок: Публічні згадки");
                report.AppendLine("Знайдено: У відкритих джерелах згадок не знайдено");
            }

            return report.ToString().Trim();
        }

        public static string NormalizePhone(string phone)
        {
            string digits = OnlyDigits(phone);
            if (digits.StartsWith("380") && digits.Length == 12)
                return "+" + digits;
            if (digits.StartsWith("0") && digits.Length == 10)
                return "+38" + digits;
            if (digits.Length == 9 && !digits.StartsWith("0"))
                return "+380" + digits;
            if (digits.Length >= 10)
                return "+" + digits.TrimStart('0');
            return phone?.Trim() ?? "";
        }

        private static PhoneProfile BuildProfile(string normalized, string digits)
        {
            var (country, flag, code) = DetectCountry(digits);
            string operatorName = DetectOperator(digits, code);
            string lineType = GuessLineType(digits, code);
            string local = digits.StartsWith("380") && digits.Length == 12 ? "0" + digits[3..] : normalized;

            return new PhoneProfile
            {
                SummaryLine = $"{flag} {country} · {operatorName} · {lineType}",
                CountryLine = $"{country} ({code}, {flag})",
                OperatorLine = operatorName,
                LineType = lineType,
                RegionHint = code == "+380" ? GuessUaRegion(digits) : "",
                MessengerLinks = BuildMessengerLinks(digits),
                Formats = $"{normalized}, {local}, {digits}"
            };
        }

        private static (string Country, string Flag, string Code) DetectCountry(string digits)
        {
            foreach (var kv in CountryCodes.OrderByDescending(k => k.Key.Length))
            {
                if (digits.StartsWith(kv.Key))
                    return (kv.Value.Country, kv.Value.Flag, "+" + kv.Key);
            }
            return ("Невідома", "🌍", "+" + digits[..Math.Min(3, digits.Length)]);
        }

        private static string DetectOperator(string digits, string countryCode)
        {
            if (countryCode == "+380" && digits.Length >= 5)
            {
                string national = digits[3..];
                if (national.Length >= 2)
                {
                    string prefix = national[..2];
                    if (UaOperators.TryGetValue(prefix, out string op))
                        return op;
                }
                return "Невідомий український оператор";
            }

            if (countryCode == "+7" && digits.Length >= 4)
            {
                string prefix = digits[1..4];
                return prefix switch
                {
                    "900" or "901" or "902" or "903" or "905" or "906" or "908" or "909" or "951" or "952" or "953" or "958" => "Tele2 / T-Mobile RU",
                    "910" or "911" or "912" or "913" or "914" or "915" or "916" or "917" or "918" or "919" => "MTS",
                    "920" or "921" or "922" or "923" or "924" or "925" or "926" or "927" or "928" or "929" => "Megafon",
                    "930" or "931" or "932" or "933" or "934" or "936" or "937" or "938" or "939" => "Beeline",
                    _ => "Мобільний оператор (RU/KZ)"
                };
            }

            return "Оператор не визначено (потрібен HLR/API)";
        }

        private static string GuessLineType(string digits, string countryCode)
        {
            if (countryCode == "+380")
            {
                if (digits.Length == 12) return "Мобільний";
                if (digits.Length is 11 or 10) return "Міський / короткий";
            }
            return digits.Length >= 11 ? "Мобільний" : "Стаціонарний / короткий";
        }

        private static string GuessUaRegion(string digits)
        {
            if (digits.Length < 5) return "";
            string prefix = digits[3..5];
            return prefix switch
            {
                "50" or "66" or "95" or "99" => "Ймовірно центральний регіон (Vodafone)",
                "67" or "68" or "96" or "97" or "98" => "Ймовірно Kyivstar coverage",
                "63" or "73" or "93" => "Ймовірно lifecell coverage",
                _ => "Україна"
            };
        }

        private static string BuildMessengerLinks(string digits)
        {
            string intl = digits.TrimStart('0');
            if (!intl.StartsWith("380") && intl.Length == 9)
                intl = "380" + intl;

            return $"WhatsApp: wa.me/{intl} · Telegram: пошук · Viber: +{intl}";
        }

        private static async Task<List<PhoneHit>> SearchSocialMentionsAsync(string normalized, string digits, CancellationToken token, SearchSession session)
        {
            string local = digits.StartsWith("380") && digits.Length == 12 ? "0" + digits[3..] : normalized;
            string query = $"\"{normalized}\" OR \"{local}\" OR \"{digits}\" (site:facebook.com OR site:instagram.com OR site:vk.com OR site:t.me OR site:twitter.com OR site:tiktok.com OR site:linkedin.com)";

            return await RunDorkSearchAsync(query, "📱 Соцмережі", token, session, 6);
        }

        private static async Task<List<PhoneHit>> SearchMarketMentionsAsync(string normalized, string digits, CancellationToken token, SearchSession session)
        {
            string local = digits.StartsWith("380") && digits.Length == 12 ? "0" + digits[3..] : normalized;
            string query = $"\"{normalized}\" OR \"{local}\" (site:olx.ua OR site:prom.ua OR site:auto.ria.com OR site:besplatka.ua)";

            return await RunDorkSearchAsync(query, "🛒 Бази оголошень", token, session, 5);
        }

        private static async Task<List<PhoneHit>> RunDorkSearchAsync(string query, string category, CancellationToken token, SearchSession session, int max)
        {
            var hits = await DorkSearchService.SearchAsync(
                query,
                PhoneLinkPattern,
                link => Uri.TryCreate(link, UriKind.Absolute, out _),
                new DorkSearchOptions
                {
                    MaxResults = max,
                    MaxParallelEngines = 2,
                    PageTimeoutMs = 12000,
                    PostLoadDelayMs = 1000,
                    StopAfterFirstEngineWithHits = false,
                    EngineTemplates = new[]
                    {
                        "https://html.duckduckgo.com/html/?q={0}",
                        "https://www.bing.com/search?q={0}"
                    }
                },
                token,
                session);

            return hits.Select(h => new PhoneHit
            {
                Platform = $"{category} · {ResolvePlatform(h.LinkKey)}",
                Title = Trim(h.Title, 70, "Згадка номера"),
                Snippet = Trim(h.Snippet, 120, "Деталі у посиланні"),
                Link = h.Link
            }).ToList();
        }

        private static string ResolvePlatform(string linkKey)
        {
            if (linkKey.Contains("olx.ua")) return "OLX";
            if (linkKey.Contains("prom.ua")) return "Prom.ua";
            if (linkKey.Contains("t.me")) return "Telegram";
            if (linkKey.Contains("vk.com")) return "VK";
            if (linkKey.Contains("facebook.com")) return "Facebook";
            if (linkKey.Contains("instagram.com")) return "Instagram";
            if (linkKey.Contains("linkedin.com")) return "LinkedIn";
            if (linkKey.Contains("auto.ria.com")) return "Auto.RIA";
            if (linkKey.Contains("tiktok.com")) return "TikTok";
            if (linkKey.Contains("twitter.com") || linkKey.Contains("x.com")) return "X";
            return "Web";
        }

        private static string OnlyDigits(string value) => Regex.Replace(value ?? "", @"\D", "");

        private static string Trim(string text, int max, string fallback)
        {
            text = Regex.Replace(text ?? "", @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(text)) return fallback;
            return text.Length > max ? text[..max] + "..." : text;
        }

        private sealed class PhoneProfile
        {
            public string SummaryLine { get; init; } = "";
            public string CountryLine { get; init; } = "";
            public string OperatorLine { get; init; } = "";
            public string LineType { get; init; } = "";
            public string RegionHint { get; init; } = "";
            public string MessengerLinks { get; init; } = "";
            public string Formats { get; init; } = "";
        }

        private sealed class PhoneHit
        {
            public string Platform { get; init; } = "";
            public string Title { get; init; } = "";
            public string Snippet { get; init; } = "";
            public string Link { get; init; } = "";
        }
    }
}