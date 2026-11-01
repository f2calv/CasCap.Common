using CasCap.Common.Configuration.Blazor.Constants;
using CasCap.Common.Configuration.Blazor.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ManagementClientTests(ITestOutputHelper output) : SqliteTestBase(output)
{
    [Fact]
    public async Task BrowserClient_UsesRealCookieAntiforgeryPreviewAndPublication()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = ManagementTestSupport.Factory(_connectionString, _environment);
        using var http = ManagementTestSupport.Client(host);
        using var login = await ManagementTestSupport.SignInAsync(http);
        var client = new RuntimeConfigurationClient<RuntimeControls>(NullLogger<RuntimeConfigurationClient<RuntimeControls>>.Instance, http);
        var destination = new RuntimeConfigurationDestination("test-application", _environment, "controls");
        var initial = await client.ReadAsync(destination, TestContext.Current.CancellationToken);
        Assert.True(initial.Succeeded);
        var preview = await client.PreviewAsync(destination, new RuntimeControls { Verbosity = 4, FeatureEnabled = true },
            TestContext.Current.CancellationToken);
        Assert.True(preview.Succeeded);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        var publication = await client.PublishAsync(destination, new()
        {
            Value = preview.Value!.Value,
            ExpectedRevision = initial.Value!.Revision,
            Reason = "Typed browser client acceptance",
        }, TestContext.Current.CancellationToken);
        Assert.True(publication.Succeeded);
        Assert.True(publication.Value!.Committed);
        Assert.True(publication.Value.ActivationPending);
        Assert.Equal(2, publication.Value.Document.Revision);
        Assert.True((await TestBase.WaitForAsync(http, value => value.Controls.FeatureEnabled)).Controls.FeatureEnabled);
        var stale = await client.PublishAsync(destination, new()
        {
            Value = new RuntimeControls(),
            ExpectedRevision = 1,
            Reason = "Deliberately stale browser candidate",
        }, TestContext.Current.CancellationToken);
        Assert.Equal(RuntimeConfigurationClientStatus.Conflict, stale.Status);
        Assert.Null(stale.Value);
        Assert.Equal(2, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Theory]
    [InlineData(false, RuntimeConfigurationClientStatus.Unauthorized)]
    [InlineData(true, RuntimeConfigurationClientStatus.Forbidden)]
    public async Task BrowserClient_ReportsActualPermissionFailures(bool viewer, string expectedStatus)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = ManagementTestSupport.Factory(_connectionString, _environment);
        using var http = ManagementTestSupport.Client(host);
        if (viewer)
        {
            using var login = await ManagementTestSupport.SignInAsync(http, role: "viewer");
        }
        var client = new RuntimeConfigurationClient<RuntimeControls>(NullLogger<RuntimeConfigurationClient<RuntimeControls>>.Instance, http);
        var result = await client.PublishAsync(new("test-application", _environment, "controls"),
            ManagementTestSupport.Publication(), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }
}
