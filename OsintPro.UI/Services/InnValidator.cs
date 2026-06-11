using System.Linq;
using System.Text.RegularExpressions;

namespace OsintPro.UI.Services
{
    public static class InnValidator
    {
        private static readonly Regex FormatPattern = new(@"^\d{10}$", RegexOptions.Compiled);

        private static readonly int[] Weights1 = { -1, 5, 7, 9, 4, 6, 10, 5, 7 };
        private static readonly int[] Weights2 = { -1, 3, 5, 7, 9, 4, 6, 10, 5 };

        public static bool HasValidFormat(string inn) =>
            !string.IsNullOrWhiteSpace(inn) && FormatPattern.IsMatch(inn.Trim());

        public static bool IsValidChecksum(string inn)
        {
            if (!HasValidFormat(inn))
                return false;

            string digits = inn.Trim();
            int control = CalculateControlDigit(digits, Weights1);
            if (control == 10)
                control = CalculateControlDigit(digits, Weights2);
            if (control == 10)
                control = 0;

            return digits[9] - '0' == control;
        }

        public static bool IsValid(string inn) => HasValidFormat(inn) && IsValidChecksum(inn);

        public static string ValidateMessage(string inn)
        {
            if (string.IsNullOrWhiteSpace(inn))
                return "ІПН не вказано";

            if (!HasValidFormat(inn))
                return "ІПН має містити рівно 10 цифр";

            if (!IsValidChecksum(inn))
                return "Невірна контрольна цифра РНОКПП";

            return "";
        }

        private static int CalculateControlDigit(string digits, int[] weights)
        {
            int sum = digits
                .Take(9)
                .Select((ch, i) => (ch - '0') * weights[i])
                .Sum();

            return sum % 11;
        }
    }
}