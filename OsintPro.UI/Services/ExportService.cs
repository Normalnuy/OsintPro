using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public static class ExportService
    {
        public static void ExportDossierJson(Dossier dossier, string filePath)
        {
            string json = JsonConvert.SerializeObject(dossier, Formatting.Indented);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        public static void ExportDossierCsv(Dossier dossier, string filePath)
        {
            var lines = new List<string> { "Section,Title,Details" };
            AppendSection(lines, "Security", dossier.Security);
            AppendSection(lines, "Courts", dossier.CourtCases);
            AppendSection(lines, "Debts", dossier.Debts);
            AppendSection(lines, "Business", dossier.Businesses);
            AppendSection(lines, "Declarations", dossier.Declarations);
            AppendSection(lines, "Market", dossier.Market);
            AppendSection(lines, "Social", dossier.Social);
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        private static void AppendSection(List<string> lines, string section, IEnumerable<ParsedItem> items)
        {
            foreach (var item in items ?? Enumerable.Empty<ParsedItem>())
            {
                lines.Add($"{Escape(section)},{Escape(item.Title)},{Escape(item.Details)}");
            }
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains('"') || value.Contains(',') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";

            return value.Replace('\n', ' ');
        }
    }
}