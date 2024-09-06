using System.Text;
using Microsoft.Extensions.Configuration;

public class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _auth_username;
    private readonly string _auth_password;

    public BasicAuthMiddleware(IConfiguration configuration, RequestDelegate next)
    {
        _auth_username = configuration["AUTH_USERNAME"];
        _auth_password = configuration["AUTH_PASSWORD"];
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
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

            var username = _auth_username;
            var password = _auth_password;

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