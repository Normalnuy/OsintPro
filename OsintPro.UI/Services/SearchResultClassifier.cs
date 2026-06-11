using System;

namespace OsintPro.UI.Services
{
    public static class SearchResultClassifier
    {
        public static bool IsErrorResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
                return false;

            string trimmed = result.Trim();

            if (trimmed.Contains("🛑", StringComparison.Ordinal))
                return false;

            if (trimmed.StartsWith("✅", StringComparison.Ordinal) ||
                trimmed.StartsWith("❕", StringComparison.Ordinal) ||
                trimmed.Contains("⚠️КАПЧА⚠️", StringComparison.Ordinal))
                return false;

            if (ContainsSuccessMarkers(trimmed))
                return false;

            if (trimmed.StartsWith("❌ Помилка", StringComparison.Ordinal) ||
                trimmed.StartsWith("❌ Мін'юст", StringComparison.Ordinal) ||
                trimmed.StartsWith("❌ Сесія", StringComparison.Ordinal) ||
                trimmed.StartsWith("❌ Помилка API", StringComparison.Ordinal))
                return true;

            return trimmed.StartsWith("❌", StringComparison.Ordinal) &&
                   !trimmed.Contains("Боржник:", StringComparison.Ordinal);
        }

        public static bool IsCacheableResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
                return false;

            if (result.Contains("⚠️КАПЧА⚠️", StringComparison.Ordinal))
                return false;

            if (result.Contains("🛑", StringComparison.Ordinal))
                return false;

            return !IsErrorResult(result);
        }

        private static bool ContainsSuccessMarkers(string text)
        {
            return text.Contains("Боржник:", StringComparison.Ordinal) ||
                   text.Contains("Категорія:", StringComparison.Ordinal) ||
                   text.Contains("📂 Справа", StringComparison.Ordinal) ||
                   text.Contains("Справа №", StringComparison.Ordinal) ||
                   text.Contains("@@BLOCK_SEPARATOR@@", StringComparison.Ordinal) ||
                   text.Contains("TITLE::", StringComparison.Ordinal) ||
                   text.Contains("Платформа:", StringComparison.Ordinal) ||
                   text.Contains("Профіль:", StringComparison.Ordinal) ||
                   text.Contains("📝 Тип:", StringComparison.Ordinal) ||
                   text.Contains("Декларант:", StringComparison.Ordinal) ||
                   text.Contains("Посилання:", StringComparison.Ordinal);
        }
    }
}