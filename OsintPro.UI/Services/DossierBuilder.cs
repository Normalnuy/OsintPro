using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public static class DossierBuilder
    {
        public static Dossier FromSearchResults(
            SearchContext context,
            IEnumerable<CourtCaseDisplay> courts,
            IEnumerable<GenericRecordDisplay> security,
            IEnumerable<GenericRecordDisplay> debts,
            IEnumerable<GenericRecordDisplay> business,
            IEnumerable<GenericRecordDisplay> declarations,
            IEnumerable<GenericRecordDisplay> footprint,
            IEnumerable<GenericRecordDisplay> social,
            Dossier existing = null)
        {
            string title = context?.DossierTitle ?? existing?.FullName ?? "Пошук";

            var courtItems = courts?
                .Where(c => !IsPlaceholder(c.CaseNumber) && !c.FullText.Contains("CAPTCHA_SESSION"))
                .Select(c => new ParsedItem { Title = c.CaseNumber, Details = $"{c.CourtName}\n{c.Status}\n{c.FullText}" });

            var dossier = new Dossier
            {
                Id = existing?.Id ?? System.Guid.NewGuid().ToString(),
                FullName = title,
                DateCreated = existing?.DateCreated ?? System.DateTime.Now,
                CustomNotes = existing?.CustomNotes ?? "",
                SearchSnapshot = SearchSnapshot.FromContext(context),
                Security = new ObservableCollection<ParsedItem>(MapGeneric(security)),
                CourtCases = new ObservableCollection<ParsedItem>(courtItems ?? Enumerable.Empty<ParsedItem>()),
                Debts = new ObservableCollection<ParsedItem>(MapGeneric(debts)),
                Businesses = new ObservableCollection<ParsedItem>(MapGeneric(business)),
                Declarations = new ObservableCollection<ParsedItem>(MapGeneric(declarations)),
                Market = new ObservableCollection<ParsedItem>(MapGeneric(footprint)),
                Social = new ObservableCollection<ParsedItem>(MapGeneric(social))
            };

            return dossier;
        }

        private static IEnumerable<ParsedItem> MapGeneric(IEnumerable<GenericRecordDisplay> records) =>
            records?.Where(r => !IsPlaceholder(r.Title))
                .Select(r => new ParsedItem
                {
                    Title = r.Title,
                    Details = $"{r.Subtitle1}\n{r.Subtitle2}\n{r.FullDetails}"
                }) ?? Enumerable.Empty<ParsedItem>();

        private static bool IsPlaceholder(string text) =>
            string.IsNullOrWhiteSpace(text) ||
            text.Contains("Очікування") || text.Contains("Помилка") ||
            text.Contains("❕") || text.Contains("🛑");
    }
}