using OsintPro.UI.Services;

namespace OsintPro.UI.Models
{
    public sealed class SearchSnapshot
    {
        public string LastName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Patronymic { get; set; } = "";
        public string Inn { get; set; } = "";
        public string Nickname { get; set; } = "";
        public string Dob { get; set; } = "";
        public string Contact { get; set; } = "";
        public bool StrictMatch { get; set; }
        public bool OnlyExactResults { get; set; }
        public bool CacheEnabled { get; set; } = true;

        public static SearchSnapshot FromContext(SearchContext context) => context == null
            ? new SearchSnapshot()
            : new SearchSnapshot
            {
                LastName = context.LastName,
                FirstName = context.FirstName,
                Patronymic = context.Patronymic,
                Inn = context.Inn,
                Nickname = context.Nickname,
                Dob = context.Dob,
                Contact = context.Contact,
                StrictMatch = context.StrictMatch,
                OnlyExactResults = context.OnlyExactResults,
                CacheEnabled = context.CacheEnabled
            };

        public SearchContext ToContext() =>
            SearchContext.FromInputs(
                LastName, FirstName, Patronymic, Inn, Nickname, Dob, Contact,
                StrictMatch, OnlyExactResults, CacheEnabled);
    }
}