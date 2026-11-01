using CasCap.Common.Configuration.Management.Abstractions;
using CasCap.Common.Configuration.Management.Attributes;
using CasCap.Common.Configuration.Management.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Routing;

namespace CasCap.Common.Configuration.Management.Controllers;

/// <summary>Optional typed management operations for one host-owned contract and destination.</summary>
[ApiController]
[Route(RuntimeConfigurationManagementRoutes.Destination)]
[ServiceFilter(typeof(RuntimeConfigurationManagementFilter))]
[ProducesResponseType<ValidationProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(404)]
[ProducesResponseType<ProblemDetails>(409)]
[ProducesResponseType<ProblemDetails>(503)]
public sealed class RuntimeConfigurationManagementController<T>(
    IRuntimeConfigurationManagement<T> management,
    IAntiforgery antiforgery,
    IOptions<JsonOptions> config
) : ControllerBase where T : class
{
    /// <inheritdoc cref="IRuntimeConfigurationManagement{T}.ReadAsync"/>
    [HttpGet]
    [EndpointName("RuntimeConfiguration.Read")]
    [EndpointSummary("Read the authoritative current document, without audit or activation internals.")]
    public async Task<Results<JsonHttpResult<RuntimeConfigurationDocument<T>>, ProblemHttpResult, ValidationProblem>> ReadAsync(CancellationToken cancellationToken)
        => Map(await management.ReadAsync(cancellationToken));

    /// <summary>Issues a principal-bound token and paired HttpOnly cookie for same-origin mutations.</summary>
    [HttpGet(RuntimeConfigurationManagementRoutes.Antiforgery)]
    [EndpointName("RuntimeConfiguration.Antiforgery")]
    [EndpointSummary("Obtain the antiforgery header name and token for the signed-in user.")]
    public JsonHttpResult<RuntimeConfigurationAntiforgeryResponse> Antiforgery(CancellationToken cancellationToken)
        => TypedResults.Json(Token(), config.Value.JsonSerializerOptions);

    /// <inheritdoc cref="IRuntimeConfigurationManagement{T}.Preview"/>
    [HttpPost(RuntimeConfigurationManagementRoutes.Preview)]
    [EndpointName("RuntimeConfiguration.Preview")]
    [EndpointSummary("Validate a complete typed candidate without changing durable or runtime state.")]
    public Results<JsonHttpResult<PreviewRuntimeConfigurationResponse<T>>, ProblemHttpResult, ValidationProblem> Preview(
        [RuntimeConfigurationManagementBody] PreviewRuntimeConfigurationRequest<T> request,
        CancellationToken cancellationToken
    ) => Map(management.Preview(request));

    /// <inheritdoc cref="IRuntimeConfigurationManagement{T}.PublishAsync"/>
    [HttpPost(RuntimeConfigurationManagementRoutes.Publish)]
    [EndpointName("RuntimeConfiguration.Publish")]
    [ActionName(nameof(PublishAsync))]
    [EndpointSummary("Commit once against the expected durable revision; reader activation remains pending.")]
    public async Task<Results<JsonHttpResult<PublishRuntimeConfigurationResponse<T>>, ProblemHttpResult, ValidationProblem>> PublishAsync(
        [RuntimeConfigurationManagementBody] PublishRuntimeConfigurationRequest<T> request,
        CancellationToken cancellationToken
    ) => Map(await management.PublishAsync(request, User, cancellationToken));

    private RuntimeConfigurationAntiforgeryResponse Token()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return new(tokens.HeaderName!, tokens.RequestToken!);
    }

    private Results<JsonHttpResult<TResponse>, ProblemHttpResult, ValidationProblem> Map<TResponse>(RuntimeConfigurationManagementResult<TResponse> result)
    {
        if (result.Value is not null)
            return TypedResults.Json(result.Value, config.Value.JsonSerializerOptions);
        if (result.Field is { } field)
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { [field] = ["The supplied value is invalid."] });
        return TypedResults.Problem(statusCode: result.Status, title: result.Status switch
        {
            401 => "Authentication required.",
            403 => "Operation not permitted.",
            404 => "No durable configuration document exists.",
            409 => "The durable revision changed. Reload before deciding whether to publish again.",
            _ => "Configuration authority unavailable. The publication outcome may be uncertain; do not retry automatically.",
        });
    }
}
