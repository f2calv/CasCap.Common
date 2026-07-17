namespace CasCap.Common.Abstractions;

/// <summary>
/// Marks a controller as belonging to one or more feature flags, so it is only discovered (routed and advertised in
/// OpenAPI/Swagger) on a host whose enabled feature set includes at least one of them.
/// </summary>
/// <remarks>
/// In a single-assembly modular monolith the whole controller surface compiles into one process, but each deployment
/// (pod/host) typically enables only a subset of features and registers only that subset's services. Without gating,
/// every host maps every controller and returns HTTP 500 when a controller's feature-scoped dependency is not
/// registered. Applying this attribute makes <see cref="FeatureGatedControllerFeatureProvider"/> remove the controller
/// on hosts where none of its features are enabled. An <b>unmarked</b> controller is always available — its
/// dependencies must therefore be registered on every host that maps controllers.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class FeatureControllerAttribute(params string[] features) : Attribute
{
    /// <summary>Feature names that gate this controller; the controller is exposed when any one of them is enabled.</summary>
    public IReadOnlySet<string> Features { get; } = features.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
