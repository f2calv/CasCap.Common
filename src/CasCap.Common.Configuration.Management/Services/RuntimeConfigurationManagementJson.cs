using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Management.Services;

internal sealed class RuntimeConfigurationManagementJson(IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> config)
{
    public JsonSerializerOptions Options { get; } = new(config.Value.JsonSerializerOptions)
    {
        AllowDuplicateProperties = false,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 32,
    };
}
