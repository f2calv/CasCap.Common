#if NET8_0_OR_GREATER
using System.Reflection;
using CasCap.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering <see cref="IEventSink{T}"/> implementations
/// based on <see cref="SinkTypeAttribute"/> and <see cref="SinkConfig"/> configuration.
/// </summary>
public static partial class SinkServiceCollectionExtensions
{
    /// <summary>
    /// Well-known keyed-service key for the primary event sink — the sink whose query
    /// interfaces are the canonical resolution target.
    /// </summary>
    public const string PrimarySinkKey = "Primary";

    /// <inheritdoc cref="AddEventSinks{TEvent}(IServiceCollection, SinkConfig, ILoggerFactory?, Assembly[])"/>
    public static List<Type> AddEventSinks<TEvent>(this IServiceCollection services, SinkConfig sinkOptions, params Assembly[] assemblies)
        => AddEventSinks<TEvent>(services, sinkOptions, loggerFactory: null, assemblies);

    /// <summary>
    /// Scans the provided <paramref name="assemblies"/> for classes decorated with
    /// <see cref="SinkTypeAttribute"/> that implement <see cref="IEventSink{T}"/> and registers those
    /// whose <see cref="SinkTypeAttribute.SinkType"/> is enabled in the provided <see cref="SinkConfig"/>.
    /// Every enabled sink receives every event. Among the enabled sinks that also implement domain query
    /// interfaces, one answers queries through those interfaces and the <see cref="PrimarySinkKey"/>
    /// alias: the <see cref="SinkConfig.QuerySinkType"/> sink when enabled, otherwise the last registered.
    /// Query-capable sinks registered by an earlier call, such as an in-memory fallback, are removed.
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
    {
        var logger = loggerFactory?.CreateLogger(nameof(SinkServiceCollectionExtensions));
        var registeredSinks = new List<Type>();
        var sinkInterfaceType = typeof(IEventSink<TEvent>);
        var eventTypeName = typeof(TEvent).Name;

        var querySinks = new List<(Type SinkType, string Key, List<Type> Interfaces)>();

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
                if (logger is not null)
                    LogSinkSkipped(logger, nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sinkType.Name);
                continue;
            }

            var extraInterfaces = sinkType.GetInterfaces()
                .Where(i => i != sinkInterfaceType
                    && !sinkInterfaceType.IsAssignableFrom(i)
                    && i.Namespace?.StartsWith("CasCap", StringComparison.Ordinal) == true)
                .ToList();

            if (extraInterfaces.Count > 0)
            {
                foreach (var iface in extraInterfaces)
                {
                    // Remove sinks registered BEFORE this call (e.g. from a different assembly
                    // or the fallback Memory sink). Only keyed registrations with ImplementationType
                    // are discoverable; their factory forwardings use the "Primary" key which will
                    // be re-registered below, so they remain functional.
                    var replacedExternal = services
                        .Where(sd => sd.ServiceType == sinkInterfaceType
                            && sd.ImplementationType is not null
                            && sd.ImplementationType != sinkType
                            && !registeredSinks.Contains(sd.ImplementationType)
                            && iface.IsAssignableFrom(sd.ImplementationType))
                        .ToList();

                    foreach (var sd in replacedExternal)
                    {
                        services.Remove(sd);
                        registeredSinks.Remove(sd.ImplementationType!);
                        if (logger is not null)
                            LogSinkReplaced(logger, nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sd.ImplementationType!.Name, iface.Name);
                    }
                }
            }

            services.Add(new ServiceDescriptor(sinkInterfaceType, attr.SinkType, sinkType, ServiceLifetime.Singleton));
            var sinkKey = attr.SinkType;
            services.AddSingleton(sinkInterfaceType, sp => sp.GetRequiredKeyedService<IEventSink<TEvent>>(sinkKey));
            registeredSinks.Add(sinkType);

            if (extraInterfaces.Count > 0)
                querySinks.Add((sinkType, sinkKey, extraInterfaces));

            if (logger is not null)
                LogSinkRegistered(logger, nameof(SinkServiceCollectionExtensions), eventTypeName, attr.SinkType, sinkType.Name);
        }

        if (querySinks.Count > 0)
        {
            var preferredIndex = querySinks.FindLastIndex(s => s.Key == sinkOptions.QuerySinkType);
            var primary = preferredIndex >= 0 ? querySinks[preferredIndex] : querySinks[^1];
            services.AddKeyedSingleton(sinkInterfaceType, PrimarySinkKey, (sp, _) =>
                sp.GetRequiredKeyedService<IEventSink<TEvent>>(primary.Key));

            foreach (var iface in querySinks.SelectMany(s => s.Interfaces).Distinct())
            {
                var (_, answeringKey, _) = primary.Interfaces.Contains(iface) ? primary : querySinks.FindLast(s => s.Interfaces.Contains(iface));
                services.AddSingleton(iface, sp => sp.GetRequiredKeyedService<IEventSink<TEvent>>(answeringKey));
                if (logger is not null)
                    LogSinkInterfaceRegistered(logger, nameof(SinkServiceCollectionExtensions), eventTypeName, answeringKey, iface.Name);
            }
        }

        if (logger is not null)
            LogSinksRegistered(logger, nameof(SinkServiceCollectionExtensions), eventTypeName, registeredSinks.Count, sinkOptions.AvailableSinks.Count);

        return registeredSinks;
    }

    [LoggerMessage(LogLevel.Debug, "{ClassName} {EventType} sink {SinkType} ({SinkClass}) skipped (disabled)")]
    private static partial void LogSinkSkipped(ILogger logger, string className, string eventType, string sinkType, string sinkClass);

    [LoggerMessage(LogLevel.Information, "{ClassName} {EventType} sink {SinkType} replaces {ReplacedSink} (shared {Interface})")]
    private static partial void LogSinkReplaced(
        ILogger logger,
        string className,
        string eventType,
        string sinkType,
        string replacedSink,
        string @interface);

    [LoggerMessage(LogLevel.Information, "{ClassName} {EventType} sink {SinkType} registered as {Interface}")]
    private static partial void LogSinkInterfaceRegistered(
        ILogger logger,
        string className,
        string eventType,
        string sinkType,
        string @interface);

    [LoggerMessage(LogLevel.Information, "{ClassName} {EventType} sink {SinkType} ({SinkClass}) registered")]
    private static partial void LogSinkRegistered(ILogger logger, string className, string eventType, string sinkType, string sinkClass);

    [LoggerMessage(LogLevel.Information, "{ClassName} {EventType} sinks registered: {Count} of {Total} available")]
    private static partial void LogSinksRegistered(ILogger logger, string className, string eventType, int count, int total);
}
#endif
