using System.Globalization;
using Microsoft.AspNetCore.Localization;

namespace Himma.Api.Infrastructure.Localization;

/// <summary>
/// Arabic / English text. The language of a request comes from its Accept-Language header
/// (the frontend sends the language the user picked); Arabic is the default.
/// Every user-facing message is written as Lang.T("عربي", "English") right where it is used,
/// so both versions stay side by side and none can be forgotten.
/// </summary>
public static class Lang
{
    public const string Arabic = "ar";
    public const string English = "en";

    public static bool IsEnglish => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == English;

    public static string T(string arabic, string english) => IsEnglish ? english : arabic;

    /// <summary>The English variant of seeded data when it exists, otherwise the original (Arabic) value.</summary>
    public static string Pick(string arabic, string? english) =>
        IsEnglish && !string.IsNullOrWhiteSpace(english) ? english : arabic;

    public static string? PickOptional(string? arabic, string? english) =>
        IsEnglish && !string.IsNullOrWhiteSpace(english) ? english : arabic;

    public static IApplicationBuilder UseHimmaLocalization(this IApplicationBuilder app)
    {
        var cultures = new[] { new CultureInfo(Arabic), new CultureInfo(English) };
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(Arabic),
            SupportedCultures = cultures,
            SupportedUICultures = cultures,
            FallBackToParentUICultures = true,
            FallBackToParentCultures = true,
        };
        // Only the header decides: no query string or cookie switching.
        options.RequestCultureProviders = [new AcceptLanguageHeaderRequestCultureProvider()];
        return app.UseRequestLocalization(options);
    }
}
