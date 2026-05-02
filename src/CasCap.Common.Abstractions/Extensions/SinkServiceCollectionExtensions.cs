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
    /// interfaces are the canonical resolution target.
    /// </summary>
    public const string PrimarySinkKey = "Primary";

    /// <inheritdoc cref="AddEventSinks{TEvent}(IServiceCollection, SinkConfig, ILoggerFactory?, Assembly[])"/>
    public static List<Type> AddEventSinks<TEvent>(this IServiceCollection services, SinkConfig sinkOptions, params Assembly[] assemblies)
        where TEvent : class, new()
        => AddEventSinks<TEvent>(services, sinkOptions, loggerFactory: null, assemblies);

    /// <summary>
    /// Scans the provided <paramref name="assemblies"/> for classes decorated with
    /// <see cref="SinkTypeAttribute"/> that implement <see cref="IEventSink{T}"/> and registers those
    /// whose <see cref="SinkTypeAttribute.SinkType"/> is enabled in the provided <see cref="SinkConfig"/>.
    /// When a newly registered sink implements domain-specific query interfaces
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
        where TEvent : class, new()
    {
        var logger = loggerFactory?.CreateLogger(nameof(SinkServiceCollectionExtensions));
        var registeredSinks = new List<Type>();
        var sinkInterfaceType = typeof(IEventSink<TEvent>);
        var eventTypeName = typeof(TEvent).Name;

        // Track ALL descriptors added per sink type so replacements within the same call
        // can fully remove the predecessor — including factory-forwarding descriptors that
        // have ImplementationType = null and are invisible to the ImplementationType-based scan.
        var descriptorsBySink = new Dictionary<Type, List<ServiceDescriptor>>();

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
                    // Remove sinks registered within THIS call — tracked descriptors let us
                    // remove factory-forwarding registrations that lack ImplementationType.
                    var replacedTracked = descriptorsBySink.Keys
                        .Where(t => t != sinkType && iface.IsAssignableFrom(t))
                        .ToList();

                    foreach (var replacedType in replacedTracked)
                    {
                        foreach (var sd in descriptorsBySink[replacedType])
                            services.Remove(sd);

                        registeredSinks.Remove(replacedType);
                        descriptorsBySink.Remove(replacedType);
                        logger?.LogInformation("{ClassName} {EventType} sink {SinkType} replaces {ReplacedSink} (shared {Interface})",
                            nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, replacedType.Name, iface.Name);
                    }

                    // Remove sinks registered BEFORE this call (e.g. from a different assembly
                    // or the fallback Memory sink). Only keyed registrations with ImplementationType
                    // are discoverable; their factory forwardings use the "Primary" key which will
                    // be re-registered below, so they remain functional.
                    var replacedExternal = services
                        .Where(sd => sd.ServiceType == sinkInterfaceType
                            && sd.ImplementationType is not null
                            && sd.ImplementationType != sinkType
                            && !descriptorsBySink.ContainsKey(sd.ImplementationType)
                            && iface.IsAssignableFrom(sd.ImplementationType))
                        .ToList();

                    foreach (var sd in replacedExternal)
                    {
                        services.Remove(sd);
                        registeredSinks.Remove(sd.ImplementationType!);
                        logger?.LogInformation("{ClassName} {EventType} sink {SinkType} replaces {ReplacedSink} (shared {Interface})",
                            nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sd.ImplementationType!.Name, iface.Name);
                    }
                }
            }

            var newDescriptors = new List<ServiceDescriptor>();

            var keyedDesc = new ServiceDescriptor(sinkInterfaceType, attr.SinkType, sinkType, ServiceLifetime.Singleton);
            services.Add(keyedDesc);
            newDescriptors.Add(keyedDesc);

            var fwdDesc = ServiceDescriptor.Singleton(sinkInterfaceType, sp =>
                sp.GetRequiredKeyedService<IEventSink<TEvent>>(attr.SinkType));
            services.Add(fwdDesc);
            newDescriptors.Add(fwdDesc);

            registeredSinks.Add(sinkType);

            // Register domain-specific interfaces as forwarding singletons and set the Primary keyed alias
            if (extraInterfaces.Count > 0)
            {
                var primaryDesc = ServiceDescriptor.KeyedSingleton(sinkInterfaceType, PrimarySinkKey, (sp, _) =>
                    sp.GetRequiredKeyedService<IEventSink<TEvent>>(attr.SinkType));
                services.Add(primaryDesc);
                newDescriptors.Add(primaryDesc);

                foreach (var iface in extraInterfaces)
                {
                    var ifaceDesc = ServiceDescriptor.Singleton(iface, sp => sp.GetRequiredKeyedService<IEventSink<TEvent>>(attr.SinkType));
                    services.Add(ifaceDesc);
                    newDescriptors.Add(ifaceDesc);
                    logger?.LogInformation("{ClassName} {EventType} sink {SinkType} registered as {Interface}",
                        nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, iface.Name);
                }
            }

            descriptorsBySink[sinkType] = newDescriptors;

            logger?.LogInformation("{ClassName} {EventType} sink {SinkType} ({SinkClass}) registered",
                nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sinkType.Name);
        }

        logger?.LogInformation("{ClassName} {EventType} sinks registered: {Count} of {Total} available",
            nameof(SinkServiceCollectionExtensions), eventTypeName, registeredSinks.Count, sinkOptions.AvailableSinks.Count);

        return registeredSinks;
    }
}
#endif
