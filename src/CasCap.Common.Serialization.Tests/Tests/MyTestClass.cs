namespace CasCap.Common.Serialization.Tests;

/// <summary>
/// Test class for verifying serialization round-trip behaviour.
/// </summary>
[MessagePackObject(true)]
public class MyTestClass
{
    public int ID { get; set; } = 1337;
    public DateTime utcNow { get; set; } = DateTime.UtcNow;
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
    public Dictionary<DateTime, string> d { get; set; } = new Dictionary<DateTime, string> { { DateTime.UtcNow.Date, "x" }, { DateTime.UtcNow.Date.AddDays(-1), "y" } };
}
