namespace CasCap.Common.Configuration.Management.Contracts.Models;

/// <summary>The exact application/environment/contract resource passed to host authorization handlers.</summary>
/// <param name="Application">The configured application. <example>sample-app</example></param>
/// <param name="Environment">The configured environment. <example>staging</example></param>
/// <param name="Contract">The configured contract identifier. <example>controls</example></param>
public sealed record RuntimeConfigurationDestination(string Application, string Environment, string Contract);
