using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

public class BasicAuthorizationHandler : AuthorizationHandler<BasicAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, BasicAuthorizationRequirement requirement)
    {
        var httpContext = (HttpContext)context.Resource;

        // Check if the Authorization header is present
        if (!httpContext.Request.Headers.ContainsKey("Authorization"))
        {
            context.Fail(); // Authorization header is missing
            return Task.CompletedTask;
        }

        // Parse the Authorization header
        var authHeader = AuthenticationHeaderValue.Parse(httpContext.Request.Headers["Authorization"].ToString());
        if (authHeader.Scheme != "Basic" || string.IsNullOrEmpty(authHeader.Parameter))
        {
            context.Fail(); // Authorization header is invalid
            return Task.CompletedTask;
        }

        // Decode the base64-encoded credentials
        var credentialsBytes = Convert.FromBase64String(authHeader.Parameter);
        var credentials = Encoding.UTF8.GetString(credentialsBytes).Split(':', 2);

        if (credentials.Length != 2)
        {
            context.Fail(); // Invalid credentials format
            return Task.CompletedTask;
        }

        var providedUsername = credentials[0];
        var providedPassword = credentials[1];

        // Compare the provided credentials with the expected ones
        if (providedUsername == requirement.Username && providedPassword == requirement.Password)
        {
            context.Succeed(requirement); // Authentication successful
        }
        else
        {
            context.Fail(); // Invalid credentials
        }

        return Task.CompletedTask;
    }
}
