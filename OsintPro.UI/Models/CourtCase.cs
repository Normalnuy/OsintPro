namespace OsintPro.Models
{
    public class CourtCase
    {
        public string CaseNumber { get; set; }
        public string CourtName { get; set; }
        public string Parties { get; set; }
        public string Subject { get; set; }
        public string Status { get; set; }
        public string FullText { get; set; } // Весь сирий текст для модального вікна
    }
}