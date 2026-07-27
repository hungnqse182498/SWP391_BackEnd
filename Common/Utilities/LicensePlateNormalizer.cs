using System.Text.RegularExpressions;

namespace Common.Utilities;

public static partial class LicensePlateNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        return NonAlphaNumericRegex()
            .Replace(value.Trim().ToUpperInvariant(), string.Empty);
    }

    public static bool IsValid(string? value)
    {
        return CanonicalPlateRegex().IsMatch(Normalize(value));
    }

    [GeneratedRegex("[^A-Z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex("^[A-Z0-9]{4,15}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalPlateRegex();
}
