#if NET8_0_OR_GREATER
namespace CasCap.Common.Abstractions;

/// <summary>Renders a <see cref="CommsEvent"/> as the text sent to a notification group when it is forwarded directly.</summary>
/// <remarks>
/// Used when no agent turns the event into a reply. Consumers register a default implementation
/// with <c>TryAddSingleton</c>, so an application can replace it by registering its own first.
/// </remarks>
public interface ICommsEventFormatter
{
    /// <summary>Formats <paramref name="commsEvent"/> for direct delivery.</summary>
    /// <param name="commsEvent">The stream event to render.</param>
    /// <returns>The message text.</returns>
    public string Format(CommsEvent commsEvent);
}
#endif
