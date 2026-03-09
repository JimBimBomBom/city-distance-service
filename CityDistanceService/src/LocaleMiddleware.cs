// LocaleMiddleware.cs
public class LocaleMiddleware
{
    private readonly RequestDelegate      _next;
    private readonly ILocalizationService _localization;

    // ILocalizationService is a singleton, safe to inject into middleware directly.
    public LocaleMiddleware(RequestDelegate next, ILocalizationService localization)
    {
        _next         = next;
        _localization = localization;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Priority: 1) ?lang= query param  2) Accept-Language header  3) default
        var raw = context.Request.Query["lang"].FirstOrDefault();

        if (string.IsNullOrEmpty(raw))
        {
            var acceptLang = context.Request.Headers.AcceptLanguage.FirstOrDefault();
            if (!string.IsNullOrEmpty(acceptLang))
            {
                // "cs-CZ,cs;q=0.9,en;q=0.8" → try each tag in order until one resolves
                raw = acceptLang
                    .Split(',')
                    .Select(tag => tag.Split(';')[0].Trim())   // strip q-values
                    .FirstOrDefault(tag => Resolve(tag) != null);
            }
        }

        context.Items["Language"] = raw != null ? Resolve(raw) : null;
        await _next(context);
    }

    /// <summary>
    /// Resolves a client-supplied tag to a supported language code.
    /// Tries exact match first ("en-US"), then base language ("en").
    /// Returns null if neither matches anything in languages.json.
    /// </summary>
    private string? Resolve(string tag)
    {
        var supported = _localization.AvailableLanguages;

        // Exact match: "en-US" → "en-US"
        if (supported.Any(l => l.Code.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            return supported.First(l => l.Code.Equals(tag, StringComparison.OrdinalIgnoreCase)).Code;

        // Base language fallback: "en-GB" → "en"
        var baseLang = tag.Split('-')[0];
        if (supported.Any(l => l.Code.Equals(baseLang, StringComparison.OrdinalIgnoreCase)))
            return supported.First(l => l.Code.Equals(baseLang, StringComparison.OrdinalIgnoreCase)).Code;

        return null;
    }
}

public static class HttpContextExtensions
{
    public static string GetLanguage(this HttpContext context)
        => context.Items["Language"] as string ?? Constants.DefaultLanguage;
}