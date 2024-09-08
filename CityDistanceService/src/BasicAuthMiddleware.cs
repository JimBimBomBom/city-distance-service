using Microsoft.AspNetCore.Authorization;

public class BasicAuthorizationRequirement : IAuthorizationRequirement
{
    public string Username { get; }
    public string Password { get; }

    public BasicAuthorizationRequirement(string username, string password)
    {
        Username = username;
        Password = password;
    }
}
