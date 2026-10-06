#if NET8_0_OR_GREATER
namespace CasCap.Common.Abstractions;

/// <summary>Chooses the notification group a <see cref="CommsEvent"/> is delivered to.</summary>
/// <remarks>
/// Lets operational events, such as scheduled jobs or connection changes, go to an operator-only
/// monitor group while user-facing events stay in the main group. Consumers register a replaceable
/// default with <c>TryAddSingleton</c>, so an application can replace it by registering its own first.
/// </remarks>
public interface ICommsGroupRouter
{
    /// <summary>Returns the exact group name <paramref name="commsEvent"/> should be sent to.</summary>
    /// <param name="commsEvent">The stream event to route.</param>
    /// <returns>The exact group name, including case and spaces.</returns>
    public string ResolveGroup(CommsEvent commsEvent);
}
#endif
