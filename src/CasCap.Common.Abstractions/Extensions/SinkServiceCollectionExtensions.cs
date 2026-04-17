#if NET8_0_OR_GREATER
using System.Reflection;
using CasCap.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="IEventSink{T}"/> implementations
/// based on <see cref="SinkTypeAttribute"/> and <see cref="SinkConfig"/> configuration.
/// </summary>
public static class SinkServiceCollectionExtensions
{
    /// <summary>
    /// Well-known keyed-service key for the primary event sink — the sink whose query
    /// interfaces (e.g. <c>IBuderusQuery</c>) are the canonical resolution target.
    /// </summary>
    public const string PrimarySinkKey = "Primary";

    /// <inheritdoc cref="AddEventSinks{TEvent}(IServiceCollection, SinkConfig, ILoggerFactory?, Assembly[])"/>
    public static List<Type> AddEventSinks<TEvent>(this IServiceCollection services, SinkConfig sinkOptions, params Assembly[] assemblies)
        where TEvent : class
        => AddEventSinks<TEvent>(services, sinkOptions, loggerFactory: null, assemblies);

    /// <summary>
    /// Scans the provided <paramref name="assemblies"/> for classes decorated with
    /// <see cref="SinkTypeAttribute"/> that implement <see cref="IEventSink{T}"/> and registers those
    /// whose <see cref="SinkTypeAttribute.SinkType"/> is enabled in the provided <see cref="SinkConfig"/>.
    /// When a newly registered sink implements domain-specific interfaces (e.g. <c>IBuderusQuery</c>)
    /// that a previously registered sink also implements, the earlier sink is automatically replaced
    /// so that only the latest writer survives for event processing.
    /// </summary>
    /// <typeparam name="TEvent">The event type handled by the sinks.</typeparam>
    /// <param name="services">The service collection to register sinks into.</param>
    /// <param name="sinkOptions">The sink configuration specifying which sink types are enabled.</param>
    /// <param name="loggerFactory">
    /// Optional logger factory for diagnostic logging during sink registration.
    /// When <see langword="null"/>, no logging is emitted.
    /// </param>
    /// <param name="assemblies">The assemblies to scan for sink implementations.</param>
    /// <returns>The list of sink types that were registered.</returns>
    public static List<Type> AddEventSinks<TEvent>(
        this IServiceCollection services,
        SinkConfig sinkOptions,
        ILoggerFactory? loggerFactory,
        params Assembly[] assemblies)
        where TEvent : class
    {
        var logger = loggerFactory?.CreateLogger(nameof(SinkServiceCollectionExtensions));
        var registeredSinks = new List<Type>();
        var sinkInterfaceType = typeof(IEventSink<TEvent>);
        var eventTypeName = typeof(TEvent).Name;

        var sinkTypes = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false }
                && sinkInterfaceType.IsAssignableFrom(t)
                && t.GetCustomAttribute<SinkTypeAttribute>() is not null);

        foreach (var sinkType in sinkTypes)
        {
            var attr = sinkType.GetCustomAttribute<SinkTypeAttribute>()!;
            if (!sinkOptions.AvailableSinks.TryGetValue(attr.SinkType, out var sinkConfig) || !sinkConfig.Enabled)
            {
                logger?.LogDebug("{ClassName} {EventType} sink {SinkType} ({SinkClass}) skipped (disabled)",
                    nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sinkType.Name);
                continue;
            }

            var extraInterfaces = sinkType.GetInterfaces()
                .Where(i => i != sinkInterfaceType
                    && !sinkInterfaceType.IsAssignableFrom(i)
                    && i.Namespace?.StartsWith("CasCap", StringComparison.Ordinal) == true)
                .ToList();

            // When a new sink shares domain interfaces with a previously registered sink,
            // remove the earlier sink entirely so the new one replaces it.
            if (extraInterfaces.Count > 0)
            {
                foreach (var iface in extraInterfaces)
                {
                    var replaced = services
                        .Where(sd => sd.ServiceType == sinkInterfaceType
                            && sd.ImplementationType is not null
                            && sd.ImplementationType != sinkType
                            && iface.IsAssignableFrom(sd.ImplementationType))
                        .ToList();

                    foreach (var sd in replaced)
                    {
                        services.Remove(sd);
                        registeredSinks.Remove(sd.ImplementationType!);
                        logger?.LogInformation("{ClassName} {EventType} sink {SinkType} replaces {ReplacedSink} (shared {Interface})",
                            nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sd.ImplementationType!.Name, iface.Name);
                    }
                }
            }

            services.AddKeyedSingleton(sinkInterfaceType, attr.SinkType, sinkType);
            services.AddSingleton(sinkInterfaceType, sp =>
                sp.GetRequiredKeyedService<IEventSink<TEvent>>(attr.SinkType));
            registeredSinks.Add(sinkType);

            // Register domain-specific interfaces as forwarding singletons and set the Primary keyed alias
            if (extraInterfaces.Count > 0)
            {
                services.AddKeyedSingleton(sinkInterfaceType, PrimarySinkKey, (sp, _) =>
                    sp.GetRequiredKeyedService<IEventSink<TEvent>>(attr.SinkType));

                foreach (var iface in extraInterfaces)
                {
                    services.AddSingleton(iface, sp => sp.GetRequiredKeyedService<IEventSink<TEvent>>(attr.SinkType));
                    logger?.LogInformation("{ClassName} {EventType} sink {SinkType} registered as {Interface}",
                        nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, iface.Name);
                }
            }

            logger?.LogInformation("{ClassName} {EventType} sink {SinkType} ({SinkClass}) registered",
                nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sinkType.Name);
        }

        logger?.LogInformation("{ClassName} {EventType} sinks registered: {Count} of {Total} available",
            nameof(SinkServiceCollectionExtensions), eventTypeName, registeredSinks.Count, sinkOptions.AvailableSinks.Count);

        return registeredSinks;
    }
}
#endif
