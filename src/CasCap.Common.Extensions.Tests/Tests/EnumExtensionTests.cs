using System.ComponentModel.DataAnnotations;

namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for <see cref="EnumExtensions"/> and enum-related helpers in <see cref="HelperExtensions"/>.</summary>
public class EnumExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    private enum Sample { None = 0, First = 1, Second = 2 }

    private enum WithDisplay
    {
        [Display(Name = "Pretty Name")] Fancy = 1,
        Plain = 2,
    }

    // Two distinct enums that share a member name to prove the per-type cache in ParseEnumFAST.
    private enum SharedA { Shared = 1, OnlyA = 2 }

    private enum SharedB { Shared = 5, OnlyB = 6 }

    [Fact, Trait("Category", "Enums")]
    public void GetAllItems()
    {
        var items = EnumExtensions.GetAllItems<Sample>().ToList();
        Assert.Equal([Sample.None, Sample.First, Sample.Second], items);
    }

    [Theory, Trait("Category", "Enums")]
    [InlineData("First", "First")]
    [InlineData("first", "First")]
    [InlineData("SECOND", "Second")]
    public void ParseEnum(string value, string expected)
        => Assert.Equal(Enum.Parse<Sample>(expected), value.ParseEnum<Sample>());

    [Theory, Trait("Category", "Enums")]
    [InlineData("First", true, "First")]
    [InlineData("nope", false, "None")]
    public void TryParseEnum(string value, bool expectedResult, string expectedName)
    {
        var result = value.TryParseEnum<Sample>(out var parsed);
        Assert.Equal(expectedResult, result);
        Assert.Equal(Enum.Parse<Sample>(expectedName), parsed);
    }

    [Fact, Trait("Category", "Enums")]
    public void ParseEnumFAST_CachesAndIsCaseInsensitive()
    {
        Assert.Equal(Sample.Second, "Second".ParseEnumFAST<Sample>());
        Assert.Equal(Sample.Second, "second".ParseEnumFAST<Sample>());
    }

    [Fact, Trait("Category", "Enums")]
    public void ParseEnumFAST_DistinctEnumsSharingMemberNameDoNotCollide()
    {
        // Prior to the per-(type,value) cache key these collided and returned the wrong enum.
        Assert.Equal(SharedA.Shared, "Shared".ParseEnumFAST<SharedA>());
        Assert.Equal(SharedB.Shared, "Shared".ParseEnumFAST<SharedB>());
    }

    [Fact, Trait("Category", "Enums")]
    public void ToStringCached()
        => Assert.Equal("First", Sample.First.ToStringCached());

    [Theory, Trait("Category", "Enums")]
    [InlineData("Fancy", "Pretty Name")]
    public void GetDisplayName(string member, string expected)
        => Assert.Equal(expected, Enum.Parse<WithDisplay>(member).GetDisplayName());

    [Fact, Trait("Category", "Enums")]
    public void HasFlag_AnyOfList()
    {
        const FileAccess value = FileAccess.Read;
        Assert.True(value.HasFlag([FileAccess.Read, FileAccess.Write]));
        Assert.False(value.HasFlag([FileAccess.Write]));
    }
}
