using System;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public sealed class CacheLookupResult
    {
        public string Result { get; init; } = "";
        public DateTime CachedAtUtc { get; init; }
        public TimeSpan Age => DateTime.UtcNow - CachedAtUtc;
    }

    public static class SearchResultCache
    {
        private static readonly ConcurrentDictionary<string, CacheEntry> Memory = new();
        private static TimeSpan CurrentTtl =>
            TimeSpan.FromHours(Math.Clamp(AppSettings.Current.CacheTtlHours, 1, 168));

        private static readonly string CacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "JustinOSINT",
            "search-cache");

        private sealed class CacheEntry
        {
            public string Result { get; init; } = "";
            public DateTime CachedAtUtc { get; init; }
        }

        private sealed class CacheFileModel
        {
            public string Result { get; set; } = "";
            public DateTime CachedAtUtc { get; set; }
            public bool Encrypted { get; set; }
        }

        static SearchResultCache()
        {
            Directory.CreateDirectory(CacheDirectory);
        }

        public static string BuildKey(SearchModule module, params string[] parts)
        {
            var normalized = string.Join("|",
                parts.Select(p => (p ?? "").Trim().ToLowerInvariant()));
            return $"{module}:{normalized}";
        }

        public static string BuildKey(SearchContext context, SearchModule module, params string[] parts)
        {
            var extra = context == null
                ? ""
                : $"|strict={(context.StrictMatch ? 1 : 0)}|dob={context.Dob}|cache={(context.CacheEnabled ? 1 : 0)}";
            return BuildKey(module, parts) + extra;
        }

        public static bool TryGet(SearchModule module, string cacheKey, out CacheLookupResult lookup)
        {
            lookup = null;

            if (!AppSettings.Current.EnableSearchCache)
                return false;

            if (string.IsNullOrWhiteSpace(cacheKey))
                return false;

            if (Memory.TryGetValue(cacheKey, out var memoryEntry) && !IsExpired(memoryEntry))
            {
                lookup = ToLookup(memoryEntry);
                return true;
            }

            string filePath = GetFilePath(module, cacheKey);
            if (!File.Exists(filePath))
                return false;

            try
            {
                var json = File.ReadAllText(filePath);
                var model = JsonSerializer.Deserialize<CacheFileModel>(json);
                if (model == null || IsExpired(model.CachedAtUtc))
                {
                    TryDelete(filePath);
                    return false;
                }

                string result = model.Result ?? "";
                if (model.Encrypted)
                    DpapiProtector.TryUnprotect(result, out result);

                var entry = new CacheEntry
                {
                    Result = result,
                    CachedAtUtc = model.CachedAtUtc
                };

                Memory[cacheKey] = entry;
                lookup = ToLookup(entry);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Set(SearchModule module, string cacheKey, string result, bool cacheEnabled = true)
        {
            if (!cacheEnabled || !AppSettings.Current.EnableSearchCache)
                return;

            if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(result))
                return;

            if (!ShouldCache(result))
                return;

            var entry = new CacheEntry
            {
                Result = result,
                CachedAtUtc = DateTime.UtcNow
            };

            Memory[cacheKey] = entry;

            try
            {
                var model = new CacheFileModel
                {
                    Result = DpapiProtector.Protect(result),
                    CachedAtUtc = entry.CachedAtUtc,
                    Encrypted = true
                };

                string filePath = GetFilePath(module, cacheKey);
                File.WriteAllText(filePath, JsonSerializer.Serialize(model));
            }
            catch { }
        }

        public static void Invalidate(SearchModule module, string cacheKey)
        {
            if (string.IsNullOrWhiteSpace(cacheKey))
                return;

            Memory.TryRemove(cacheKey, out _);
            TryDelete(GetFilePath(module, cacheKey));
        }

        public static void ClearExpired()
        {
            foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize<CacheFileModel>(json);
                    if (model == null || IsExpired(model.CachedAtUtc))
                        File.Delete(file);
                }
                catch { }
            }

            foreach (var key in Memory.Keys.ToList())
            {
                if (Memory.TryGetValue(key, out var entry) && IsExpired(entry))
                    Memory.TryRemove(key, out _);
            }
        }

        public static void ClearAll()
        {
            Memory.Clear();
            if (!Directory.Exists(CacheDirectory)) return;
            foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*.json"))
                TryDelete(file);
        }

        public static (long bytes, int files) GetStats()
        {
            if (!Directory.Exists(CacheDirectory))
                return (0, 0);

            long bytes = 0;
            int files = 0;
            foreach (var file in Directory.EnumerateFiles(CacheDirectory, "*.json"))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                    files++;
                }
                catch { }
            }
            return (bytes, files);
        }

        public static bool ShouldCache(string result) =>
            SearchResultClassifier.IsCacheableResult(result);

        private static CacheLookupResult ToLookup(CacheEntry entry) => new()
        {
            Result = entry.Result,
            CachedAtUtc = entry.CachedAtUtc
        };

        private static bool IsExpired(CacheEntry entry) => IsExpired(entry.CachedAtUtc);

        private static bool IsExpired(DateTime cachedAtUtc) =>
            DateTime.UtcNow - cachedAtUtc > CurrentTtl;

        private static string GetFilePath(SearchModule module, string cacheKey)
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey))).ToLowerInvariant();
            return Path.Combine(CacheDirectory, $"{module}_{hash}.json");
        }

        private static void TryDelete(string filePath)
        {
            try { File.Delete(filePath); } catch { }
        }
    }
}