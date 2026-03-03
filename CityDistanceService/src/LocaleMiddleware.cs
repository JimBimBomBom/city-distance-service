public class LocaleMiddleware
{
    private readonly RequestDelegate _next;

    public LocaleMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Priority: 1) ?lang= query param  2) Accept-Language header  3) default
        var lang = context.Request.Query["lang"].FirstOrDefault();

        if (string.IsNullOrEmpty(lang))
        {
            var acceptLang = context.Request.Headers.AcceptLanguage.FirstOrDefault();
            if (!string.IsNullOrEmpty(acceptLang))
            {
                // Parse: "cs-CZ,cs;q=0.9,en;q=0.8" → "cs"
                lang = acceptLang.Split(',')[0].Split('-')[0].Split(';')[0].Trim().ToLower();
            }
        }

        context.Items["Language"] = lang;
        await _next(context);
    }
}

public static class HttpContextExtensions
{
    public static string GetLanguage(this HttpContext context)
    {
        return context.Items["Language"] as string ?? Constants.DefaultLanguage;
    }
}
