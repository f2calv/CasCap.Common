namespace CasCap.Common.Providers;

/// <summary>
/// Removes <see cref="FeatureControllerAttribute"/>-marked controllers whose feature(s) are not enabled on this host,
/// so a controller is only routed (and advertised in OpenAPI/Swagger) where its feature-scoped dependencies exist.
/// </summary>
/// <remarks>
/// Runs after the default <see cref="ControllerFeatureProvider"/> has populated the controller list, then prunes it.
/// Controllers without a <see cref="FeatureControllerAttribute"/> are always kept. Register it via
/// <see cref="MvcBuilderExtensions.AddFeatureGatedControllers(IMvcBuilder, IReadOnlySet{string})"/>.
/// </remarks>
public sealed class FeatureGatedControllerFeatureProvider(IReadOnlySet<string> enabledFeatures)
    : IApplicationFeatureProvider<ControllerFeature>
{
    /// <inheritdoc/>
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        for (var i = feature.Controllers.Count - 1; i >= 0; i--)
        {
            var attr = feature.Controllers[i].GetCustomAttribute<FeatureControllerAttribute>(inherit: false);
            if (attr is not null && !attr.Features.Overlaps(enabledFeatures))
                feature.Controllers.RemoveAt(i);
        }
    }
}
