using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class UpdateCheckerServiceTests
{
    [Theory]
    [InlineData("v1.0.3", "1.0.3")]
    [InlineData("v.1.0.1", "1.0.1")]
    [InlineData("1.0.0", "1.0.0")]
    public void NormalizeVersion_ParsesTags(string input, string expected) =>
        Assert.Equal(expected, UpdateCheckerService.NormalizeVersion(input));

    [Fact]
    public void CompareVersions_OrdersCorrectly()
    {
        Assert.True(UpdateCheckerService.CompareVersions("1.0.3", "1.0.2") > 0);
        Assert.True(UpdateCheckerService.CompareVersions("1.0.1", "1.0.2") < 0);
    }
}