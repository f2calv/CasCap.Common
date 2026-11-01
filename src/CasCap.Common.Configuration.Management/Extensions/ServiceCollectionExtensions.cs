using CasCap.Common.Configuration.Management.Abstractions;
using CasCap.Common.Configuration.Management.Services;
using Microsoft.AspNetCore.Antiforgery;

namespace CasCap.Common.Configuration.Management.Extensions;

/// <summary>Opts one reader host into management without changing authentication schemes or default policies.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the fixed-scope controller for T. The host must register the authority, authentication,
    /// all three resource-aware policies, call UseAuthentication, and map controllers.
    /// </summary>
    public static IServiceCollection AddRuntimeConfigurationManagement<T>(this IServiceCollection services) where T : class
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(RuntimeConfigurationManagementJson)))
            throw new InvalidOperationException("Only one management contract is supported per host.");
        services.AddAuthorization();
        services.AddAntiforgery();
        services.AddOptions<AntiforgeryOptions>()
            .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "Management requires an antiforgery request header.")
            .ValidateOnStart();
        services.AddScoped<IRuntimeConfigurationManagement<T>, RuntimeConfigurationManagementService<T>>();
        services.AddSingleton<RuntimeConfigurationManagementJson>();
        services.AddScoped<RuntimeConfigurationManagementFilter>();
        services.AddControllers(options => options.Conventions.Add(new RuntimeConfigurationManagementControllerConvention<T>()))
            .ConfigureApplicationPartManager(manager =>
                manager.FeatureProviders.Add(new RuntimeConfigurationManagementControllerFeatureProvider<T>()));
        services.Configure<ApiBehaviorOptions>(options =>
        {
            var previous = options.InvalidModelStateResponseFactory;
            options.InvalidModelStateResponseFactory = context =>
                context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor descriptor
                    && descriptor.ControllerTypeInfo.AsType() == typeof(Controllers.RuntimeConfigurationManagementController<T>)
                    ? new BadRequestObjectResult(new ValidationProblemDetails(new Dictionary<string, string[]>
                    {
                        ["request"] = ["A complete, correctly shaped JSON request is required."],
                    })
                    {
                        Status = 400,
                        Title = "Request validation failed.",
                    })
                    {
                        ContentTypes = { "application/problem+json" },
                    }
                    : previous(context);
        });
        return services;
    }
}
