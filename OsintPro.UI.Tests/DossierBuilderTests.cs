using OsintPro.UI.Models;
using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class DossierBuilderTests
{
    [Fact]
    public void FromSearchResults_WithoutExisting_GeneratesDistinctIds()
    {
        var context = SearchContext.FromInputs("Іванов", "Іван", "", "", "", "", "", false, false, true);

        var first = DossierBuilder.FromSearchResults(context, [], [], [], [], [], [], []);
        var second = DossierBuilder.FromSearchResults(context, [], [], [], [], [], [], []);

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void FromSearchResults_WithExisting_PreservesIdAndDateCreated()
    {
        var context = SearchContext.FromInputs("Петров", "Петро", "", "", "", "", "", false, false, true);
        var existing = new Dossier
        {
            Id = "archive-id-123",
            DateCreated = new DateTime(2024, 1, 15),
            CustomNotes = "Нотатки"
        };

        var dossier = DossierBuilder.FromSearchResults(context, [], [], [], [], [], [], [], existing);

        Assert.Equal("archive-id-123", dossier.Id);
        Assert.Equal(existing.DateCreated, dossier.DateCreated);
        Assert.Equal("Нотатки", dossier.CustomNotes);
    }
}