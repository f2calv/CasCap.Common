namespace CasCap.Common.Configuration.Blazor.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RuntimeConfigurationClientTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task Client_RetainsExactRevisionAndSendsTypedValuesWithAntiforgery()
    {
        Render();
        var client = _context.Services.GetRequiredService<RuntimeConfigurationClient<EditorControls>>();
        var destination = new RuntimeConfigurationDestination("sample-app", "staging", "controls");
        var current = await client.ReadAsync(destination, TestContext.Current.CancellationToken);
        Assert.True(current.Succeeded);
        Assert.Equal(9007199254740993, current.Value!.Revision);
        var value = new EditorControls { Day = DayOfWeek.Friday, Duration = TimeSpan.FromMinutes(8), FeatureEnabled = true, Verbosity = 4 };
        var preview = await client.PreviewAsync(destination, value, TestContext.Current.CancellationToken);
        Assert.True(preview.Succeeded);
        var publication = await client.PublishAsync(destination, new()
        {
            Value = preview.Value!.Value,
            ExpectedRevision = current.Value.Revision,
            Reason = "Typed publication",
        }, TestContext.Current.CancellationToken);
        Assert.True(publication.Succeeded);
        Assert.True(publication.Value!.Committed);
        Assert.True(publication.Value.ActivationPending);
        Assert.Equal(value, _handler.Published!.Value);
        Assert.Equal(1, _handler.Publications);
    }

    [Theory]
    [InlineData(false, RuntimeConfigurationClientStatus.InvalidResponse)]
    [InlineData(true, RuntimeConfigurationClientStatus.Unavailable)]
    public async Task Client_InvalidResponseAndTransportFailureNeverSucceed(bool transport, string status)
    {
        _handler.InvalidSuccess = !transport;
        _handler.TransportFailure = transport;
        Render();
        var client = _context.Services.GetRequiredService<RuntimeConfigurationClient<EditorControls>>();
        var result = await client.ReadAsync(new("sample-app", "staging", "controls"), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Equal(status, result.Status);
        Assert.DoesNotContain("Private transport information", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserAssembly_HasNoProviderOrServerRuntimeDependency()
    {
        var references = typeof(RuntimeConfigurationEditor<>).Assembly.GetReferencedAssemblies().Select(value => value.Name!).ToArray();
        Assert.DoesNotContain(references, value => value.Contains("EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.Contains("Configuration.Runtime", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.Contains("Configuration.Management") && !value.EndsWith(".Contracts", StringComparison.Ordinal));
        Assert.DoesNotContain(references, value => value.Contains("Npgsql", StringComparison.Ordinal) || value.Contains("Sqlite", StringComparison.Ordinal));
    }
}
