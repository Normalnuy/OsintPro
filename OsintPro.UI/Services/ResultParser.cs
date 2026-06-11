using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public static class ResultParser
    {
        private const string ColorLoading = "#FFD700";
        private const string ColorSuccess = "#32CD32";
        private const string ColorError = "#FF6347";
        private const string ColorInfo = "#007ACC";

        public static List<GenericRecordDisplay> ParseBusiness(string rawText, bool onlyExact = false)
        {
            var records = new List<GenericRecordDisplay>();
            if (string.IsNullOrEmpty(rawText)) return records;

            if (rawText.Contains("✅") && !rawText.Contains("TITLE::"))
            {
                records.Add(MakeGeneric("❕ Не знайдено", "Записів немає", "", ColorSuccess, rawText, true));
                return records;
            }
            if (rawText.Contains("❌")) return SingleGeneric("Помилка", "Збій Clarity", "", ColorError, rawText);
            if (rawText.Contains("скасовано")) return SingleGeneric("🛑 Скасовано", "Зупинено", "", ColorError, rawText);
            if (rawText.Contains("❕")) return SingleGeneric("Інформація", rawText, "", ColorInfo, rawText);

            foreach (var block in rawText.Split(new[] { "@@BLOCK_SEPARATOR@@" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string cleanBlock = block.Trim();
                if (string.IsNullOrWhiteSpace(cleanBlock)) continue;

                var titleMatch = Regex.Match(cleanBlock, @"TITLE::(.*?)(?=\nSUB1::|$)", RegexOptions.Singleline);
                var sub1Match = Regex.Match(cleanBlock, @"SUB1::(.*?)(?=\nSUB2::|$)", RegexOptions.Singleline);
                var sub2Match = Regex.Match(cleanBlock, @"SUB2::(.*?)(?=\nEXACT::|$)", RegexOptions.Singleline);
                var exactMatch = Regex.Match(cleanBlock, @"EXACT::(.*?)(?=\nDETAILS::|$)", RegexOptions.Singleline);
                var detailsMatch = Regex.Match(cleanBlock, @"DETAILS::\n(.*)", RegexOptions.Singleline);

                string title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "Невідомо";
                string sub1 = sub1Match.Success ? sub1Match.Groups[1].Value.Trim() : "Бізнес";
                string sub2 = sub2Match.Success ? sub2Match.Groups[1].Value.Trim() : "";
                bool isExact = exactMatch.Success && exactMatch.Groups[1].Value.Trim() == "YES";
                string details = detailsMatch.Success ? detailsMatch.Groups[1].Value.Trim() : cleanBlock;

                if (string.IsNullOrEmpty(title) || title.Contains("Невідомо")) continue;
                if (onlyExact && !isExact) continue;

                string color = isExact ? ColorSuccess : ColorInfo;
                if (isExact && !title.StartsWith("✅")) title = "✅ " + title;
                if (title.Length > 60) title = title.Substring(0, 60) + "...";

                records.Add(MakeGeneric(title, sub1, sub2, color, details, isExact, title + sub1));
            }

            return Dedup(records);
        }

        public static List<GenericRecordDisplay> ParseSecurity(string rawText, bool onlyExact = false)
        {
            var records = new List<GenericRecordDisplay>();
            if (string.IsNullOrEmpty(rawText)) return records;

            if (!rawText.Contains("Платформа:") && !rawText.Contains("Статус:"))
            {
                if (rawText.Contains("❌")) return SingleGeneric("Помилка", "Збій мережі", "", ColorError, rawText);
                if (rawText.Contains("скасовано")) return SingleGeneric("🛑 Скасовано", "Зупинено", "", ColorError, rawText);
                return SingleGeneric("❕ Чисто", "В базах розшуку чисто", "", ColorSuccess, "В базах розшуку та санкцій не знайдено.");
            }

            foreach (var block in rawText.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = block.Split('\n');
                if (lines.Length == 0) continue;
                string title = lines.FirstOrDefault(l => l.Contains("Платформа:"))?.Replace("🛡️ Платформа:", "").Trim() ?? "Реєстр";
                string subtitle1 = lines.FirstOrDefault(l => l.Contains("Статус:"))?.Replace("Статус:", "").Trim() ?? "Увага";
                string subtitle2 = lines.FirstOrDefault(l => l.Contains("Заголовок:"))?.Replace("Заголовок:", "").Trim() ?? "";
                records.Add(MakeGeneric(title, subtitle1, subtitle2, ColorSuccess, block.Trim(), true, title + subtitle1));
            }

            return onlyExact ? records : Dedup(records);
        }

        public static List<GenericRecordDisplay> ParseFootprint(string rawText, bool onlyExact = false)
        {
            var records = new List<GenericRecordDisplay>();
            if (string.IsNullOrEmpty(rawText)) return records;

            if (!rawText.Contains("Платформа:") && !rawText.Contains("Знайдено:"))
            {
                if (rawText.Contains("❌")) return SingleGeneric("Помилка", "Збій мережі", "", ColorError, rawText);
                if (rawText.Contains("скасовано")) return SingleGeneric("🛑 Скасовано", "Зупинено", "", ColorError, rawText);
                return SingleGeneric("❕ Не знайдено", "Слідів не виявлено", "", ColorSuccess, "В базах оголошень нічого не знайдено.");
            }

            foreach (var block in rawText.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = block.Split('\n');
                if (lines.Length == 0) continue;
                string title = lines.FirstOrDefault(l => l.Contains("Платформа:"))?.Replace("🌐 Платформа:", "").Trim() ?? "Маркетплейс";
                string subtitle1 = lines.FirstOrDefault(l => l.Contains("Заголовок:"))?.Replace("Заголовок:", "").Trim() ?? "Оголошення";
                string subtitle2 = lines.FirstOrDefault(l => l.Contains("Знайдено:"))?.Replace("Знайдено:", "").Trim() ?? "";
                string link = lines.FirstOrDefault(l => l.Contains("Посилання:"))?.Replace("Посилання:", "").Trim() ?? subtitle2;
                records.Add(MakeGeneric(title, subtitle1, subtitle2, ColorSuccess, block.Trim(), true, link));
            }

            return onlyExact ? records.Where(r => r.IsExactMatch).ToList() : Dedup(records);
        }

        public static List<CourtCaseDisplay> ParseCases(string rawText, bool onlyExact = false)
        {
            var cases = new List<CourtCaseDisplay>();
            if (string.IsNullOrEmpty(rawText)) return cases;

            if (rawText.Contains("⚠️КАПЧА⚠️"))
            {
                cases.Add(new CourtCaseDisplay
                {
                    CaseNumber = "⚠️ Потрібна перевірка",
                    CourtName = "Натисніть на картку",
                    Status = "Очікує",
                    CardColor = ColorLoading,
                    FullText = "CAPTCHA_SESSION|" + rawText.Split('|').Last().Trim()
                });
                return cases;
            }

            if (!rawText.Contains("Справа №") && !rawText.Contains("📂"))
            {
                if (rawText.Contains("❌")) cases.Add(MakeCase("Помилка", "Збій системи", "", ColorError, rawText));
                else if (rawText.Contains("скасовано")) cases.Add(MakeCase("🛑 Скасовано", "Зупинено", "", ColorError, rawText));
                else cases.Add(MakeCase("❕ Чисто", "Даних не знайдено", "Справ немає", ColorSuccess, "Записів у реєстрі не знайдено."));
                return cases;
            }

            foreach (var block in rawText.Split(new[] { "📂" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(block, @"Справа №([^\n\*]+)");
                if (!match.Success) continue;
                string[] lines = block.Split('\n');
                string caseNumber = "Справа №" + match.Groups[1].Value.Trim();
                cases.Add(new CourtCaseDisplay
                {
                    CaseNumber = caseNumber,
                    CourtName = lines.FirstOrDefault(l => l.Contains("Суд:"))?.Split(':')[1].Trim() ?? "Не вказано",
                    Status = lines.FirstOrDefault(l => l.Contains("Статус:"))?.Split(':')[1].Trim() ?? "В процесі",
                    CardColor = ColorInfo,
                    FullText = block.Replace("**", "").Replace("*", "").Trim(),
                    IsExactMatch = true
                });
            }

            return cases;
        }

        public static List<GenericRecordDisplay> ParseDebts(string rawText, bool onlyExact = false)
        {
            var records = new List<GenericRecordDisplay>();
            if (string.IsNullOrEmpty(rawText)) return records;

            if (!rawText.Contains("Боржник:") && !rawText.Contains("Категорія:"))
            {
                if (rawText.Contains("❌")) return SingleGeneric("Помилка", "Збій системи", "", ColorError, rawText);
                if (rawText.Contains("скасовано")) return SingleGeneric("🛑 Скасовано", "Зупинено", "", ColorError, rawText);
                return SingleGeneric("❕ Чисто", "Боргів не знайдено", "Особа чиста", ColorSuccess, "Записів у реєстрі боржників не знайдено.");
            }

            foreach (var block in rawText.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(block)) continue;
                string[] lines = block.Split('\n');
                string debtor = lines.FirstOrDefault(l => l.Contains("Боржник:"))?.Replace("❌ Боржник:", "")?.Replace("Боржник:", "")?.Trim() ?? "Борг";
                records.Add(MakeGeneric(
                    debtor,
                    lines.FirstOrDefault(l => l.Contains("Категорія:"))?.Replace("Категорія:", "")?.Trim() ?? "Не вказано",
                    lines.FirstOrDefault(l => l.Contains("Видавець:"))?.Replace("Видавець:", "")?.Trim() ?? "",
                    ColorError,
                    block.Trim(),
                    true,
                    debtor));
            }

            return onlyExact ? records.Where(r => r.IsExactMatch).ToList() : Dedup(records);
        }

        public static List<GenericRecordDisplay> ParseDeclarations(string rawText, bool onlyExact = false)
        {
            var records = new List<GenericRecordDisplay>();
            if (string.IsNullOrEmpty(rawText)) return records;

            if (!rawText.Contains("Тип:") && !rawText.Contains("Посада:"))
            {
                if (rawText.Contains("❌")) return SingleGeneric("Помилка", "Збій НАЗК", "", ColorError, rawText);
                if (rawText.Contains("скасовано")) return SingleGeneric("🛑 Скасовано", "Зупинено", "", ColorError, rawText);
                return SingleGeneric("❕ Чисто", "Декларацій не знайдено", "", ColorError, "Записів у реєстрі не знайдено.");
            }

            foreach (var block in rawText.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = block.Split('\n');
                if (lines.Length == 0) continue;
                string title = lines.FirstOrDefault(l => l.Contains("Тип:"))?.Replace("📝 Тип:", "").Trim() ?? "Декларація";
                records.Add(MakeGeneric(
                    title,
                    lines.FirstOrDefault(l => l.Contains("Посада:"))?.Replace("Посада:", "").Trim() ?? "Не вказано",
                    lines.FirstOrDefault(l => l.Contains("Декларант:"))?.Replace("Декларант:", "").Trim() ?? "Не вказано",
                    ColorSuccess,
                    block.Trim(),
                    true,
                    title));
            }

            return onlyExact ? records.Where(r => r.IsExactMatch).ToList() : Dedup(records);
        }

        public static List<GenericRecordDisplay> ParseSocial(string rawText, bool onlyExact = false)
        {
            var records = new List<GenericRecordDisplay>();
            if (string.IsNullOrEmpty(rawText)) return records;

            if (!rawText.Contains("Платформа:") && !rawText.Contains("Профіль:") && !rawText.Contains("Посада/Збіг:"))
            {
                if (rawText.Contains("❌")) return SingleGeneric("Помилка", "Захист від ботів", "Змініть IP", ColorError, rawText);
                if (rawText.Contains("скасовано")) return SingleGeneric("🛑 Скасовано", "Зупинено", "", ColorError, rawText);
                return SingleGeneric("❕ Не знайдено", "Даних не знайдено", "", ColorError, "У соціальних мережах та базах резюме нічого не знайдено.");
            }

            foreach (var block in rawText.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] lines = block.Split('\n');
                if (lines.Length == 0) continue;
                string title = lines.FirstOrDefault(l => l.Contains("Платформа:"))?.Replace("📱 Платформа:", "")?.Replace("💼 Платформа:", "").Trim() ?? "Соцмережа/Резюме";
                string subtitle1 = lines.FirstOrDefault(l => l.Contains("Профіль:") || l.Contains("Посада/Збіг:") || l.Contains("Збіг:"))?.Replace("Профіль:", "")?.Replace("Посада/Збіг:", "")?.Replace("Збіг:", "").Trim() ?? "Невідомо";

                string age = lines.FirstOrDefault(l => l.Contains("Вік/Дата:"))?.Replace("Вік/Дата:", "").Trim();
                string city = lines.FirstOrDefault(l => l.Contains("Місце:"))?.Replace("Місце:", "").Trim();
                string link = lines.FirstOrDefault(l => l.Contains("Посилання:"))?.Replace("Посилання:", "").Trim() ?? "Невідомо";

                string subtitle2 = link;
                if (!string.IsNullOrEmpty(age) && age != "Не вказано") subtitle2 = age;
                if (!string.IsNullOrEmpty(city) && city != "Невідомо") subtitle2 = (subtitle2 == link ? city : $"{subtitle2} | {city}");

                bool isDirect = block.Contains("Знайдено за прямим посиланням");
                records.Add(MakeGeneric(title, subtitle1, subtitle2, ColorSuccess, block.Trim(), isDirect || !onlyExact, link));
            }

            return onlyExact ? records.Where(r => r.IsExactMatch).ToList() : Dedup(records);
        }

        public static string BuildSectionMeta(ModuleRunResult result)
        {
            if (result == null)
                return "";

            if (result.FromCache && result.CacheAge.HasValue)
                return $"Оновлено з кешу · {FormatAge(result.CacheAge.Value)}";

            return $"Оновлено · {result.CompletedAtUtc.ToLocalTime():HH:mm:ss}";
        }

        private static string FormatAge(TimeSpan age)
        {
            if (age.TotalMinutes < 1) return "<1 хв тому";
            if (age.TotalHours < 1) return $"{(int)age.TotalMinutes} хв тому";
            return $"{(int)age.TotalHours} год тому";
        }

        private static List<GenericRecordDisplay> SingleGeneric(string t, string s1, string s2, string color, string details) =>
            new() { MakeGeneric(t, s1, s2, color, details, false) };

        private static GenericRecordDisplay MakeGeneric(
            string title, string sub1, string sub2, string color, string details,
            bool isExact, string dedupKey = null) => new()
        {
            Title = title,
            Subtitle1 = sub1,
            Subtitle2 = sub2,
            CardColor = color,
            FullDetails = details,
            IsExactMatch = isExact,
            DedupKey = dedupKey ?? (title + sub1 + sub2).ToLowerInvariant()
        };

        private static CourtCaseDisplay MakeCase(string number, string court, string status, string color, string text) => new()
        {
            CaseNumber = number,
            CourtName = court,
            Status = status,
            CardColor = color,
            FullText = text
        };

        private static List<GenericRecordDisplay> Dedup(List<GenericRecordDisplay> records)
        {
            var seen = new HashSet<string>();
            var result = new List<GenericRecordDisplay>();
            foreach (var record in records)
            {
                string key = string.IsNullOrWhiteSpace(record.DedupKey)
                    ? (record.Title + record.Subtitle1).ToLowerInvariant()
                    : record.DedupKey.ToLowerInvariant();
                if (seen.Add(key))
                    result.Add(record);
            }
            return result;
        }
    }
}