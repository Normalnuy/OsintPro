using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace OsintPro.UI.Services
{
    public sealed class SearchSession : IAsyncDisposable
    {
        private BrowserLease _lease;
        private bool _disposed;

        public IBrowserContext Context { get; private set; }

        public static async Task<SearchSession> CreateAsync(CancellationToken cancellationToken = default)
        {
            var lease = await BrowserManager.AcquireBrowserLeaseAsync(cancellationToken);
            var context = await BrowserManager.CreateStealthContextAsync(lease.Browser, cancellationToken);
            return new SearchSession
            {
                _lease = lease,
                Context = context
            };
        }

        public async Task<IPage> NewPageAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SearchSession));

            cancellationToken.ThrowIfCancellationRequested();
            return await Context.NewPageAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _disposed = true;

            try
            {
                if (Context != null)
                    await Context.CloseAsync();
            }
            catch { }

            _lease?.Dispose();
            Context = null;
            _lease = null;
        }
    }
}