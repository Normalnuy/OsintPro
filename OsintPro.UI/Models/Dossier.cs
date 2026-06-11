using System;
using System.Collections.ObjectModel;

namespace OsintPro.UI.Models
{
    public class Dossier
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FullName { get; set; }
        public DateTime DateCreated { get; set; } = DateTime.Now;
        public DateTime? LastUpdated { get; set; }
        public string CustomNotes { get; set; } = "";
        public SearchSnapshot SearchSnapshot { get; set; }

        public ObservableCollection<ParsedItem> Security { get; set; } = new ObservableCollection<ParsedItem>();
        public ObservableCollection<ParsedItem> CourtCases { get; set; } = new ObservableCollection<ParsedItem>();
        public ObservableCollection<ParsedItem> Debts { get; set; } = new ObservableCollection<ParsedItem>();
        public ObservableCollection<ParsedItem> Businesses { get; set; } = new ObservableCollection<ParsedItem>();
        public ObservableCollection<ParsedItem> Declarations { get; set; } = new ObservableCollection<ParsedItem>();
        public ObservableCollection<ParsedItem> Market { get; set; } = new ObservableCollection<ParsedItem>();
        public ObservableCollection<ParsedItem> Social { get; set; } = new ObservableCollection<ParsedItem>();
    }

    public class ParsedItem
    {
        public string Title { get; set; }
        public string Details { get; set; }
    }
}