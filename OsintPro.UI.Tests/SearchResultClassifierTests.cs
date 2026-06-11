using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class SearchResultClassifierTests
{
    [Fact]
    public void DebtorResultIsNotError()
    {
        string result = "❌ Боржник: ІВАНОВ ІВАН\nКатегорія: Податки";
        Assert.False(SearchResultClassifier.IsErrorResult(result));
        Assert.True(SearchResultClassifier.IsCacheableResult(result));
    }

    [Fact]
    public void RealErrorIsDetected()
    {
        Assert.True(SearchResultClassifier.IsErrorResult("❌ Помилка: timeout"));
        Assert.False(SearchResultClassifier.IsCacheableResult("❌ Помилка: timeout"));
    }

    [Fact]
    public void CaptchaIsNotCacheable()
    {
        Assert.False(SearchResultClassifier.IsCacheableResult("⚠️КАПЧА⚠️|session"));
    }
}