namespace OsintPro.Models
{
    public class BusinessRecord
    {
        public string Title { get; set; }        // Назва: "Директор", "Власник", "ФОП"
        public string Organization { get; set; } // Компанія: "ТОВ «ГАРТ»"
        public string Region { get; set; }       // Область
        public string FullDetails { get; set; }  // Весь текст для Popup
    }
}