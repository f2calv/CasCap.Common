using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CasCap.Common.Configuration.Management.Services;

internal sealed class RuntimeConfigurationManagementBodyBinder(RuntimeConfigurationManagementJson json) : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        const int maximumBytes = 98_304;
        var request = bindingContext.HttpContext.Request;
        var cancellationToken = bindingContext.HttpContext.RequestAborted;
        if (!request.HasJsonContentType() || request.ContentLength > maximumBytes)
        {
            Reject(bindingContext);
            return;
        }
        using var buffer = new MemoryStream();
        var chunk = new byte[8_192];
        int count;
        while ((count = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > maximumBytes)
            {
                Reject(bindingContext);
                return;
            }
            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }
        try
        {
            var value = JsonSerializer.Deserialize(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length)),
                bindingContext.ModelType, json.Options);
            if (value is null)
                Reject(bindingContext);
            else
                bindingContext.Result = ModelBindingResult.Success(value);
        }
        catch (JsonException)
        {
            Reject(bindingContext);
        }
    }

    private static void Reject(ModelBindingContext context)
        => context.ModelState.TryAddModelError("request", "A complete, correctly shaped JSON request is required.");
}
