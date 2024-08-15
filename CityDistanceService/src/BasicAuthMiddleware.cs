using System.Text;

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    // private readonly List<string> _exemptedPaths;

    public BasicAuthMiddleware(RequestDelegate next)//, List<string> exemptedPaths)
    {
        _next = next;
        // _exemptedPaths = exemptedPaths;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // if (_exemptedPaths.Contains(context.Request.Path))
        // {
        //     await _next(context);
        //     return;
        // }

        if (!context.Request.Headers.ContainsKey("Authorization"))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("Authorization header missing");
            return;
        }

        var authHeader = context.Request.Headers["Authorization"].ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader.Substring("Basic ".Length).Trim();
            var credentialString = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var credentials = credentialString.Split(':');

            var username = Environment.GetEnvironmentVariable("AUTH_USERNAME");
            var password = Environment.GetEnvironmentVariable("AUTH_PASSWORD");

            if (credentials[0] == username && credentials[1] == password)
            {
                await _next(context);
                return;
            }
        }

        context.Response.StatusCode = 401;
        await context.Response.WriteAsync("Unauthorized");
    }
}
