using System.ComponentModel.DataAnnotations;

namespace CasCap.Tests.Unit;

/// <summary>Tests remote MCP credential-reference validation.</summary>
[Trait("Category", "Agent Creation")]
public sealed class ToolSourceValidationTests
{
    [Fact]
    public void Credential_WithRemoteEndpoint_IsValid()
    {
        var source = new ToolSource
        {
            Endpoint = "https://mcp.example.com/mcp",
            Credential = "operator-tools",
        };

        var results = Validate(source);

        Assert.Empty(results);
    }

    [Fact]
    public void Credential_WithoutRemoteEndpoint_IsRejected()
    {
        var source = new ToolSource
        {
            Service = "OperatorMcpQueryService",
            Credential = "operator-tools",
        };

        var results = Validate(source);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(ToolSource.Credential)));
    }

    private static List<ValidationResult> Validate(ToolSource source)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(source, new ValidationContext(source), results, validateAllProperties: true);
        return results;
    }
}