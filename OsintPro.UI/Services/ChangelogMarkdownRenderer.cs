using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace OsintPro.UI.Services
{
    public static class ChangelogMarkdownRenderer
    {
        private static readonly Brush Accent = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00C6FF"));
        private static readonly Brush Muted = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9AA0A6"));
        private static readonly Brush Text = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E8E8E8"));
        private static readonly Brush Gold = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD700"));

        public static FlowDocument ToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 14,
                Foreground = Text,
                PagePadding = new Thickness(0)
            };

            if (string.IsNullOrWhiteSpace(markdown))
                return doc;

            foreach (string rawLine in markdown.Replace("\r", "").Split('\n'))
            {
                string line = rawLine.TrimEnd();
                if (string.IsNullOrWhiteSpace(line))
                {
                    doc.Blocks.Add(new Paragraph { Margin = new Thickness(0, 4, 0, 4) });
                    continue;
                }

                if (line.StartsWith("# "))
                {
                    doc.Blocks.Add(MakeParagraph(StripMd(line[2..]), 22, FontWeights.Black, Accent, new Thickness(0, 0, 0, 10)));
                    continue;
                }

                if (line.StartsWith("## "))
                {
                    string heading = StripMd(line[3..]);
                    bool isSection = heading.Contains("✨") || heading.Contains("⚙") || heading.Contains("🧪");
                    var p = MakeParagraph(heading, 16, FontWeights.Bold, isSection ? Gold : Accent, new Thickness(0, 14, 0, 6));
                    doc.Blocks.Add(p);
                    continue;
                }

                if (line.StartsWith("---"))
                {
                    doc.Blocks.Add(new BlockUIContainer(new System.Windows.Shapes.Rectangle
                    {
                        Height = 1,
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3300C6FF")),
                        Margin = new Thickness(0, 10, 0, 10)
                    }));
                    continue;
                }

                if (line.StartsWith("- ") || line.StartsWith("• "))
                {
                    var bullet = new Paragraph { Margin = new Thickness(14, 2, 0, 2), LineHeight = 24 };
                    bullet.Inlines.Add(new Run("▸ ") { Foreground = Accent, FontWeight = FontWeights.Bold });
                    AppendInlineWithBold(bullet, line[2..].Trim());
                    doc.Blocks.Add(bullet);
                    continue;
                }

                var normal = new Paragraph { Margin = new Thickness(0, 2, 0, 2), LineHeight = 24 };
                AppendInlineWithBold(normal, line);
                doc.Blocks.Add(normal);
            }

            return doc;
        }

        private static Paragraph MakeParagraph(string text, double size, FontWeight weight, Brush foreground, Thickness margin)
        {
            var p = new Paragraph { Margin = margin, LineHeight = size + 6 };
            var run = new Run(StripMd(text)) { FontSize = size, FontWeight = weight, Foreground = foreground };
            p.Inlines.Add(run);
            return p;
        }

        private static void AppendInlineWithBold(Paragraph paragraph, string text)
        {
            foreach (Match match in Regex.Matches(text, @"\*\*(.+?)\*\*|([^*]+)"))
            {
                if (match.Groups[1].Success)
                {
                    paragraph.Inlines.Add(new Run(match.Groups[1].Value)
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = Accent
                    });
                }
                else if (match.Groups[2].Success)
                {
                    string part = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(part))
                        paragraph.Inlines.Add(new Run(part) { Foreground = Text });
                }
            }
        }

        private static string StripMd(string text) =>
            text.Replace("**", "").Replace("__", "").Trim();
    }
}