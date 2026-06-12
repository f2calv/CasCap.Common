using System.ComponentModel;
using System.Text;

namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for collection, parsing and serialization helpers in <see cref="HelperExtensions"/>.</summary>
public class HelperExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    private enum Described
    {
        [Description("A friendly description")] Friendly = 1,
        Bare = 2,
    }

    /// <summary>Simple DTO used to exercise XML (de)serialization helpers.</summary>
    public class XmlSample
    {
        /// <summary>The sample name.</summary>
        public string? Name { get; set; }
    }

    [Fact, Trait("Category", "Collections")]
    public void GetBatches()
    {
        var batches = new List<int> { 0, 1, 2, 3, 4 }.GetBatches(2);
        Assert.Equal(3, batches.Count);
        Assert.Equal([0, 1], batches[0]);
        Assert.Equal([2, 3], batches[1]);
        Assert.Equal([4], batches[2]);
    }

    [Fact, Trait("Category", "Collections")]
    public void IsNullOrEmpty_And_IsAny()
    {
        List<int>? nullList = null;
        Assert.True(nullList.IsNullOrEmpty());
        Assert.True(new List<int>().IsNullOrEmpty());
        Assert.False(new List<int> { 1 }.IsNullOrEmpty());

        Assert.False(new List<int>().IsAny());
        Assert.True(new List<int> { 1 }.IsAny());
    }

    [Fact, Trait("Category", "Collections")]
    public void ToHashSet_DeduplicatesValues()
    {
        var hs = new[] { 1, 1, 2, 3, 3 }.ToHashSet();
        Assert.Equal([1, 2, 3], hs.OrderBy(x => x));
    }

    [Theory, Trait("Category", "Parsing")]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("nonsense", false)]
    public void ToBoolean(string input, bool expected)
        => Assert.Equal(expected, input.ToBoolean());

    [Theory, Trait("Category", "Parsing")]
    [InlineData("42", 42)]
    [InlineData("", 0)]
    [InlineData("not-a-number", 0)]
    public void ToInt(string input, int expected)
        => Assert.Equal(expected, input.ToInt());

    [Theory, Trait("Category", "Parsing")]
    [InlineData("10.5", 10.5)]
    [InlineData("", 0)]
    public void ToDecimal(string input, decimal expected)
        => Assert.Equal(expected, input.ToDecimal());

    [Theory, Trait("Category", "Enums")]
    [InlineData("Friendly", "A friendly description")]
    [InlineData("Bare", "Bare")]
    public void GetDescription(string member, string expected)
        => Assert.Equal(expected, Enum.Parse<Described>(member).GetDescription());

    [Fact, Trait("Category", "Serialization")]
    public void FromXml_RoundTrips()
    {
        var obj = "<XmlSample><Name>Bob</Name></XmlSample>".FromXml<XmlSample>();
        Assert.Equal("Bob", obj!.Name);
    }

    [Fact, Trait("Category", "Serialization")]
    public void FromBytes_RoundTrips()
    {
        var bytes = Encoding.UTF8.GetBytes("<XmlSample><Name>Jane</Name></XmlSample>");
        var obj = bytes.FromBytes<XmlSample>();
        Assert.Equal("Jane", obj!.Name);
    }
}
