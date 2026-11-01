using System.Net;
using System.Net.Http.Json;
using static CasCap.Common.Configuration.Runtime.Tests.Tests.Integration.ManagementTestSupport;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ManagementPostgresTests(ITestOutputHelper output) : TestBase(output)
{
    [Fact]
    public async Task IndependentHttpWriters_UseDurablePostgresConcurrencyAndAudit()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var first = Factory(_connectionString, _environment, "PostgreSql");
        await using var second = Factory(_connectionString, _environment, "PostgreSql");
        using var a = Client(first);
        using var b = Client(second);
        var route = Route(_environment);
        await SignInAsync(a, actor: "first-user");
        await SignInAsync(b, actor: "second-user");
        await TokenAsync(a, route);
        await TokenAsync(b, route);
        var responses = await Task.WhenAll(a.PostAsJsonAsync($"{route}/publish", Publication(), TestContext.Current.CancellationToken),
            b.PostAsJsonAsync($"{route}/publish", Publication(), TestContext.Current.CancellationToken));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses)
            response.Dispose();
        var current = (await a.GetFromJsonAsync<RuntimeConfigurationDocument<RuntimeControls>>(route, TestContext.Current.CancellationToken))!;
        Assert.Equal(2, current.Revision);
        Assert.True(current.Value.FeatureEnabled);
        Assert.True((await _store.ReadAsync(TestContext.Current.CancellationToken))!.Actor is "first-user" or "second-user");
        Assert.Null(await _store.ReadRevisionAsync(3, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PostgresOutage_ManagementFailsExplicitlyWhileReaderRetainsAcceptedState()
    {
        await PublishAsync(new RuntimeControls { FeatureEnabled = true }, 0);
        await using var host = Factory(_connectionString, _environment, "PostgreSql");
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route(_environment));
        var before = await WaitForAsync(client, observation => observation.Controls.FeatureEnabled);
        try
        {
            await SetDatabaseAvailabilityAsync(false);
            foreach (var publish in new[] { false, true })
            {
                using var response = publish
                    ? await client.PostAsJsonAsync($"{Route(_environment)}/publish", Publication(), TestContext.Current.CancellationToken)
                    : await client.GetAsync(Route(_environment), TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
                Assert.DoesNotContain(_connectionString, problem);
                Assert.DoesNotContain("Npgsql", problem);
                Assert.True(response.Headers.CacheControl?.NoStore);
            }
            Assert.Equal(before.Sentinel, (await WaitForAsync(client, observation => observation.Controls.FeatureEnabled)).Sentinel);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/feature", TestContext.Current.CancellationToken)).StatusCode);
        }
        finally
        {
            await SetDatabaseAvailabilityAsync(true);
        }
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }
}
