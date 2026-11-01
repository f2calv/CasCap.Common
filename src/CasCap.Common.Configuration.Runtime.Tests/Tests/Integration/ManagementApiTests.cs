using CasCap.Common.Configuration.Management.Contracts.Constants;
using CasCap.Common.Configuration.Runtime.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using static CasCap.Common.Configuration.Runtime.Tests.Tests.Integration.ManagementTestSupport;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ManagementApiTests(ITestOutputHelper output) : SqliteTestBase(output)
{
    private string Route => ManagementTestSupport.Route(_environment);
    private string KeyDirectory => Path.GetDirectoryName(new SqliteConnectionStringBuilder(_connectionString).DataSource)!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StatelessHosts_PreviewCommitReplacementAndActivationRetainSentinel(bool cached)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var first = Factory(_connectionString, _environment, keyDirectory: KeyDirectory,
            configure: cached ? AddCache : null);
        await using var second = Factory(_connectionString, _environment, keyDirectory: KeyDirectory,
            configure: cached ? AddCache : null);
        using var a = Client(first);
        using var b = Client(second);
        var beforeA = await TestBase.WaitForAsync(a, _ => true);
        var beforeB = await TestBase.WaitForAsync(b, _ => true);
        await SignInAsync(a);
        await TokenAsync(a, Route);
        using var currentResponse = await a.GetAsync(Route, TestContext.Current.CancellationToken);
        Assert.True(currentResponse.Headers.CacheControl?.NoStore);
        var current = (await currentResponse.Content.ReadFromJsonAsync<RuntimeConfigurationDocument<RuntimeControls>>(
            TestContext.Current.CancellationToken))!;
        Assert.Equal(1, current.Revision);
        var wire = await currentResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        foreach (var secret in new[] { "actor", "reason", "acceptedRevision", "connection", "restoredFromRevision" })
            Assert.DoesNotContain(secret, wire, StringComparison.OrdinalIgnoreCase);
        using var preview = await a.PostAsJsonAsync($"{Route}/preview",
            new PreviewRuntimeConfigurationRequest<RuntimeControls> { Value = Publication().Value }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.False((await TestBase.WaitForAsync(a, _ => true)).Controls.FeatureEnabled);
        using var publish = await a.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var committed = (await publish.Content.ReadFromJsonAsync<PublishRuntimeConfigurationResponse<RuntimeControls>>(
            TestContext.Current.CancellationToken))!;
        Assert.True(committed.Committed);
        Assert.True(committed.ActivationPending);
        Assert.Equal(2, committed.Document.Revision);
        Assert.Equal("first-user", (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Actor);
        Assert.Equal(beforeA.Sentinel, (await TestBase.WaitForAsync(a, observation => observation.Controls.FeatureEnabled)).Sentinel);
        Assert.Equal(beforeB.Sentinel, (await TestBase.WaitForAsync(b, observation => observation.Controls.FeatureEnabled)).Sentinel);
        Assert.Equal(HttpStatusCode.OK, (await b.GetAsync("/feature", TestContext.Current.CancellationToken)).StatusCode);
        await using var replacement = Factory(_connectionString, _environment, keyDirectory: KeyDirectory);
        using var c = Client(replacement);
        // Carry the browser's paired cookies and token to a replacement sharing the key ring.
        using var fresh = Client(first);
        using var freshLogin = await SignInAsync(fresh);
        using var freshTokenResponse = await fresh.GetAsync($"{Route}/antiforgery", TestContext.Current.CancellationToken);
        var freshToken = (await freshTokenResponse.Content.ReadFromJsonAsync<RuntimeConfigurationAntiforgeryResponse>(
            TestContext.Current.CancellationToken))!;
        c.DefaultRequestHeaders.Add("Cookie", string.Join("; ", freshLogin.Headers.GetValues("Set-Cookie")
            .Concat(freshTokenResponse.Headers.GetValues("Set-Cookie")).Select(cookie => cookie.Split(';')[0])));
        c.DefaultRequestHeaders.Add(freshToken.HeaderName, freshToken.RequestToken);
        using var replacementPublish = await c.PostAsJsonAsync($"{Route}/publish", Publication(2, 3), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, replacementPublish.StatusCode);
        Assert.Equal(3, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        if (cached)
            await DeleteCacheAsync(first.Services);
    }

    [Theory]
    [InlineData(null, 401, 401, 401)]
    [InlineData("none", 403, 403, 403)]
    [InlineData("viewer", 200, 403, 403)]
    [InlineData("editor", 200, 200, 403)]
    [InlineData("publisher", 200, 200, 200)]
    public async Task AuthenticationAndOperationPolicies_AreServerEnforced(string? role, int read, int preview, int publish)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        if (role is not null)
            await SignInAsync(client, role);
        if (read == 200)
            await TokenAsync(client, Route);
        using var current = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        using var validation = await client.PostAsJsonAsync($"{Route}/preview", new { value = new RuntimeControls() },
            TestContext.Current.CancellationToken);
        using var publication = await client.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken);
        Assert.Equal(read, (int)current.StatusCode);
        Assert.Equal(preview, (int)validation.StatusCode);
        Assert.Equal(publish, (int)publication.StatusCode);
        Assert.Null(current.Headers.Location);
        Assert.True(current.Headers.CacheControl?.NoStore);
        Assert.Equal(publish == 200 ? 2 : 1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Fact]
    public async Task MissingPolicy_FailsClosed()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment, publishPolicy: false);
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        using var response = await client.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Fact]
    public async Task ControllerDiscovery_AdvertisesClosedTypedBodiesAndOnlyFourCoreOperations()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        var descriptions = host.Services.GetRequiredService<IApiDescriptionGroupCollectionProvider>()
            .ApiDescriptionGroups.Items.SelectMany(group => group.Items)
            .Where(description => description.RelativePath?.StartsWith("api/runtime-configuration/", StringComparison.Ordinal) is true)
            .ToArray();
        Assert.Equal(4, descriptions.Length);
        var publish = Assert.Single(descriptions, description => description.RelativePath!.EndsWith("/publish", StringComparison.Ordinal));
        Assert.Contains(publish.ParameterDescriptions, parameter => parameter.Source == BindingSource.Body
            && parameter.Type == typeof(PublishRuntimeConfigurationRequest<RuntimeControls>));
        Assert.Contains(publish.SupportedResponseTypes, response => response.StatusCode == 200
            && response.Type == typeof(PublishRuntimeConfigurationResponse<RuntimeControls>));
    }

    [Fact]
    public async Task HostPolicyAuthenticationSchemes_DoNotChangeExistingMachineDefaults()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment, configure: services =>
        {
            services.AddAuthentication().AddCookie("unrelated-machine");
            services.Configure<AuthenticationOptions>(options => options.DefaultAuthenticateScheme = "unrelated-machine");
            services.AddAuthorization(options =>
            {
                options.AddPolicy(RuntimeConfigurationManagementPolicies.View,
                    policy => policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme).RequireClaim("permission", "view"));
                options.AddPolicy(RuntimeConfigurationManagementPolicies.Edit,
                    policy => policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme).RequireClaim("permission", "edit"));
                options.AddPolicy(RuntimeConfigurationManagementPolicies.Publish,
                    policy => policy.AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme).RequireClaim("permission", "publish"));
            });
        });
        using var client = Client(host);
        using var anonymous = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(anonymous.Headers.Location);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        using var response = await client.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("unrelated-machine", host.Services.GetRequiredService<IOptions<AuthenticationOptions>>().Value.DefaultAuthenticateScheme);
    }

    [Theory]
    [InlineData("application")]
    [InlineData("environment")]
    [InlineData("contract")]
    public async Task ArbitraryScopeCannotBeSelected(string part)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        var route = part switch
        {
            "application" => Route.Replace("test-application", "other-application", StringComparison.Ordinal),
            "environment" => Route.Replace(_environment, "other-environment", StringComparison.Ordinal),
            _ => Route.Replace("controls", "other-contract", StringComparison.Ordinal),
        };
        foreach (var suffix in new[] { "", "/antiforgery", "/preview", "/publish" })
        {
            using var response = suffix is "/publish" or "/preview"
                ? await client.PostAsJsonAsync(route + suffix, Publication(), TestContext.Current.CancellationToken)
                : await client.GetAsync(route + suffix, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("bad")]
    [InlineData("other-principal")]
    public async Task JsonMutationsRequirePrincipalBoundAntiforgery(string scenario)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        await SignInAsync(client);
        var token = await TokenAsync(client, Route);
        if (scenario == "other-principal")
            await SignInAsync(client, actor: "second-user");
        else
        {
            client.DefaultRequestHeaders.Remove(token.HeaderName);
            if (scenario == "bad")
                client.DefaultRequestHeaders.Add(token.HeaderName, "invalid-token");
        }
        foreach (var suffix in new[] { "preview", "publish" })
        {
            using var response = await client.PostAsJsonAsync($"{Route}/{suffix}", Publication(), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("antiforgery", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{")]
    [InlineData("""{"value":{},"reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":-1,"reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":1.5,"reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":"1","reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":9223372036854775808,"reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":1,"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{"verbosity":2,"verbosity":3},"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{"verbosity":99},"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{"unknown":true},"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{"mode":999},"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{"mode":"Undefined"},"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":1,"reason":"valid","actor":"spoofed"}""")]
    [InlineData("""{"value":{},"expectedRevision":1,"reason":""}""")]
    [InlineData("""{"value":{},"expectedRevision":1,"reason":null}""")]
    [InlineData("""{"value":null,"expectedRevision":1,"reason":"valid"}""")]
    [InlineData("""{"value":{},"expectedRevision":1,"reason":"valid","expiresAtUtc":"2000-01-01T00:00:00Z"}""")]
    public async Task MalformedAndInvalidCandidatesNeverAppend(string json)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        using var body = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync($"{Route}/publish", body, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(9_007_199_254_740_993L)]
    [InlineData(long.MaxValue)]
    public async Task ExactIntegralRevisionBoundsRemainConflictsNotRoundedValidation(long revision)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        using var response = await client.PostAsJsonAsync($"{Route}/publish", Publication(revision), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("reason")]
    [InlineData("body")]
    public async Task BoundedMetadataAndBody_RejectWithoutMutation(string invalid)
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        var request = invalid == "reason" ? Publication() with { Reason = new string('x', 501) }
            : Publication() with { Value = new RuntimeControls { Categories = [new string('x', 100_000)] } };
        using var response = await client.PostAsJsonAsync($"{Route}/publish", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    [Fact]
    public async Task HostEnumConverterAndFutureExpiry_ArePreservedWithoutGlobalSerializerChanges()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var host = Factory(_connectionString, _environment);
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        var mvcOptions = host.Services.GetRequiredService<IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>().Value.JsonSerializerOptions;
        Assert.True(mvcOptions.AllowDuplicateProperties);
        Assert.True(mvcOptions.PropertyNameCaseInsensitive);
        using var preview = await client.PostAsJsonAsync($"{Route}/preview",
            new { value = new RuntimeControls { Mode = RuntimeControlMode.Detailed } }, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.Contains("\"mode\":\"Detailed\"", await preview.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var expiry = DateTimeOffset.UtcNow.AddMinutes(10);
        using var publish = await client.PostAsJsonAsync($"{Route}/publish", Publication() with { ExpiresAtUtc = expiry, Reason = new string('x', 500) },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        Assert.Equal(expiry, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.ExpiresAtUtc);
    }

    [Fact]
    public async Task WarmRedisHeadNeverSuppliesManagementExpectedRevision()
    {
        await PublishAsync(new RuntimeControls(), 0);
        _cacheSettings = _cacheSettings with { MaximumAge = TimeSpan.FromMinutes(1), ReconciliationInterval = TimeSpan.FromMinutes(1) };
        await using var host = Factory(_connectionString, _environment, configure: AddCache, timeProvider: new ManualTimeProvider());
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        var cachedReader = host.Services.GetRequiredService<IRuntimeConfigurationStore<RuntimeControls>>();
        Assert.Equal(1, (await cachedReader.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        await PublishAsync(new RuntimeControls { Verbosity = 3 }, 1);
        Assert.Equal(1, (await cachedReader.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        var document = (await client.GetFromJsonAsync<RuntimeConfigurationDocument<RuntimeControls>>(Route, TestContext.Current.CancellationToken))!;
        Assert.Equal(2, document.Revision);
        using var publish = await client.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, publish.StatusCode);
        Assert.Equal(2, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        await DeleteCacheAsync(host.Services);
    }

    [Fact]
    public async Task IndependentHosts_RacingExpectedRevisionHaveOneCommit()
    {
        await PublishAsync(new RuntimeControls(), 0);
        await using var first = Factory(_connectionString, _environment, keyDirectory: KeyDirectory);
        await using var second = Factory(_connectionString, _environment, keyDirectory: KeyDirectory);
        using var a = Client(first);
        using var b = Client(second);
        await SignInAsync(a);
        await SignInAsync(b);
        await TokenAsync(a, Route);
        await TokenAsync(b, Route);
        var responses = await Task.WhenAll(a.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken),
            b.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses)
            response.Dispose();
        Assert.Equal(2, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
        Assert.Null(await _store.ReadRevisionAsync(3, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrUnavailableAuthority_IsSanitizedWithoutCacheFallback(bool unavailable)
    {
        await PublishAsync(new RuntimeControls(), 0);
        var log = new CapturedLog();
        var publisher = new FailedPublisher(unavailable);
        await using var host = Factory(_connectionString, _environment, configure: services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IRuntimeConfigurationPublisher<RuntimeControls>>(publisher));
            services.AddLogging(logging => logging.AddProvider(log));
        });
        using var client = Client(host);
        await SignInAsync(client);
        await TokenAsync(client, Route);
        using var response = await client.GetAsync(Route, TestContext.Current.CancellationToken);
        Assert.Equal(unavailable ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(FailedPublisher.Secret, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        if (unavailable)
        {
            using var publish = await client.PostAsJsonAsync($"{Route}/publish", Publication(), TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, publish.StatusCode);
            Assert.Equal(1, publisher.Attempts);
            Assert.DoesNotContain(FailedPublisher.Secret, await publish.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Contains(log.Messages, entry => entry.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
            Assert.DoesNotContain(log.Messages, entry => entry.Contains(FailedPublisher.Secret, StringComparison.Ordinal));
            Assert.All(log.Exceptions, exception => Assert.Null(exception));
        }
        Assert.Equal(1, (await _store.ReadAsync(TestContext.Current.CancellationToken))!.Revision);
    }

    private sealed class FailedPublisher(bool unavailable) : IRuntimeConfigurationPublisher<RuntimeControls>
    {
        public const string Secret = "sensitive-provider-payload-and-actor";
        public int Attempts { get; private set; }
        public Task<RuntimeConfigurationRevision<RuntimeControls>?> ReadAsync(CancellationToken cancellationToken = default)
            => unavailable ? throw new InvalidOperationException(Secret) : Task.FromResult<RuntimeConfigurationRevision<RuntimeControls>?>(null);
        public Task<RuntimeConfigurationRevision<RuntimeControls>?> ReadRevisionAsync(long revision, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<RuntimeConfigurationRevision<RuntimeControls>> PublishAsync(RuntimeControls value, long expectedRevision,
            string actor, string reason, DateTimeOffset? expiresAtUtc = null, CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new InvalidOperationException(Secret);
        }
    }

    private sealed class CapturedLog : ILoggerProvider, ILogger
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ConcurrentQueue<Exception?> Exceptions { get; } = new();
        public ILogger CreateLogger(string categoryName) => this;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Dispose() { }
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Enqueue(formatter(state, exception));
            Exceptions.Enqueue(exception);
        }
    }
}
