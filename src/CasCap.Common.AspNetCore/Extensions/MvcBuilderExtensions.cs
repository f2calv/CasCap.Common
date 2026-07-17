namespace CasCap.Common.Extensions;

/// <summary>Extension methods for feature-gated MVC controller discovery.</summary>
public static class MvcBuilderExtensions
{
    /// <summary>
    /// Registers a <see cref="FeatureGatedControllerFeatureProvider"/> so <see cref="FeatureControllerAttribute"/>-marked
    /// controllers are only mapped (and advertised in OpenAPI/Swagger) when at least one of their features is present in
    /// <paramref name="enabledFeatures"/>. Unmarked controllers are unaffected.
    /// </summary>
    /// <param name="builder">The MVC builder returned by <c>AddControllers()</c>.</param>
    /// <param name="enabledFeatures">The feature set enabled on this host (the same set passed to the feature-flag launcher).</param>
    public static IMvcBuilder AddFeatureGatedControllers(this IMvcBuilder builder, IReadOnlySet<string> enabledFeatures)
    {
        builder.ConfigureApplicationPartManager(apm =>
            apm.FeatureProviders.Add(new FeatureGatedControllerFeatureProvider(enabledFeatures)));
        return builder;
    }
}
