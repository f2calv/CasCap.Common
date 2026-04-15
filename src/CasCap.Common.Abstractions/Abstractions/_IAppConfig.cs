namespace CasCap.Common.Abstractions;

/// <summary>
/// This interface is to be implemented by any application configuration class, to allow easy identification.
/// </summary>
public interface IAppConfig
{
#if NET8_0_OR_GREATER
    /// <summary>Configuration section path used for options binding (e.g. <c>"CasCap:MyConfig"</c>).</summary>
    static abstract string ConfigurationSectionName { get; }
#endif
}
