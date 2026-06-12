namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for <see cref="DateTimeExtensions"/>.</summary>
public class DateTimeExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Fact, Trait("Category", "Dates")]
    public void TruncateToHour()
        => Assert.Equal(new DateTime(2024, 1, 2, 13, 0, 0), new DateTime(2024, 1, 2, 13, 45, 30).TruncateToHour());

    [Fact, Trait("Category", "Dates")]
    public void TruncateToDay()
        => Assert.Equal(new DateTime(2024, 1, 2), new DateTime(2024, 1, 2, 13, 45, 30).TruncateToDay());

    [Fact, Trait("Category", "Dates")]
    public void TruncateToMonth()
        => Assert.Equal(new DateTime(2024, 1, 1), new DateTime(2024, 1, 17, 13, 45, 30).TruncateToMonth());

    [Fact, Trait("Category", "Dates")]
    public void GetMissingDates()
    {
        var dates = new DateTime(2024, 1, 1).GetMissingDates(new DateTime(2024, 1, 4));
        Assert.Equal([new(2024, 1, 2), new(2024, 1, 3), new(2024, 1, 4)], dates);
    }

    [Theory, Trait("Category", "Dates")]
    [InlineData("2024-01-06", true)]  // Saturday
    [InlineData("2024-01-07", true)]  // Sunday
    [InlineData("2024-01-08", false)] // Monday
    public void IsWeekend(string date, bool expected)
        => Assert.Equal(expected, DateTime.Parse(date).IsWeekend());

    [Fact, Trait("Category", "Dates")]
    public void ToUtc_SpecifiesUtcKind()
    {
        var dt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).ToUtc();
        Assert.Equal(DateTimeKind.Utc, dt.Kind);
    }

    [Fact, Trait("Category", "Dates")]
    public void To_yyyy_MM_dd()
        => Assert.Equal("2024-01-02", new DateTime(2024, 1, 2).To_yyyy_MM_dd());

    [Fact, Trait("Category", "Dates")]
    public void GetTimeDifference_FormatsComponents()
    {
        var ts = new TimeSpan(1, 2, 3, 4);
        Assert.Equal("1d 2h 3m 4s", ts.GetTimeDifference());
    }
}
