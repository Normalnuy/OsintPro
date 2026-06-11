namespace OsintPro.UI.Services
{
    public readonly record struct SearchMatchOptions(bool Strict, string Dob = "")
    {
        public static SearchMatchOptions Soft { get; } = new(false, "");
    }
}