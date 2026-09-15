namespace CasCap.Common.Extensions;

/// <summary>Service registration helpers for the agent framework infrastructure.</summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Builds and registers an <see cref="AgentTypeRegistry"/> so that agent configuration can
    /// resolve <see cref="ToolSource.Service"/> and <see cref="PromptSource.Service"/> names
    /// without scanning every loaded assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tool service types are discovered from <paramref name="services"/> itself — any registered
    /// implementation type exposing <see cref="McpServerToolAttribute"/>-decorated methods is
    /// indexed. Call this <b>after</b> the registrations that add those services, so the registry
    /// reflects the features enabled for this deployment.
    /// </para>
    /// <para>
    /// Prompt types are static classes and therefore absent from the container, so the assemblies
    /// that declare them must be supplied explicitly.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection, inspected for registered tool service types.</param>
    /// <param name="promptAssemblies">Assemblies scanned for <see cref="McpServerPromptTypeAttribute"/> types.</param>
    /// <exception cref="InvalidOperationException">Two distinct types share a simple name.</exception>
    public static IServiceCollection AddAgentTypeRegistry(
        this IServiceCollection services, params Assembly[] promptAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        var toolTypes = services
            .Select(d => d.ImplementationType ?? d.ServiceType)
            .Where(t => t is not null && !t.IsAbstract && !t.IsInterface && HasToolMethods(t))
            .ToList();

        // Prompt types are static classes, which are abstract+sealed in IL — filtering on
        // !IsAbstract here would exclude every one of them.
        var promptTypes = promptAssemblies
            .SelectMany(GetLoadableTypes)
            .Where(t => t.GetCustomAttribute<McpServerPromptTypeAttribute>() is not null)
            .ToList();

        var registry = new AgentTypeRegistry(toolTypes!, promptTypes);
        services.AddSingleton(registry);
        return services;
    }

    private static bool HasToolMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
