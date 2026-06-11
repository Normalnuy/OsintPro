using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OsintPro.UI.Models;

namespace OsintPro.UI.Services
{
    public class ArchiveManager
    {
        private readonly string _archiveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "JustinOSINT",
            "Archives");

        public ArchiveManager()
        {
            if (!Directory.Exists(_archiveFolder))
                Directory.CreateDirectory(_archiveFolder);
            MigrateLegacyFiles();
        }

        public void SaveDossier(Dossier dossier)
        {
            if (string.IsNullOrWhiteSpace(dossier.Id))
                dossier.Id = Guid.NewGuid().ToString();

            dossier.DateCreated = dossier.DateCreated == default ? DateTime.Now : dossier.DateCreated;
            dossier.LastUpdated = DateTime.Now;

            string filePath = GetFilePathForId(dossier.Id);
            string json = JsonConvert.SerializeObject(dossier, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        public List<Dossier> GetAllDossiers()
        {
            var list = new List<Dossier>();
            if (!Directory.Exists(_archiveFolder)) return list;

            foreach (var file in Directory.GetFiles(_archiveFolder, "*.json"))
            {
                var dossier = TryLoad(file);
                if (dossier != null) list.Add(dossier);
            }
            return list;
        }

        public List<Dossier> SearchDossiers(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAllDossiers().OrderByDescending(d => d.LastUpdated ?? d.DateCreated).ToList();

            string normalized = query.Trim().ToLowerInvariant();
            return GetAllDossiers()
                .Where(d => MatchesQuery(d, normalized))
                .OrderByDescending(d => d.LastUpdated ?? d.DateCreated)
                .ToList();
        }

        public Dossier GetById(string id)
        {
            string path = GetFilePathForId(id);
            return File.Exists(path) ? TryLoad(path) : null;
        }

        public bool DeleteDossier(string id)
        {
            string path = GetFilePathForId(id);
            if (!File.Exists(path)) return false;
            try
            {
                File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        private static bool MatchesQuery(Dossier d, string normalized)
        {
            if ((d.FullName ?? "").ToLowerInvariant().Contains(normalized)) return true;
            if ((d.CustomNotes ?? "").ToLowerInvariant().Contains(normalized)) return true;
            if ((d.Id ?? "").ToLowerInvariant().Contains(normalized)) return true;

            var snap = d.SearchSnapshot;
            if (snap != null)
            {
                if ((snap.Inn ?? "").ToLowerInvariant().Contains(normalized)) return true;
                if ((snap.Contact ?? "").ToLowerInvariant().Contains(normalized)) return true;
                if ((snap.Nickname ?? "").ToLowerInvariant().Contains(normalized)) return true;
                if ((snap.LastName ?? "").ToLowerInvariant().Contains(normalized)) return true;
                if ((snap.FirstName ?? "").ToLowerInvariant().Contains(normalized)) return true;
            }
            return false;
        }

        private string GetFilePathForId(string id) =>
            Path.Combine(_archiveFolder, $"{SanitizeId(id)}.json");

        private static string SanitizeId(string id)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                id = id.Replace(c, '_');
            return id;
        }

        private static Dossier TryLoad(string file)
        {
            try
            {
                string json = File.ReadAllText(file);
                return JsonConvert.DeserializeObject<Dossier>(json);
            }
            catch { return null; }
        }

        private void MigrateLegacyFiles()
        {
            foreach (var file in Directory.GetFiles(_archiveFolder, "*.json"))
            {
                try
                {
                    var dossier = TryLoad(file);
                    if (dossier == null || string.IsNullOrWhiteSpace(dossier.Id)) continue;

                    string target = GetFilePathForId(dossier.Id);
                    if (string.Equals(file, target, StringComparison.OrdinalIgnoreCase)) continue;

                    if (!File.Exists(target))
                        File.WriteAllText(target, JsonConvert.SerializeObject(dossier, Formatting.Indented));

                    File.Delete(file);
                }
                catch { }
            }
        }
    }
}