using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Runtime.Tests.Models;

/// <summary>Deliberately lenient generated settings ensure the codec enforces its own strict wire contract.</summary>
[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip)]
[JsonSerializable(typeof(RuntimeControls))]
internal sealed partial class RuntimeConfigurationJsonContext : JsonSerializerContext;
