using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Sentry;

namespace OsintPro.UI.Services
{
    public sealed class BrowserLease : IDisposable
    {
        private readonly SemaphoreSlim _slot;
        private bool _released;

        internal BrowserLease(IBrowser browser, SemaphoreSlim slot)
        {
            Browser = browser;
            _slot = slot;
        }

        public IBrowser Browser { get; }

        public void Dispose()
        {
            if (_released)
                return;

            _released = true;
            _slot.Release();
        }
    }

    public static class BrowserManager
    {
        private static IPlaywright _playwright;
        private static IBrowser _headlessBrowser;
        private static IBrowser _visibleBrowser;
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static readonly SemaphoreSlim _headlessSlot = new(2, 2);
        private static readonly SemaphoreSlim _visibleSlot = new(3, 3);

        private const string StealthInitScript = @"
            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
            window.chrome = { runtime: {} };
            Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3] });
            Object.defineProperty(navigator, 'languages', { get: () => ['uk-UA', 'uk', 'en-US', 'en'] });
        ";

        private const string DefaultUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        public static async Task<IBrowser> GetHeadlessBrowserAsync()
        {
            await EnsureInitializedAsync();
            return _headlessBrowser;
        }

        public static async Task<IBrowser> GetVisibleBrowserAsync()
        {
            await EnsureInitializedAsync();
            return _visibleBrowser;
        }

        public static async Task<BrowserLease> AcquireHeadlessLeaseAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();
            await _headlessSlot.WaitAsync(cancellationToken);
            return new BrowserLease(_headlessBrowser, _headlessSlot);
        }

        public static async Task<BrowserLease> AcquireBrowserLeaseAsync(CancellationToken cancellationToken = default)
        {
            await EnsureInitializedAsync();
            await _visibleSlot.WaitAsync(cancellationToken);
            return new BrowserLease(_visibleBrowser, _visibleSlot);
        }

        public static async Task<IBrowserContext> CreateStealthContextAsync(IBrowser browser, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = DefaultUserAgent
            });

            await context.AddInitScriptAsync(StealthInitScript);
            return context;
        }

        private static async Task EnsureInitializedAsync()
        {
            if (_playwright != null && _headlessBrowser != null && _visibleBrowser != null)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_playwright == null)
                    _playwright = await Playwright.CreateAsync();

                if (_headlessBrowser == null)
                {
                    _headlessBrowser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = true
                    });
                }

                if (_visibleBrowser == null)
                {
                    _visibleBrowser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                    {
                        Headless = false,
                        Args = new[] { "--headless=new", "--disable-blink-features=AutomationControlled" }
                    });
                }
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public static async Task CloseAllAsync()
        {
            try
            {
                if (_headlessBrowser != null)
                    await _headlessBrowser.CloseAsync();
                if (_visibleBrowser != null)
                    await _visibleBrowser.CloseAsync();
                _playwright?.Dispose();
            }
            catch { }
            finally
            {
                _headlessBrowser = null;
                _visibleBrowser = null;
                _playwright = null;
            }
        }
    }
}