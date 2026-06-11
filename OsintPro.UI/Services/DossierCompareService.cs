using System.Collections.Generic;
using System.Linq;
using System.Text;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public static class DossierCompareService
    {
        public static string Compare(Dossier left, Dossier right)
        {
            if (left == null || right == null)
                return "Оберіть два досьє для порівняння.";

            var sb = new StringBuilder();
            sb.AppendLine($"📊 Порівняння досьє");
            sb.AppendLine($"A: {left.FullName} ({left.DateCreated:dd.MM.yyyy})");
            sb.AppendLine($"B: {right.FullName} ({right.DateCreated:dd.MM.yyyy})");
            sb.AppendLine();

            CompareSection(sb, "🚨 Безпека", left.Security, right.Security);
            CompareSection(sb, "⚖️ Суди", left.CourtCases, right.CourtCases);
            CompareSection(sb, "💰 Борги", left.Debts, right.Debts);
            CompareSection(sb, "🏢 Бізнес", left.Businesses, right.Businesses);
            CompareSection(sb, "📄 НАЗК", left.Declarations, right.Declarations);
            CompareSection(sb, "🌐 Слід", left.Market, right.Market);
            CompareSection(sb, "📱 Соцмережі", left.Social, right.Social);

            return sb.ToString().Trim();
        }

        private static void CompareSection(StringBuilder sb, string title, IEnumerable<ParsedItem> a, IEnumerable<ParsedItem> b)
        {
            var setA = ToKeySet(a);
            var setB = ToKeySet(b);
            var onlyA = setA.Except(setB).ToList();
            var onlyB = setB.Except(setA).ToList();

            if (onlyA.Count == 0 && onlyB.Count == 0)
            {
                sb.AppendLine($"{title}: без відмінностей ({setA.Count} записів)");
                return;
            }

            sb.AppendLine($"{title}: +{onlyB.Count} нових, -{onlyA.Count} відсутніх");
            foreach (var item in onlyB.Take(5))
                sb.AppendLine($"  ➕ {item}");
            foreach (var item in onlyA.Take(5))
                sb.AppendLine($"  ➖ {item}");
            sb.AppendLine();
        }

        private static HashSet<string> ToKeySet(IEnumerable<ParsedItem> items) =>
            items?.Select(i => $"{i.Title}|{i.Details}".Trim().ToLowerInvariant())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToHashSet() ?? new HashSet<string>();
    }
}