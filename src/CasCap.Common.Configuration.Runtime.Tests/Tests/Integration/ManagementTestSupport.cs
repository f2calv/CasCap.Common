using CasCap.Common.Configuration.Management.Contracts.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

internal static class ManagementTestSupport
{
    public static string Route(string environment) => $"/api/runtime-configuration/test-application/{environment}/controls";

    public static RuntimeConfigurationHostFactory Factory(
        string connectionString,
        string environment,
        string backend = "Sqlite",
        string? keyDirectory = null,
        Action<IServiceCollection>? configure = null,
        bool publishPolicy = true,
        TimeProvider? timeProvider = null
    ) => new(connectionString, environment, timeProvider, configureServices: services =>
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        if (keyDirectory is not null)
            services.AddDataProtection().SetApplicationName("management-tests").PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        services.AddAuthorization(options =>
        {
            options.AddPolicy(RuntimeConfigurationManagementPolicies.View, policy => policy.RequireAssertion(context =>
                context.Resource is RuntimeConfigurationDestination { Application: "test-application", Contract: "controls" } destination
                && destination.Environment == environment && context.User.HasClaim("permission", "view")));
            options.AddPolicy(RuntimeConfigurationManagementPolicies.Edit, policy => policy.RequireClaim("permission", "edit"));
            if (publishPolicy)
                options.AddPolicy(RuntimeConfigurationManagementPolicies.Publish, policy => policy.RequireClaim("permission", "publish"));
        });
        services.AddSingleton<IStartupFilter, TestCookieSignIn>();
        configure?.Invoke(services);
    }, backend: backend, managementEnabled: true);

    public static HttpClient Client(RuntimeConfigurationHostFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    public static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string role = "publisher", string actor = "first-user")
    {
        var response = await client.GetAsync($"/test-auth/{role}/{actor}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return response;
    }

    public static async Task<RuntimeConfigurationAntiforgeryResponse> TokenAsync(HttpClient client, string route)
    {
        var token = await client.GetFromJsonAsync<RuntimeConfigurationAntiforgeryResponse>(
            $"{route}/antiforgery", TestContext.Current.CancellationToken);
        Assert.NotNull(token);
        client.DefaultRequestHeaders.Remove(token.HeaderName);
        client.DefaultRequestHeaders.Add(token.HeaderName, token.RequestToken);
        return token;
    }

    public static PublishRuntimeConfigurationRequest<RuntimeControls> Publication(long expected = 1, int verbosity = 4)
        => new()
        {
            Value = new RuntimeControls { Verbosity = verbosity, FeatureEnabled = true, Categories = ["complete"] },
            ExpectedRevision = expected,
            Reason = "Isolated HTTP publication",
        };

    private sealed class TestCookieSignIn : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                var parts = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts is ["test-auth", var role, var actor])
                {
                    var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, actor), new(ClaimTypes.Name, actor) };
                    if (role is "viewer" or "editor" or "publisher")
                        claims.Add(new("permission", "view"));
                    if (role is "editor" or "publisher")
                        claims.Add(new("permission", "edit"));
                    if (role is "publisher")
                        claims.Add(new("permission", "publish"));
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
                    context.Response.StatusCode = 204;
                    return;
                }
                await nextMiddleware(context);
            });
            next(app);
        };
    }
}
