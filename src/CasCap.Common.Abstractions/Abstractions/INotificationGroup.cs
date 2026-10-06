namespace CasCap.Common.Abstractions;

/// <summary>Represents a named group in a notification service.</summary>
public interface INotificationGroup
{
    /// <summary>The group identifier used when sending messages to the group.</summary>
    public string Id { get; }

    /// <summary>The display name of the group.</summary>
    public string Name { get; }

    /// <summary>The member identifiers (e.g. phone numbers) in the group.</summary>
    public string[] Members { get; }
}
