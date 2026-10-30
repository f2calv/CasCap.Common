using CasCap.Common.Configuration.Management.Contracts.Constants;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Blazor.Services;

/// <summary>Calls the same-origin management API with typed values, principal-bound antiforgery and no mutation retries.</summary>
public sealed partial class RuntimeConfigurationClient<T>(
    ILogger<RuntimeConfigurationClient<T>> logger,
    HttpClient httpClient,
    JsonSerializerOptions? serializerOptions = null
) where T : class
{
    private readonly JsonSerializerOptions _json = new(serializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web))
    {
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>Reads the authoritative durable document, not a replica's effective options.</summary>
    public Task<RuntimeConfigurationClientResult<RuntimeConfigurationDocument<T>>> ReadAsync(
        RuntimeConfigurationDestination destination,
        CancellationToken cancellationToken = default
    ) => SendAsync<RuntimeConfigurationDocument<T>>(new HttpRequestMessage(HttpMethod.Get, Path(destination)), cancellationToken);

    /// <summary>Validates an independent typed candidate without publishing it.</summary>
    public Task<RuntimeConfigurationClientResult<PreviewRuntimeConfigurationResponse<T>>> PreviewAsync(
        RuntimeConfigurationDestination destination,
        T value,
        CancellationToken cancellationToken = default
    ) => PostAsync<PreviewRuntimeConfigurationRequest<T>, PreviewRuntimeConfigurationResponse<T>>(
        destination, RuntimeConfigurationManagementRoutes.Preview, new() { Value = value }, cancellationToken);

    /// <summary>Publishes once with the expected revision. Unavailable outcomes must not be blindly retried.</summary>
    public Task<RuntimeConfigurationClientResult<PublishRuntimeConfigurationResponse<T>>> PublishAsync(
        RuntimeConfigurationDestination destination,
        PublishRuntimeConfigurationRequest<T> request,
        CancellationToken cancellationToken = default
    ) => PostAsync<PublishRuntimeConfigurationRequest<T>, PublishRuntimeConfigurationResponse<T>>(
        destination, RuntimeConfigurationManagementRoutes.Publish, request, cancellationToken);

    private async Task<RuntimeConfigurationClientResult<TResponse>> PostAsync<TRequest, TResponse>(
        RuntimeConfigurationDestination destination,
        string suffix,
        TRequest value,
        CancellationToken cancellationToken
    ) where TResponse : class
    {
        var token = await SendAsync<RuntimeConfigurationAntiforgeryResponse>(
            new HttpRequestMessage(HttpMethod.Get, $"{Path(destination)}/{RuntimeConfigurationManagementRoutes.Antiforgery}"), cancellationToken);
        if (!token.Succeeded)
            return new() { Status = token.Status, Message = token.Message, Errors = token.Errors };
        if (string.IsNullOrWhiteSpace(token.Value!.HeaderName) || string.IsNullOrWhiteSpace(token.Value.RequestToken))
        {
            LogClientFailure(logger, "InvalidAntiforgeryResponse");
            return Failure<TResponse>(RuntimeConfigurationClientStatus.InvalidResponse);
        }
        var request = new HttpRequestMessage(HttpMethod.Post, $"{Path(destination)}/{suffix}")
        {
            Content = JsonContent.Create(value, options: _json),
        };
        if (!request.Headers.TryAddWithoutValidation(token.Value.HeaderName, token.Value.RequestToken))
        {
            request.Dispose();
            LogClientFailure(logger, "InvalidAntiforgeryHeader");
            return Failure<TResponse>(RuntimeConfigurationClientStatus.InvalidResponse);
        }
        return await SendAsync<TResponse>(request, cancellationToken);
    }

    private async Task<RuntimeConfigurationClientResult<TResponse>> SendAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) where TResponse : class
    {
        using (request)
        {
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken: cancellationToken);
                    var value = body.RootElement.Deserialize<TResponse>(_json);
                    var acknowledgement = value is not PublishRuntimeConfigurationResponse<T>
                        || TrueProperty(body.RootElement, nameof(PublishRuntimeConfigurationResponse<T>.Committed))
                        && TrueProperty(body.RootElement, nameof(PublishRuntimeConfigurationResponse<T>.ActivationPending));
                    if (value is not null && ValidResponse(value) && acknowledgement)
                        return new() { Status = RuntimeConfigurationClientStatus.Success, Value = value, Message = "Completed." };
                    LogClientFailure(logger, "InvalidSuccessResponse");
                    return Failure<TResponse>(RuntimeConfigurationClientStatus.InvalidResponse);
                }
                var status = response.StatusCode switch
                {
                    HttpStatusCode.BadRequest => RuntimeConfigurationClientStatus.Validation,
                    HttpStatusCode.Unauthorized => RuntimeConfigurationClientStatus.Unauthorized,
                    HttpStatusCode.Forbidden => RuntimeConfigurationClientStatus.Forbidden,
                    HttpStatusCode.NotFound => RuntimeConfigurationClientStatus.NotFound,
                    HttpStatusCode.Conflict => RuntimeConfigurationClientStatus.Conflict,
                    HttpStatusCode.ServiceUnavailable => RuntimeConfigurationClientStatus.Unavailable,
                    _ => RuntimeConfigurationClientStatus.InvalidResponse,
                };
                IReadOnlyDictionary<string, string[]> errors = new Dictionary<string, string[]>();
                if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
                {
                    // Problems allow extension members; only validation fields are consumed.
                    using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                        cancellationToken: cancellationToken);
                    if (problem.RootElement.TryGetProperty("errors", out var fields))
                        errors = fields.Deserialize<Dictionary<string, string[]>>(_json) ?? [];
                }
                return Failure<TResponse>(status) with { Errors = errors };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                LogClientFailure(logger, "TransportTimeout");
                return Failure<TResponse>(RuntimeConfigurationClientStatus.Unavailable);
            }
            catch (HttpRequestException exception)
            {
                LogClientFailure(logger, exception.GetType().Name);
                return Failure<TResponse>(RuntimeConfigurationClientStatus.Unavailable);
            }
            catch (JsonException exception)
            {
                LogClientFailure(logger, exception.GetType().Name);
                return Failure<TResponse>(RuntimeConfigurationClientStatus.InvalidResponse);
            }
        }
    }

    private static bool ValidResponse<TResponse>(TResponse value) => value switch
    {
        RuntimeConfigurationDocument<T> document => document.Revision > 0 && document.Value is not null,
        PreviewRuntimeConfigurationResponse<T> preview => preview.Value is not null,
        PublishRuntimeConfigurationResponse<T> publication => publication.Document is { Revision: > 0, Value: not null },
        _ => true,
    };

    private bool TrueProperty(JsonElement document, string name) =>
        document.TryGetProperty(_json.PropertyNamingPolicy?.ConvertName(name) ?? name, out var value)
        && value.ValueKind == JsonValueKind.True;

    private static string Path(RuntimeConfigurationDestination destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination.Application);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination.Environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination.Contract);
        return "/" + RuntimeConfigurationManagementRoutes.Destination
            .Replace("{application}", Uri.EscapeDataString(destination.Application), StringComparison.Ordinal)
            .Replace("{environment}", Uri.EscapeDataString(destination.Environment), StringComparison.Ordinal)
            .Replace("{contract}", Uri.EscapeDataString(destination.Contract), StringComparison.Ordinal);
    }

    private static RuntimeConfigurationClientResult<TResponse> Failure<TResponse>(string status) where TResponse : class => new()
    {
        Status = status,
        Message = status switch
        {
            RuntimeConfigurationClientStatus.Validation => "Validation failed. Correct the fields or refresh your sign-in token.",
            RuntimeConfigurationClientStatus.Unauthorized => "Sign in through the host application.",
            RuntimeConfigurationClientStatus.Forbidden => "You do not have permission for this operation.",
            RuntimeConfigurationClientStatus.NotFound => "No document is available for this destination.",
            RuntimeConfigurationClientStatus.Conflict => "The document changed. Reload and review before publishing again.",
            RuntimeConfigurationClientStatus.Unavailable => "The service is unavailable. A publication outcome may be uncertain; do not retry blindly.",
            _ => "The server returned an unexpected response. No activation is confirmed.",
        },
    };

    [LoggerMessage(LogLevel.Warning, "Runtime configuration HTTP operation failed ({FailureType})")]
    private static partial void LogClientFailure(ILogger logger, string failureType);
}
