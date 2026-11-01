using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CasCap.Common.Configuration.Management.Services;

internal sealed class RuntimeConfigurationManagementFilter(
    RuntimeConfigurationOptions settings,
    IAuthorizationPolicyProvider authorizationPolicyProvider,
    IAuthorizationService authorizationService,
    IPolicyEvaluator policyEvaluator,
    IAntiforgery antiforgery
) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        http.Response.Headers.CacheControl = "no-store";
        http.Response.Headers.Pragma = "no-cache";
        var destination = new RuntimeConfigurationDestination(settings.Scope.Application, settings.Scope.Environment, settings.ContractId);
        var operation = context.ActionDescriptor.RouteValues["action"];
        var policies = operation switch
        {
            nameof(Controllers.RuntimeConfigurationManagementController<object>.PublishAsync) =>
                new[] { RuntimeConfigurationManagementPolicies.View, RuntimeConfigurationManagementPolicies.Edit, RuntimeConfigurationManagementPolicies.Publish },
            nameof(Controllers.RuntimeConfigurationManagementController<object>.Preview) =>
                new[] { RuntimeConfigurationManagementPolicies.View, RuntimeConfigurationManagementPolicies.Edit },
            _ => [RuntimeConfigurationManagementPolicies.View],
        };
        var resolvedPolicies = new List<AuthorizationPolicy>();
        foreach (var name in policies)
        {
            var policy = await authorizationPolicyProvider.GetPolicyAsync(name);
            if (policy is null)
            {
                context.Result = Failure(403);
                return;
            }
            resolvedPolicies.Add(policy);
        }
        var combined = AuthorizationPolicy.Combine(resolvedPolicies);
        await policyEvaluator.AuthenticateAsync(combined, http);
        if (!http.User.Identities.Any(identity => identity.IsAuthenticated))
        {
            context.Result = Failure(401);
            return;
        }
        if (!Matches(context, "application", destination.Application)
            || !Matches(context, "environment", destination.Environment)
            || !Matches(context, "contract", destination.Contract))
        {
            context.Result = Failure(404);
            return;
        }
        if (!(await authorizationService.AuthorizeAsync(http.User, destination, combined)).Succeeded)
        {
            context.Result = Failure(403);
            return;
        }
        if (HttpMethods.IsPost(http.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = new ObjectResult(new ValidationProblemDetails(
                    new Dictionary<string, string[]> { ["antiforgery"] = ["Obtain a fresh antiforgery token for the current signed-in user."] })
                {
                    Status = 400,
                    Title = "Request validation failed.",
                })
                {
                    StatusCode = 400,
                    ContentTypes = { "application/problem+json" },
                };
                return;
            }
        }
        await next();
    }

    private static bool Matches(ResourceExecutingContext context, string key, string expected)
        => string.Equals(context.RouteData.Values[key]?.ToString(), expected, StringComparison.Ordinal);

    private static ObjectResult Failure(int status)
        => new(new ProblemDetails { Status = status, Title = ReasonPhrases(status) })
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };

    private static string ReasonPhrases(int status) => status switch
    {
        401 => "Authentication required.",
        403 => "Operation not permitted.",
        _ => "Configuration destination not found.",
    };
}
