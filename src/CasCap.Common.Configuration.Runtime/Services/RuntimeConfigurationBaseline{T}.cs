namespace CasCap.Common.Configuration.Runtime.Services;

/// <summary>Owns the serialized immutable startup fallback used after a revision expires.</summary>
internal sealed record RuntimeConfigurationBaseline<T>(string Json) where T : class;
