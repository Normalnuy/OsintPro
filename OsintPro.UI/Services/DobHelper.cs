using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OsintPro.UI.Services
{
    public static class DobHelper
    {
        private static readonly Regex DobPattern = new(
            @"^(0[1-9]|[12][0-9]|3[01])\.(0[1-9]|1[0-2])\.(19|20)\d{2}$",
            RegexOptions.Compiled);

        public static bool IsValid(string dob) =>
            !string.IsNullOrWhiteSpace(dob) && DobPattern.IsMatch(dob.Trim());

        public static IEnumerable<string> GetVariants(string dob)
        {
            if (!IsValid(dob))
                yield break;

            string trimmed = dob.Trim();
            yield return trimmed;
            yield return trimmed.Replace('.', '/');
            yield return trimmed.Replace('.', '-');

            var parts = trimmed.Split('.');
            if (parts.Length == 3)
            {
                yield return $"{parts[0]}.{parts[1]}.{parts[2][^2..]}";
                yield return $"{parts[0]}/{parts[1]}/{parts[2][^2..]}";
                yield return $"{parts[0]}{parts[1]}{parts[2]}";
                yield return $"{parts[2]}-{parts[1]}-{parts[0]}";
            }
        }

        public static bool AppearsIn(string text, string dob)
        {
            if (!IsValid(dob) || string.IsNullOrWhiteSpace(text))
                return false;

            string normalized = text.ToLowerInvariant();
            return GetVariants(dob).Any(v =>
                normalized.Contains(v.ToLowerInvariant(), StringComparison.Ordinal));
        }
    }
}