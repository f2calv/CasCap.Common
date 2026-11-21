using CasCap.Common.Configuration.Aot.TestHost.Models;
using Microsoft.Extensions.Options;

namespace CasCap.Common.Configuration.Aot.TestHost.Services;

/// <summary>Validates the complete small contract explicitly, including nested and cross-property rules.</summary>
internal sealed class ControlsValidator : IValidateOptions<Controls>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, Controls options) =>
        options.Minimum < 0 || options.Maximum > 5 || options.Minimum > options.Maximum
        || options.Day is < DayOfWeek.Sunday or > DayOfWeek.Saturday
        || options.Children is null || options.Children.Any(child => child is null || child.Count is < 0 or > 5 || child.Label is null)
            ? ValidateOptionsResult.Fail("The complete configuration graph must satisfy the controls contract.")
            : ValidateOptionsResult.Success;
}
