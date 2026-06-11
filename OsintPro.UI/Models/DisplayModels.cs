namespace OsintPro.UI.Models
{
    public class CourtCaseDisplay
    {
        public string CaseNumber { get; set; } = "";
        public string CourtName { get; set; } = "";
        public string Status { get; set; } = "";
        public string FullText { get; set; } = "";
        public string CardColor { get; set; } = "";
        public bool IsExactMatch { get; set; }
    }

    public class GenericRecordDisplay
    {
        public string Title { get; set; } = "";
        public string Subtitle1 { get; set; } = "";
        public string Subtitle2 { get; set; } = "";
        public string FullDetails { get; set; } = "";
        public string CardColor { get; set; } = "";
        public bool IsExactMatch { get; set; }
        public string DedupKey { get; set; } = "";
    }
}