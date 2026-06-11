using OsintPro.UI.Models;
using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class DossierCompareServiceTests
{
    [Fact]
    public void Compare_ReportsDifferences()
    {
        var left = new Dossier
        {
            FullName = "A",
            Debts = { new ParsedItem { Title = "Борг 1", Details = "100" } }
        };
        var right = new Dossier
        {
            FullName = "B",
            Debts = { new ParsedItem { Title = "Борг 2", Details = "200" } }
        };

        string report = DossierCompareService.Compare(left, right);
        Assert.Contains("Порівняння", report);
        Assert.Contains("➕", report);
    }
}