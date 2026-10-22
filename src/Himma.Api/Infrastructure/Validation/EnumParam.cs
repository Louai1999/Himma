namespace Himma.Api.Infrastructure.Validation;

/// <summary>
/// Parses enum names from URLs and query strings. Unlike the default binder it rejects numbers,
/// combined values ("a,b") and names that do not exist.
/// </summary>
public static class EnumParam
{
    public static bool TryParse<TEnum>(string? value, out TEnum result) where TEnum : struct, Enum
    {
        result = default;
        return !string.IsNullOrWhiteSpace(value)
            && char.IsLetter(value[0])
            && !value.Contains(',')
            && Enum.TryParse(value, ignoreCase: true, out result)
            && Enum.IsDefined(result);
    }
}
