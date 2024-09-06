using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly string _auth_username;
    private readonly string _auth_password;

    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IConfiguration configuration)
        : base(options, logger, encoder, clock)
    {
        _auth_username = configuration["AUTH_USERNAME"];
        _auth_password = configuration["AUTH_PASSWORD"];
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
        {
            return AuthenticateResult.Fail("Authorization header missing");
        }

        try
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var authHeaderVal = AuthenticationHeaderValue.Parse(authHeader);

            if (authHeaderVal.Scheme.Equals("basic", StringComparison.OrdinalIgnoreCase) &&
                authHeaderVal.Parameter != null)
            {
                var credentials = Encoding.UTF8
                    .GetString(Convert.FromBase64String(authHeaderVal.Parameter))
                    .Split(':', 2);

                var username = _auth_username;
                var password = _auth_password;

                if (credentials[0] == username && credentials[1] == password)
                {
                    var claims = new[] { new Claim(ClaimTypes.Name, credentials[0]) };
                    var identity = new ClaimsIdentity(claims, Scheme.Name);
                    var principal = new ClaimsPrincipal(identity);
                    var ticket = new AuthenticationTicket(principal, Scheme.Name);

                    return AuthenticateResult.Success(ticket);
                }
            }

            return AuthenticateResult.Fail("Invalid Authorization Header");
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }
    }
}
