namespace CasCap.Common.Validators;

/// <summary>Validates the shared lock configuration, including nested timing profiles.</summary>
internal sealed class CachingConfigValidator : IValidateOptions<CachingConfig>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, CachingConfig options)
    {
        var results = new List<ValidationResult>();
        if (options.Redlock is null)
            return ValidateOptionsResult.Fail("Redlock configuration is required.");

        Validator.TryValidateObject(options.Redlock, new ValidationContext(options.Redlock), results, true);
        if (options.Redlock.Profiles is not null)
        {
            foreach (var profile in options.Redlock.Profiles.Values)
            {
                if (profile is null)
                    results.Add(new ValidationResult("Redlock timing profiles cannot be null."));
                else
                    Validator.TryValidateObject(profile, new ValidationContext(profile), results, true);
            }
        }
        return results.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(results.Select(result => result.ErrorMessage ?? "Invalid Redlock configuration."));
    }
}
