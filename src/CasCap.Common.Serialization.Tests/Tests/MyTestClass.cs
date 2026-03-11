namespace CasCap.Common.Serialization.Tests;

/// <summary>
/// Test class for verifying serialization round-trip behaviour.
/// </summary>
[MessagePackObject(true)]
public class MyTestClass
{
    /// <summary>
    /// Test identifier.
    /// </summary>
    public int ID { get; set; } = 1337;

    /// <summary>
    /// UTC <see cref="DateTime"/> test value.
    /// </summary>
    public DateTime utcNow { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Local date test value.
    /// </summary>
    public DateTime dtNow { get; set; } = DateTime.Now.Date;

    DateTime _dtNowFixed;

    /// <summary>
    /// We send in a normal datetime, which when deserialized by MessagePack gets converted to Utc.
    /// </summary>
    public DateTime dtNowFixed
    {
        get { return _dtNowFixed; }
        set { _dtNowFixed = DateTime.SpecifyKind(value, DateTimeKind.Utc); }
    }

    /// <summary>
    /// Dictionary of <see cref="DateTime"/> to string values for round-trip testing.
    /// </summary>
    public Dictionary<DateTime, string> d { get; set; } = new Dictionary<DateTime, string> { { DateTime.UtcNow.Date, "x" }, { DateTime.UtcNow.Date.AddDays(-1), "y" } };
}
