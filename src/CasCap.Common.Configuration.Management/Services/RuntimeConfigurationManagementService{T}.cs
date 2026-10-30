using CasCap.Common.Configuration.Management.Abstractions;
using CasCap.Common.Configuration.Runtime.Abstractions;
using CasCap.Common.Configuration.Runtime.Services;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace CasCap.Common.Configuration.Management.Services;

internal sealed class RuntimeConfigurationManagementService<T>(
    ILogger<RuntimeConfigurationManagementService<T>> logger,
    TimeProvider timeProvider,
    IRuntimeConfigurationPublisher<T> publisher,
    RuntimeConfigurationCodec<T> codec
) : IRuntimeConfigurationManagement<T> where T : class
{
    public async Task<RuntimeConfigurationManagementResult<RuntimeConfigurationDocument<T>>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var revision = await publisher.ReadAsync(cancellationToken);
            return revision is null ? new(null, 404) : new(Document(revision));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogUnavailable(exception);
            return new(null, 503);
        }
    }

    public RuntimeConfigurationManagementResult<PreviewRuntimeConfigurationResponse<T>> Preview(PreviewRuntimeConfigurationRequest<T> request)
    {
        try
        {
            return new(new(codec.Deserialize(codec.Serialize(request.Value))));
        }
        catch (Exception exception) when (exception is ValidationException or OptionsValidationException or JsonException or ArgumentNullException)
        {
            return new(null, 400, "value");
        }
        catch (Exception exception)
        {
            LogUnavailable(exception);
            return new(null, 503);
        }
    }

    public async Task<RuntimeConfigurationManagementResult<PublishRuntimeConfigurationResponse<T>>> PublishAsync(
        PublishRuntimeConfigurationRequest<T> request,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken
    )
    {
        if (!principal.Identities.Any(identity => identity.IsAuthenticated))
            return new(null, 401);
        var actor = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(actor) || actor.Length > 200)
            return new(null, 403);
        if (request.ExpectedRevision < 0)
            return new(null, 400, "expectedRevision");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 500)
            return new(null, 400, "reason");
        if (request.ExpiresAtUtc <= timeProvider.GetUtcNow())
            return new(null, 400, "expiresAtUtc");
        var preview = Preview(new() { Value = request.Value });
        if (preview.Value is null)
            return new(null, preview.Status, preview.Field);
        try
        {
            var revision = await publisher.PublishAsync(preview.Value.Value, request.ExpectedRevision, actor, request.Reason,
                request.ExpiresAtUtc, cancellationToken);
            return new(new(Document(revision)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Both existing authorities use this provider-independent EF concurrency outcome.
        // Compare its stable type identity without introducing EF into the optional HTTP module.
        catch (Exception exception) when (exception.GetType().FullName == "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException")
        {
            return new(null, 409);
        }
        catch (Exception exception)
        {
            LogUnavailable(exception);
            return new(null, 503);
        }
    }

    private RuntimeConfigurationDocument<T> Document(RuntimeConfigurationRevision<T> revision)
        => new(revision.Revision, codec.Deserialize(codec.Serialize(revision.Value)), revision.RecordedAtUtc, revision.ExpiresAtUtc);

    private void LogUnavailable(Exception exception)
        => logger.LogWarning("Runtime configuration management unavailable ({ExceptionType}).", exception.GetType().Name);
}
