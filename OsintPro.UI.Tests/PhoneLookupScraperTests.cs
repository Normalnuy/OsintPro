using System.Threading;
using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class PhoneLookupScraperTests
{
    [Theory]
    [InlineData("+380501234567", true)]
    [InlineData("0501234567", true)]
    [InlineData("not-a-phone", false)]
    [InlineData("", false)]
    public void IsPhoneNumber_DetectsValidNumbers(string input, bool expected) =>
        Assert.Equal(expected, PhoneLookupScraper.IsPhoneNumber(input));

    [Theory]
    [InlineData("0501234567", "+380501234567")]
    [InlineData("+380671112233", "+380671112233")]
    public void NormalizePhone_FormatsUkrainianNumbers(string input, string expected) =>
        Assert.Equal(expected, PhoneLookupScraper.NormalizePhone(input));

    [Fact]
    public async Task AnalyzeAsync_UaVodafoneNumber_ContainsOperator()
    {
        string result = await PhoneLookupScraper.AnalyzeAsync("+380501419040", CancellationToken.None);
        Assert.Contains("Vodafone", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Україна", result);
        Assert.Contains("Мобільний", result);
    }
}