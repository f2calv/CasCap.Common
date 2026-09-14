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
        => Assert.Equal(["a", "b", "c"], "a;b;;c".SplitClean());

    [Fact, Trait("Category", "String Manipulation")]
    public void Sanitize()
        => Assert.Equal("a b c", "a/b:c".Sanitize());

    [Fact, Trait("Category", "Masking")]
    public void MaskPhoneNumber()
        => Assert.Equal("+44********90", "+441234567890".MaskPhoneNumber());

    [Theory, Trait("Category", "Masking")]
    [InlineData("https://subdomain.domain.com/", "https://subdomain.***")]
    [InlineData("http://ollama.local:11434/", "http://ollama.***")]
    [InlineData("http://localhost:11434/", "http://***")]
    public void MaskEndpoint(string endpoint, string expected)
        => Assert.Equal(expected, new Uri(endpoint).MaskEndpoint());

    /// <summary>
    /// Callers pass an optional <see cref="Uri"/> straight through (e.g. logging a
    /// <c>ProviderConfig.Endpoint</c> that is only required for some provider types),
    /// so masking a <see langword="null"/> endpoint must not throw.
    /// </summary>
    [Fact, Trait("Category", "Masking")]
    public void MaskEndpoint_Null()
        => Assert.Equal(string.Empty, ((Uri?)null).MaskEndpoint());

    [Fact, Trait("Category", "String Manipulation")]
    public void NormalizeWhitespace()
        => Assert.Equal("a b c", "  a\r\nb  c  ".NormalizeWhitespace());
}
