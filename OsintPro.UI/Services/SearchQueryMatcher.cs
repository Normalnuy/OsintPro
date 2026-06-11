using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace OsintPro.UI.Services
{
    public static class SearchQueryMatcher
    {
        private static readonly Regex InnPattern = new(@"^\d{10}$", RegexOptions.Compiled);

        public static bool IsInn(string query) =>
            !string.IsNullOrWhiteSpace(query) && InnPattern.IsMatch(query.Trim());

        public static string Normalize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            return Regex.Replace(text.ToLowerInvariant(), @"[\s'’""\-\.]", "");
        }

        public static string[] GetTokens(string query) =>
            (query ?? "")
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim())
                .Where(w => w.Length > 0)
                .ToArray();

        public static bool MatchesPerson(string candidate, string query, SearchMatchOptions options = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;

            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            var queryTokens = GetTokens(query);
            if (queryTokens.Length == 0)
                return true;

            string normCandidate = Normalize(candidate);
            string normQuery = Normalize(query);

            bool personMatch = options.Strict
                ? MatchesStrict(normCandidate, queryTokens)
                : MatchesSoft(normCandidate, normQuery, queryTokens);

            if (!personMatch)
                return false;

            if (!string.IsNullOrWhiteSpace(options.Dob) && DobHelper.IsValid(options.Dob))
            {
                if (!DobHelper.AppearsIn(candidate, options.Dob))
                    return false;
            }

            return true;
        }

        public static int GetMatchScore(string candidate, string query, SearchMatchOptions options = default)
        {
            if (!MatchesPerson(candidate, query, options))
                return 0;

            string normCandidate = Normalize(candidate);
            string normQuery = Normalize(query);
            var queryTokens = GetTokens(query);

            int score = 40;

            if (normCandidate.Contains(normQuery))
                score = 100;
            else if (queryTokens.Length >= 3 && normCandidate.Contains(Normalize(queryTokens[2])))
                score = 80;
            else if (queryTokens.Length >= 2)
                score = 60;

            if (!string.IsNullOrWhiteSpace(options.Dob) && DobHelper.AppearsIn(candidate, options.Dob))
                score = Math.Min(100, score + 15);

            return options.Strict ? Math.Max(score, 70) : score;
        }

        private static bool MatchesSoft(string normCandidate, string normQuery, string[] queryTokens)
        {
            if (normCandidate.Contains(normQuery))
                return true;

            if (queryTokens.Length == 1)
                return normCandidate.Contains(Normalize(queryTokens[0]));

            string normSurname = Normalize(queryTokens[0]);
            string normFirst = Normalize(queryTokens[1]);

            if (!normCandidate.Contains(normSurname) || !normCandidate.Contains(normFirst))
                return false;

            if (queryTokens.Length >= 3)
                return normCandidate.Contains(Normalize(queryTokens[2]));

            return true;
        }

        private static bool MatchesStrict(string normCandidate, string[] queryTokens)
        {
            foreach (var token in queryTokens)
            {
                if (!normCandidate.Contains(Normalize(token)))
                    return false;
            }

            return true;
        }
    }
}