using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace OsintPro.UI.Services
{
    public static class PlaywrightBootstrap
    {
        public static bool IsChromiumInstalled()
        {
            string baseDir = AppContext.BaseDirectory;
            string playwrightDir = Path.Combine(baseDir, ".playwright");
            if (!Directory.Exists(playwrightDir)) return false;

            foreach (var dir in Directory.EnumerateDirectories(playwrightDir, "*", SearchOption.AllDirectories))
            {
                if (dir.Contains("chromium", StringComparison.OrdinalIgnoreCase) &&
                    Directory.EnumerateFiles(dir, "chrome.exe", SearchOption.AllDirectories).Any())
                    return true;
            }
            return false;
        }

        public static async Task<bool> EnsureChromiumAsync(Window owner)
        {
            if (IsChromiumInstalled()) return true;

            var result = MessageBox.Show(
                owner,
                "Для пошуку потрібен браузер Chromium (Playwright).\n\nВстановити зараз? Це може зайняти кілька хвилин.",
                "Перший запуск",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return false;

            try
            {
                var exit = await Task.Run(() =>
                {
                    string script = Path.Combine(AppContext.BaseDirectory, "playwright.ps1");
                    if (!File.Exists(script))
                        return -1;

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-ExecutionPolicy Bypass -File \"{script}\" install chromium",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = System.Diagnostics.Process.Start(psi);
                    proc?.WaitForExit(300000);
                    return proc?.ExitCode ?? -1;
                });

                if (exit != 0)
                {
                    MessageBox.Show(owner,
                        "Не вдалося встановити Chromium автоматично.\nЗапустіть у папці програми:\nplaywright.ps1 install chromium",
                        "Playwright", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                return IsChromiumInstalled();
            }
            catch (Exception ex)
            {
                MessageBox.Show(owner, $"Помилка встановлення Chromium:\n{ex.Message}", "Playwright",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}