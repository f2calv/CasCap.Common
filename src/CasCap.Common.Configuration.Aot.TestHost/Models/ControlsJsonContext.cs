using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Aot.TestHost.Models;

/// <summary>Provides the entire host-owned serialization graph without reflection.</summary>
[JsonSerializable(typeof(Controls))]
internal sealed partial class ControlsJsonContext : JsonSerializerContext;
