namespace CasCap.Common.Extensions.Tests;

/// <summary>Tests for <see cref="StringExtensions"/>.</summary>
public class StringExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    [Theory, Trait("Category", "String Manipulation")]
    [InlineData("GetAllState", "get_all_state")]
    [InlineData("ABC", "a_b_c")]
    [InlineData("already_snake", "already_snake")]
    public void ToSnakeCase(string input, string expected)
        => Assert.Equal(expected, input.ToSnakeCase());

    [Theory, Trait("Category", "String Manipulation")]
    [InlineData("http://x/", "/api", "http://x/api")]
    [InlineData("http://x", "api", "http://x/api")]
    [InlineData("http://x/", "~/api", "http://x/api")]
    public void UrlCombine(string baseUrl, string relativeUrl, string expected)
        => Assert.Equal(expected, baseUrl.UrlCombine(relativeUrl));

    [Fact, Trait("Category", "String Manipulation")]
    public void String2List()
        => Assert.Equal(["a", "b", "c"], "a\nb\n\nc".String2List());

    [Theory, Trait("Category", "String Manipulation")]
    [InlineData("hello world", 5, false, "hello")]
    [InlineData("hello world", 8, true, "hello...")]
    public void SubstringSafe(string input, int maxLength, bool dots, string expected)
        => Assert.Equal(expected, input.SubstringSafe(maxLength, dots));

    [Fact, Trait("Category", "String Manipulation")]
    public void Clean()
        => Assert.Equal("abcd", "a\tb\nc\rd".Clean());

    [Theory, Trait("Category", "Validation")]
    [InlineData("test@example.com", true)]
    [InlineData("notanemail", false)]
    public void IsEmail(string input, bool expected)
        => Assert.Equal(expected, input.IsEmail());

    [Fact, Trait("Category", "String Manipulation")]
    public void ToBase64()
        => Assert.Equal("YWJj", "abc".ToBase64());

    [Fact, Trait("Category", "String Manipulation")]
    public void Split()
        => Assert.Equal(["a", "b", "c"], "a;b;;c".Split());

    [Fact, Trait("Category", "String Manipulation")]
    public void Sanitize()
        => Assert.Equal("a b c", "a/b:c".Sanitize());

    [Fact, Trait("Category", "Masking")]
    public void MaskPhoneNumber()
        => Assert.Equal("+44********90", "+441234567890".MaskPhoneNumber());

    [Fact, Trait("Category", "Masking")]
    public void MaskEndpoint()
        => Assert.Equal("https://subdomain.***", new Uri("https://subdomain.domain.com/").MaskEndpoint());

    [Fact, Trait("Category", "String Manipulation")]
    public void NormalizeWhitespace()
        => Assert.Equal("a b c", "  a\r\nb  c  ".NormalizeWhitespace());
}
