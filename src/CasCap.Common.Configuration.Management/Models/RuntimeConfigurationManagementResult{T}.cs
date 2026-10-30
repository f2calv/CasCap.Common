namespace CasCap.Common.Configuration.Management.Models;

/// <summary>A typed service outcome, independent of HTTP response execution.</summary>
/// <param name="Value">The successful response, when available.</param>
/// <param name="Status">The failure status, or 200 on success.</param>
/// <param name="Field">The invalid field, if any.</param>
public sealed record RuntimeConfigurationManagementResult<T>(T? Value, int Status = 200, string? Field = null);
