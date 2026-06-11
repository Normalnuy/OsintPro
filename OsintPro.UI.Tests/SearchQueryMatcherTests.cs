using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class SearchQueryMatcherTests
{
    [Fact]
    public void SoftMatchAcceptsPartialName()
    {
        Assert.True(SearchQueryMatcher.MatchesPerson("ІВАНОВ ІВАН ПЕТРОВИЧ", "Іванов Іван"));
    }

    [Fact]
    public void StrictMatchRequiresAllTokens()
    {
        var strict = new SearchMatchOptions(true);
        Assert.False(SearchQueryMatcher.MatchesPerson("ІВАНОВ ІВАН", "Іванов Іван Петрович", strict));
        Assert.True(SearchQueryMatcher.MatchesPerson("ІВАНОВ ІВАН ПЕТРОВИЧ", "Іванов Іван Петрович", strict));
    }

    [Fact]
    public void DobFilterExcludesMismatch()
    {
        var withDob = new SearchMatchOptions(false, "15.03.1990");
        Assert.False(SearchQueryMatcher.MatchesPerson("ІВАНОВ ІВАН", "Іванов Іван", withDob));
        Assert.True(SearchQueryMatcher.MatchesPerson("ІВАНОВ ІВАН 15.03.1990", "Іванов Іван", withDob));
    }

    [Fact]
    public void GetMatchScoreReturnsZeroWhenNoMatch()
    {
        Assert.Equal(0, SearchQueryMatcher.GetMatchScore("ПЕТРОВ", "Іванов Іван"));
    }
}