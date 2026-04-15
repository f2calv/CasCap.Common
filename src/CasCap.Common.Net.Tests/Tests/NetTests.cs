namespace CasCap.Common.Net.Tests;

/// <summary>Tests for <see cref="NetExtensions"/> methods.</summary>
public class NetExtensionTests(ITestOutputHelper testOutputHelper) : TestBase(testOutputHelper)
{
    /// <summary>Verifies that a single key-value pair is formatted as a query string.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void ToQueryString_SinglePair()
    {
        var nvc = new NameValueCollection { { "key1", "value1" } };
        var result = nvc.ToQueryString();
        Assert.Equal("?key1=value1", result);
    }

    /// <summary>Verifies that multiple key-value pairs are formatted as a query string.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void ToQueryString_MultiplePairs()
    {
        var nvc = new NameValueCollection
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };
        var result = nvc.ToQueryString();
        Assert.Equal("?key1=value1&key2=value2", result);
    }

    /// <summary>Verifies that special characters in query string values are URL-encoded.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void ToQueryString_EncodesSpecialCharacters()
    {
        var nvc = new NameValueCollection { { "q", "hello world" } };
        var result = nvc.ToQueryString();
        Assert.Equal("?q=hello+world", result);
    }

    /// <summary>Verifies that an empty collection produces a bare question-mark query string.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void ToQueryString_Empty()
    {
        var nvc = new NameValueCollection();
        var result = nvc.ToQueryString();
        Assert.Equal("?", result);
    }

    /// <summary>Verifies that AddOrOverwrite adds a new header when the header does not exist.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_String_AddsNewHeader()
    {
        using var request = new HttpRequestMessage();
        request.Headers.AddOrOverwrite("X-Custom", "value1");
        Assert.Equal("value1", request.Headers.GetValues("X-Custom").First());
    }

    /// <summary>Verifies that AddOrOverwrite replaces an existing header value.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_String_OverwritesExistingHeader()
    {
        using var request = new HttpRequestMessage();
        request.Headers.AddOrOverwrite("X-Custom", "value1");
        request.Headers.AddOrOverwrite("X-Custom", "value2");
        var values = request.Headers.GetValues("X-Custom").ToList();
        Assert.Single(values);
        Assert.Equal("value2", values[0]);
    }

    /// <summary>Verifies that AddOrOverwrite adds multiple headers from a list.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_List_AddsMultipleHeaders()
    {
        using var request = new HttpRequestMessage();
        var headers = new List<(string name, string value)>
        {
            ("X-First", "a"),
            ("X-Second", "b")
        };
        request.Headers.AddOrOverwrite(headers);
        Assert.Equal("a", request.Headers.GetValues("X-First").First());
        Assert.Equal("b", request.Headers.GetValues("X-Second").First());
    }

    /// <summary>Verifies that passing a null list to AddOrOverwrite is a no-op.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_List_NullIsNoOp()
    {
        using var request = new HttpRequestMessage();
        request.Headers.AddOrOverwrite((List<(string name, string value)>?)null);
        Assert.Empty(request.Headers);
    }

    /// <summary>Verifies that passing an empty list to AddOrOverwrite is a no-op.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_List_EmptyIsNoOp()
    {
        using var request = new HttpRequestMessage();
        request.Headers.AddOrOverwrite(new List<(string name, string value)>());
        Assert.Empty(request.Headers);
    }

    /// <summary>Verifies that AddOrOverwrite adds multiple headers from a dictionary.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_Dictionary_AddsMultipleHeaders()
    {
        using var request = new HttpRequestMessage();
        var headers = new Dictionary<string, string>
        {
            ["X-First"] = "a",
            ["X-Second"] = "b"
        };
        request.Headers.AddOrOverwrite(headers);
        Assert.Equal("a", request.Headers.GetValues("X-First").First());
        Assert.Equal("b", request.Headers.GetValues("X-Second").First());
    }

    /// <summary>Verifies that passing a null dictionary to AddOrOverwrite is a no-op.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_Dictionary_NullIsNoOp()
    {
        using var request = new HttpRequestMessage();
        request.Headers.AddOrOverwrite((Dictionary<string, string>?)null);
        Assert.Empty(request.Headers);
    }

    /// <summary>Verifies that AddOrOverwrite overwrites an existing header from a dictionary.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void AddOrOverwrite_Dictionary_OverwritesExistingHeader()
    {
        using var request = new HttpRequestMessage();
        request.Headers.AddOrOverwrite("X-Custom", "old");
        var headers = new Dictionary<string, string> { ["X-Custom"] = "new" };
        request.Headers.AddOrOverwrite(headers);
        var values = request.Headers.GetValues("X-Custom").ToList();
        Assert.Single(values);
        Assert.Equal("new", values[0]);
    }

    /// <summary>Verifies that TryGetValue returns the first value of an existing header.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void TryGetValue_ReturnsFirstValue()
    {
        using var response = new HttpResponseMessage();
        response.Headers.Add("X-Custom", "value1");
        var result = response.Headers.TryGetValue("X-Custom");
        Assert.Equal("value1", result);
    }

    /// <summary>Verifies that TryGetValue returns null when the header does not exist.</summary>
    [Fact, Trait("Category", "Extensions")]
    public void TryGetValue_MissingHeader_ReturnsNull()
    {
        using var response = new HttpResponseMessage();
        var result = response.Headers.TryGetValue("X-Missing");
        Assert.Null(result);
    }
}
