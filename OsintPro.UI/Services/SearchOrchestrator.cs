using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OsintPro.UI.Models;
using Sentry;

namespace OsintPro.UI.Services
{
    public sealed class ModuleRunResult
    {
        public SearchModule Module { get; init; }
        public string RawText { get; init; } = "";
        public bool FromCache { get; init; }
        public TimeSpan? CacheAge { get; init; }
        public DateTime CompletedAtUtc { get; init; } = DateTime.UtcNow;
    }

    public class SearchOrchestrator
    {
        private readonly IReadOnlyList<ISearchModule> _modules = new ISearchModule[]
        {
            new SecuritySearchModule(),
            new CourtsSearchModule(),
            new BusinessSearchModule(),
            new DebtsSearchModule(),
            new DeclarationsSearchModule(),
            new FootprintSearchModule(),
            new SocialSearchModule()
        };

        public IReadOnlyList<ISearchModule> Modules => _modules;

        public async Task<ModuleRunResult> RunModuleAsync(
            ISearchModule module,
            SearchContext context,
            SearchSession session,
            SearchProgressTracker progress,
            CancellationToken cancellationToken,
            bool invalidateCache = false)
        {
            string cacheKey = module.BuildCacheKey(context);

            if (invalidateCache)
                SearchResultCache.Invalidate(module.Module, cacheKey);

            progress.SetRunning(module.Module);

            if (!invalidateCache && SearchResultCache.TryGet(module.Module, cacheKey, out var cached))
            {
                progress.SetCached(module.Module, cached.Age);
                return new ModuleRunResult
                {
                    Module = module.Module,
                    RawText = cached.Result,
                    FromCache = true,
                    CacheAge = cached.Age
                };
            }

            try
            {
                string result = await module.ExecuteAsync(context, session, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    progress.SetCancelled(module.Module);
                }
                else if (SearchResultClassifier.IsErrorResult(result))
                {
                    progress.SetError(module.Module);
                }
                else
                {
                    SearchResultCache.Set(module.Module, cacheKey, result, context.CacheEnabled);
                    progress.SetCompleted(module.Module);
                }

                return new ModuleRunResult
                {
                    Module = module.Module,
                    RawText = result,
                    FromCache = false
                };
            }
            catch (TaskCanceledException)
            {
                progress.SetCancelled(module.Module);
                throw;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                progress.SetError(module.Module);
                return new ModuleRunResult
                {
                    Module = module.Module,
                    RawText = $"❌ Помилка: {ex.Message}",
                    FromCache = false
                };
            }
        }

        public ISearchModule GetModule(SearchModule module) =>
            _modules.FirstOrDefault(m => m.Module == module)
            ?? throw new ArgumentOutOfRangeException(nameof(module));
    }
}