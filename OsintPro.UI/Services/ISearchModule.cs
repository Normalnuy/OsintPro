using System.Threading;
using System.Threading.Tasks;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public interface ISearchModule
    {
        SearchModule Module { get; }
        bool IsEnabled(SearchContext context);
        string BuildCacheKey(SearchContext context);
        Task<string> ExecuteAsync(SearchContext context, SearchSession session, CancellationToken cancellationToken);
    }
}