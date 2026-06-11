using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using OsintPro.UI.Models;
using System.Collections.ObjectModel;

namespace OsintPro.UI.Services
{
    public class PdfGenerator
    {
        // Векторний логотип "Кібер-Чайка" у форматі SVG
        private const string LogoSvg = @"
        <svg width='100' height='100' viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'>
            <polygon points='10,40 45,55 35,15' fill='#0072FF' opacity='0.8'/>
            <polygon points='10,40 45,55 30,65' fill='#005bb5' opacity='0.6'/>
            <polygon points='90,30 55,55 65,10' fill='#00C6FF' opacity='0.9'/>
            <polygon points='90,30 55,55 75,55' fill='#0099cc' opacity='0.7'/>
            <polygon points='45,55 55,55 50,90' fill='#00C6FF' opacity='0.9'/>
            <polygon points='45,55 55,55 65,35 40,30' fill='#0072FF' opacity='0.9'/>
            <polygon points='40,30 25,25 45,40' fill='#00C6FF'/>
        </svg>";

        public static void ExportToPdf(Dossier dossier, string outputPath)
        {
            // Налаштування ліцензії QuestPDF
            QuestPDF.Settings.License = LicenseType.Community;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    // Формат сторінки та відступи
                    page.Size(PageSizes.A4);
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial).FontColor(Colors.Grey.Darken3));

                    // 1. ШАПКА (Header) - Відображається на кожній сторінці
                    page.Header().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(10).Row(row =>
                    {
                        // Ліва частина: Логотип + Назва застосунку
                        row.ConstantItem(40).Svg(LogoSvg);
                        row.ConstantItem(150).PaddingLeft(10).Column(col =>
                        {
                            col.Item().PaddingTop(2).Text("JUSTIN OSINT").FontSize(16).FontColor("#0072FF").Black();
                            col.Item().Text("Аналітична система").FontSize(9).FontColor(Colors.Grey.Medium);
                        });

                        // Права частина: Кому досьє + Дата
                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item().Text("ДОСЬЄ").FontSize(18).FontColor(Colors.Grey.Darken3).SemiBold();
                            col.Item().Text(dossier.FullName).FontSize(14).FontColor("#00C6FF").Bold();
                            col.Item().PaddingTop(2).Text($"Сформовано: {dossier.DateCreated:dd.MM.yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                    });

                    // 2. КОНТЕНТНА ЧАСТИНА
                    page.Content().PaddingVertical(15).Column(x =>
                    {
                        // Власні нотатки (якщо є)
                        if (!string.IsNullOrWhiteSpace(dossier.CustomNotes))
                        {
                            x.Item().PaddingBottom(15).Background("#F8F9FA").BorderLeft(3).BorderColor("#FFD700").Padding(10).Column(col =>
                            {
                                col.Item().Text("📝 АНАЛІТИЧНА ДОВІДКА / НОТАТКИ").FontSize(12).FontColor(Colors.Grey.Darken4).Bold();
                                col.Item().PaddingTop(5).Text(dossier.CustomNotes).FontSize(11).LineHeight(1.2f);
                            });
                        }

                        // Малювання секцій з даними (ОНОВЛЕНО)
                        DrawSection(x, "🛡️ РЕЄСТРИ БЕЗПЕКИ", dossier.Security, "#00C6FF"); // Блакитний
                        DrawSection(x, "⚖️ СУДОВІ СПРАВИ", dossier.CourtCases, "#0072FF"); // Синій
                        DrawSection(x, "💰 БОРГИ ТА ШТРАФИ", dossier.Debts, "#0072FF"); // Синій
                        DrawSection(x, "🏢 БІЗНЕС ТА ЗВ'ЯЗКИ", dossier.Businesses, "#00C6FF"); // Блакитний
                        DrawSection(x, "📄 ДЕКЛАРАЦІЇ НАЗК", dossier.Declarations, "#0072FF"); // Синій
                        DrawSection(x, "🌐 ЦИФРОВИЙ СЛІД ТА МАРКЕТПЛЕЙСИ", dossier.Market, "#0099cc"); // Темно-блакитний
                        DrawSection(x, "📱 СОЦІАЛЬНІ МЕРЕЖІ ТА РЕЗЮМЕ", dossier.Social, "#00C6FF"); // Блакитний

                        // Якщо досьє повністю пусте (ОНОВЛЕНО)
                        if (string.IsNullOrWhiteSpace(dossier.CustomNotes) &&
                            dossier.CourtCases.Count == 0 && dossier.Debts.Count == 0 &&
                            dossier.Businesses.Count == 0 && dossier.Declarations.Count == 0 &&
                            dossier.Security.Count == 0 && dossier.Market.Count == 0 && dossier.Social.Count == 0)
                        {
                            x.Item().AlignCenter().PaddingTop(50).Text("У даному досьє немає жодної інформації.").FontSize(14).FontColor(Colors.Grey.Medium);
                        }
                    });

                    // 3. ФУТЕР (Footer) - Відображається на кожній сторінці знизу
                    page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().Text("Згенеровано системою Justin OSINT").FontSize(8).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Сторінка ").FontSize(8).FontColor(Colors.Grey.Medium);
                            t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Medium);
                            t.Span(" з ").FontSize(8).FontColor(Colors.Grey.Medium);
                            t.TotalPages().FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            }).GeneratePdf(outputPath);
        }

        /// <summary>
        /// Допоміжний метод для красивого відмальовування блоків даних
        /// </summary>
        private static void DrawSection(ColumnDescriptor column, string title, ObservableCollection<ParsedItem> items, string accentColor)
        {
            if (items == null || items.Count == 0) return;

            // Заголовок секції з підкресленням
            column.Item().PaddingTop(10).PaddingBottom(5).Column(headerCol =>
            {
                headerCol.Item().Text(title).FontSize(13).FontColor(accentColor).Bold();
                headerCol.Item().PaddingTop(2).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });

            // Відмальовування кожної картки в секції
            foreach (var item in items)
            {
                column.Item().PaddingBottom(8).Background("#F8F9FA").Padding(10).Column(itemCol =>
                {
                    // Назва
                    itemCol.Item().Text(item.Title).FontSize(11).FontColor(Colors.Grey.Darken4).SemiBold();

                    // Деталі (текст із збереженим форматуванням)
                    if (!string.IsNullOrWhiteSpace(item.Details))
                    {
                        itemCol.Item().PaddingTop(3).Text(item.Details).FontSize(10).FontColor(Colors.Grey.Darken2).LineHeight(1.2f);
                    }
                });
            }

            // Відступ між секціями
            column.Item().PaddingBottom(10);
        }
    }
}