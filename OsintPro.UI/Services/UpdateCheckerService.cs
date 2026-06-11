using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OsintPro.UI.Services
{
    public sealed class UpdateCheckResult
    {
        public bool UpdateAvailable { get; init; }
        public string LocalVersion { get; init; } = "";
        public string LatestVersion { get; init; } = "";
        public string DownloadUrl { get; init; } = "";
        public string ReleaseNotes { get; init; } = "";
    }

    public static class UpdateCheckerService
    {
        private const string GitHubApiUrl = "https://api.github.com/repos/Normalnuy/OsintPro/releases/latest";

        public static async Task<UpdateCheckResult> CheckAsync()
        {
            string local = GetLocalVersion();
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                client.DefaultRequestHeaders.Add("User-Agent", "JustinOSINT-App/1.0.5");
                string json = await client.GetStringAsync(GitHubApiUrl);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string latest = NormalizeVersion(root.GetProperty("tag_name").GetString());
                string notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
                string url = FindZipAssetUrl(root) ?? "";

                return new UpdateCheckResult
                {
                    LocalVersion = local,
                    LatestVersion = latest,
                    UpdateAvailable = CompareVersions(latest, local) > 0 && !string.IsNullOrWhiteSpace(url),
                    DownloadUrl = url,
                    ReleaseNotes = notes
                };
            }
            catch
            {
                return new UpdateCheckResult { LocalVersion = local, LatestVersion = local };
            }
        }

        public static string GetLocalVersion()
        {
            string dll = Path.Combine(AppContext.BaseDirectory, "OsintPro.UI.dll");
            string exe = Path.Combine(AppContext.BaseDirectory, "OsintPro.UI.exe");
            string target = File.Exists(dll) ? dll : exe;
            if (!File.Exists(target)) return "0.0.0";
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(target);
            return NormalizeVersion(info.FileVersion ?? info.ProductVersion ?? "0.0.0");
        }

        public static string NormalizeVersion(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "0.0.0";
            raw = raw.Trim().TrimStart('v', 'V');
            var match = Regex.Match(raw, @"(\d+)\.(\d+)\.(\d+)");
            return match.Success
                ? $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}"
                : "0.0.0";
        }

        public static int CompareVersions(string left, string right) =>
            Version.Parse(NormalizeVersion(left)).CompareTo(Version.Parse(NormalizeVersion(right)));

        private static string FindZipAssetUrl(JsonElement root)
        {
            if (!root.TryGetProperty("assets", out var assets)) return null;
            string fallback = null;
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("browser_download_url", out var urlEl)) continue;
                string url = urlEl.GetString();
                string name = asset.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
                if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                if (name.Contains("Justin", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Osint", StringComparison.OrdinalIgnoreCase))
                    return url;
                fallback ??= url;
            }
            return fallback;
        }
    }
}