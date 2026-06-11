using OsintPro.UI.Services;

namespace OsintPro.UI.Models
{
    public sealed class SearchContext
    {
        public string LastName { get; init; } = "";
        public string FirstName { get; init; } = "";
        public string Patronymic { get; init; } = "";
        public string Inn { get; init; } = "";
        public string Nickname { get; init; } = "";
        public string Dob { get; init; } = "";
        public string Contact { get; init; } = "";
        public bool StrictMatch { get; init; }
        public bool OnlyExactResults { get; init; }
        public bool CacheEnabled { get; init; } = true;

        public bool HasFio =>
            !string.IsNullOrWhiteSpace(LastName) && !string.IsNullOrWhiteSpace(FirstName);

        public bool HasInn => InnValidator.IsValid(Inn);

        public bool HasNickname => !string.IsNullOrWhiteSpace(Nickname);

        public bool HasContact => !string.IsNullOrWhiteSpace(Contact);

        public bool HasDob => DobHelper.IsValid(Dob);

        public string FioFull => HasFio
            ? $"{LastName} {FirstName} {Patronymic}".Trim()
            : "";

        public string IdQuery => HasInn ? Inn.Trim() : FioFull;

        public string SocialQuery => HasNickname ? $"@{Nickname.Trim()}" : FioFull;

        public string BusinessQuery => HasInn ? Inn.Trim() : FioFull;

        public bool HasBusiness => HasInn || HasFio;

        public bool HasDebtsDecl => !string.IsNullOrWhiteSpace(IdQuery);

        public bool HasFootprint => HasContact || HasFio;

        public bool HasSocial => !string.IsNullOrWhiteSpace(SocialQuery) || HasFio;

        public bool HasAnyInput => HasFio || HasInn || HasNickname || HasContact;

        public string DossierTitle =>
            !string.IsNullOrWhiteSpace(FioFull) ? FioFull
            : !string.IsNullOrWhiteSpace(Inn) ? Inn.Trim()
            : !string.IsNullOrWhiteSpace(Nickname) ? Nickname.Trim()
            : Contact.Trim();

        public SearchMatchOptions MatchOptions => new(StrictMatch, Dob);

        public static SearchContext FromInputs(
            string lastName,
            string firstName,
            string patronymic,
            string inn,
            string nickname,
            string dob,
            string contact,
            bool strictMatch,
            bool onlyExactResults,
            bool cacheEnabled) => new()
        {
            LastName = lastName?.Trim() ?? "",
            FirstName = firstName?.Trim() ?? "",
            Patronymic = patronymic?.Trim() ?? "",
            Inn = inn?.Trim() ?? "",
            Nickname = nickname?.Trim() ?? "",
            Dob = dob?.Trim() ?? "",
            Contact = contact?.Trim() ?? "",
            StrictMatch = strictMatch,
            OnlyExactResults = onlyExactResults,
            CacheEnabled = cacheEnabled
        };
    }
}