using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public sealed class SecuritySearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Security;
        public bool IsEnabled(SearchContext context) => context.HasFio;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.FioFull);

        public Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken) =>
            SecurityScraper.ParseSecurityDataAsync(context.FioFull, cancellationToken, context.MatchOptions);
    }

    public sealed class CourtsSearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Courts;
        public bool IsEnabled(SearchContext context) => context.HasFio;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.FioFull);

        public Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken) =>
            CourtScraper.ParseCourtCasesAsync(context.FioFull, cancellationToken, context.MatchOptions);
    }

    public sealed class BusinessSearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Business;
        public bool IsEnabled(SearchContext context) => context.HasBusiness;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.BusinessQuery);

        public Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken) =>
            ClarityScraper.SearchAllAsync(context.BusinessQuery, cancellationToken);
    }

    public sealed class DebtsSearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Debts;
        public bool IsEnabled(SearchContext context) => context.HasDebtsDecl;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.IdQuery);

        public Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken) =>
            DebtScraper.ParseDebtsDataAsync(context.IdQuery, cancellationToken, context.MatchOptions);
    }

    public sealed class DeclarationsSearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Declarations;
        public bool IsEnabled(SearchContext context) => context.HasDebtsDecl;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.IdQuery);

        public Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken) =>
            DeclarationScraper.ParseDeclarationsDataAsync(context.IdQuery, cancellationToken);
    }

    public sealed class FootprintSearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Footprint;
        public bool IsEnabled(SearchContext context) => context.HasFootprint;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.Contact, context.FioFull, context.Dob);

        public async Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken)
        {
            var footprintTask = DigitalFootprintScraper.ParseFootprintAsync(
                context.Contact, context.FioFull, cancellationToken, context.Dob, session);

            Task<string> phoneTask = Task.FromResult("");
            if (context.HasContact && PhoneLookupScraper.IsPhoneNumber(context.Contact))
                phoneTask = PhoneLookupScraper.AnalyzeAsync(context.Contact, cancellationToken, session);

            await Task.WhenAll(footprintTask, phoneTask);

            string footprintResult = await footprintTask;
            string phoneResult = await phoneTask;

            if (!string.IsNullOrWhiteSpace(phoneResult))
                footprintResult = string.IsNullOrWhiteSpace(footprintResult)
                    ? phoneResult
                    : $"{footprintResult}\n\n{phoneResult}";

            return footprintResult;
        }
    }

    public sealed class SocialSearchModule : ISearchModule
    {
        public SearchModule Module => SearchModule.Social;
        public bool IsEnabled(SearchContext context) => context.HasSocial;
        public string BuildCacheKey(SearchContext context) =>
            SearchResultCache.BuildKey(context, Module, context.SocialQuery, context.FioFull, context.Dob);

        public async Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken)
        {
            var socialTask = (!string.IsNullOrWhiteSpace(context.SocialQuery) || context.HasFio)
                ? SocialScraper.ParseSocialDataAsync(context.SocialQuery, context.FioFull, cancellationToken, context.Dob, session)
                : Task.FromResult("");

            var careerTask = context.HasFio
                ? CareerScraper.SearchResumesAsync(context.FioFull, cancellationToken)
                : Task.FromResult("");

            string nick = context.HasNickname ? context.Nickname.Trim().TrimStart('@') : "";
            var telegramTask = !string.IsNullOrWhiteSpace(nick)
                ? PlatformScrapers.SearchTelegramAsync(nick, cancellationToken, session)
                : Task.FromResult("");

            var vkTask = (context.HasNickname || context.HasFio)
                ? PlatformScrapers.SearchVkAsync(nick, context.FioFull, cancellationToken, session)
                : Task.FromResult("");

            var linkedInTask = (context.HasNickname || context.HasFio)
                ? PlatformScrapers.SearchLinkedInAsync(nick, context.FioFull, cancellationToken, session)
                : Task.FromResult("");

            await Task.WhenAll(socialTask, careerTask, telegramTask, vkTask, linkedInTask);

            var parts = new List<string>();
            void Add(string text)
            {
                if (!string.IsNullOrWhiteSpace(text)) parts.Add(text.Trim());
            }

            Add(await socialTask);
            Add(await careerTask);
            Add(await telegramTask);
            Add(await vkTask);
            Add(await linkedInTask);

            return parts.Count > 0
                ? string.Join("\n\n", parts)
                : "✅ Згадок у соцмережах не знайдено.";
        }
    }
}