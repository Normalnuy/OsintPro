using System;
using OsintPro.UI.Services;
using Xunit;

namespace OsintPro.UI.Tests;

public class ResultParserTests
{
    [Fact]
    public void ParseDebts_FindsDebtor()
    {
        string raw = "❌ Боржник: ІВАНОВ ІВАН\nКатегорія: Податки\nВидавець: ДПС\n";
        var records = ResultParser.ParseDebts(raw);
        Assert.Single(records);
        Assert.Contains("ІВАНОВ", records[0].Title);
    }

    [Fact]
    public void ParseBusiness_OnlyExact_FiltersNonExact()
    {
        string raw = "TITLE::Компанія\nSUB1::ТОВ\nSUB2::\nEXACT::NO\nDETAILS::\n@@BLOCK_SEPARATOR@@TITLE::Точна\nSUB1::ТОВ\nSUB2::\nEXACT::YES\nDETAILS::\n";
        var all = ResultParser.ParseBusiness(raw, onlyExact: false);
        var exact = ResultParser.ParseBusiness(raw, onlyExact: true);
        Assert.True(all.Count >= 2);
        Assert.Single(exact);
    }

    [Fact]
    public void BuildSectionMeta_ShowsCacheHint()
    {
        var result = new ModuleRunResult
        {
            FromCache = true,
            CacheAge = TimeSpan.FromMinutes(5)
        };
        string meta = ResultParser.BuildSectionMeta(result);
        Assert.Contains("кешу", meta);
    }
}