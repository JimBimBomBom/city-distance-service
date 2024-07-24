class ApplicationVersionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _appVersion;

    public ApplicationVersionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _appVersion = Constants.Version;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() => {
            context.Response.Headers["Application-Version"] = _appVersion;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}