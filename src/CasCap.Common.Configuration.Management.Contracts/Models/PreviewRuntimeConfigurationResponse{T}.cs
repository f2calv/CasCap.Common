namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>A validated copy of the complete candidate. No durable state has changed.</summary>
/// <param name="Value">The validated browser-safe value. <example>{}</example></param>
public sealed record PreviewRuntimeConfigurationResponse<T>(T Value) where T : class;
