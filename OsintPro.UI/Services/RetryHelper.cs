using System;
using System.Threading;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public static class RetryHelper
    {
        public static bool IsRetriable(Exception ex) => ex switch
        {
            TimeoutException => true,
            TaskCanceledException => false,
            OperationCanceledException => false,
            _ when ex.Message.Contains("Cloudflare", StringComparison.OrdinalIgnoreCase) => true,
            _ when ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            _ when ex.Message.Contains("net::", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };

        public static async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> action,
            CancellationToken cancellationToken,
            int maxAttempts = 3,
            int initialDelayMs = 800,
            Func<Exception, bool> shouldRetry = null)
        {
            shouldRetry ??= IsRetriable;
            Exception last = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await action(cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    last = ex;
                    if (attempt >= maxAttempts || !shouldRetry(ex))
                        throw;

                    int delay = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw last ?? new InvalidOperationException("Retry failed");
        }
    }
}