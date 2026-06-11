using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class InnValidatorTests
{
    [Theory]
    [InlineData("1234567890", false)]
    [InlineData("123456789", false)]
    [InlineData("abcdefghij", false)]
    public void InvalidInnFormatsAreRejected(string inn, bool expectedValid)
    {
        Assert.Equal(expectedValid, InnValidator.IsValid(inn));
    }

    [Fact]
    public void ValidChecksumPasses()
    {
        Assert.True(InnValidator.IsValidChecksum("3257465220"));
        Assert.True(InnValidator.IsValid("3257465220"));
    }

    [Fact]
    public void ValidateMessageReturnsUkrainianText()
    {
        Assert.Contains("10 цифр", InnValidator.ValidateMessage("123"));
        Assert.Contains("контрольна", InnValidator.ValidateMessage("1234567890"));
    }
}