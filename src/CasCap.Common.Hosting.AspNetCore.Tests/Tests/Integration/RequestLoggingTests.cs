namespace CasCap.Common.Hosting.AspNetCore.Tests;

/// <summary>Request summaries emitted by <c>UseCasCapRequestLogging</c> through the real Serilog pipeline.</summary>
[Trait("Category", "RequestLogging")]
public sealed class RequestLoggingTests
{
    private const string SyntheticId = "record-4711";
    private const string SyntheticSecret = "synthetic-secret-value";
    private const string RouteProperty = "RequestRoute";
    private const string WidgetReadTemplate = "Widget read";

    [Theory]
    [InlineData($"/api/widgets/{SyntheticId}?token={SyntheticSecret}", LogEventLevel.Information, "/api/widgets/{id}")]
    [InlineData($"/api/fail/{SyntheticId}", LogEventLevel.Error, "/api/fail/{id}")]
    [InlineData($"/missing/{SyntheticId}", LogEventLevel.Debug, "(unmatched)")]
    [InlineData("/healthz", LogEventLevel.Verbose, "/healthz")]
    [InlineData("/healthz/failing", LogEventLevel.Warning, "/healthz/failing")]
    [InlineData("/app.js", LogEventLevel.Verbose, "/app.js")]
    public async Task UseCasCapRequestLogging(string path, LogEventLevel expectedLevel, string expectedRoute)
    {
        var (summary, _, _) = await SendAsync(path);

        Assert.Equal(expectedLevel, summary.Level);
        Assert.Equal(expectedRoute, Assert.IsType<ScalarValue>(summary.Properties[RouteProperty]).Value);
        Assert.False(summary.Properties.ContainsKey("RequestPath"), $"The raw request path must not be logged: {string.Join(", ", summary.Properties.Keys)}");
        var rendered = Render(summary);
        Assert.DoesNotContain(SyntheticId, rendered);
        Assert.DoesNotContain(SyntheticSecret, rendered);
    }

    [Theory]
    [InlineData("/api/widgets/1", LogEventLevel.Warning)]
    [InlineData("/api/events", LogEventLevel.Information)]
    public async Task UseCasCapRequestLogging_SlowRequest(string path, LogEventLevel expectedLevel)
    {
        var (summary, _, _) = await SendAsync(path, TimeSpan.Zero);

        Assert.Equal(expectedLevel, summary.Level);
    }

    [Fact]
    public async Task UseCasCapRequestLogging_ExceptionLoggedOnce()
    {
        var (summary, events, _) = await SendAsync($"/api/fail/{SyntheticId}");

        Assert.Null(summary.Exception);
        var failure = Assert.Single(events, logEvent => logEvent.Exception is not null);
        Assert.IsType<InvalidOperationException>(failure.Exception);
    }

    [Fact]
    public async Task UseCasCapRequestLogging_ApplicationEventsOmitRequestPath()
    {
        var (_, events, forwarded) = await SendAsync($"/api/widgets/{SyntheticId}");

        var applicationEvent = Assert.Single(events, logEvent => logEvent.MessageTemplate.Text == WidgetReadTemplate);
        Assert.True(applicationEvent.Properties.ContainsKey("RequestId"), "Events still correlate with their request.");
        Assert.DoesNotContain(SyntheticId, Render(applicationEvent));
        Assert.Contains(forwarded, record => record.StartsWith("Widget read", StringComparison.Ordinal));
        Assert.All(forwarded, record => Assert.DoesNotContain(SyntheticId, record));
    }

    private static async Task<(LogEvent Summary, IReadOnlyCollection<LogEvent> Events, IReadOnlyCollection<string> Forwarded)> SendAsync(
        string path,
        TimeSpan? slowRequestThreshold = null
        )
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var sink = new CapturingSink();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.InitializeSerilog(configureLogger: configuration => configuration.MinimumLevel.Verbose().WriteTo.Sink(sink));
        var forwardedProvider = new CapturingLoggerProvider();
        builder.Logging.AddProvider(forwardedProvider);

        await using var app = builder.Build();
        app.UseCasCapRequestLogging(slowRequestThreshold);
        app.UseExceptionHandler(errorApp => errorApp.Run(context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Task.CompletedTask;
        }));
        app.MapGet("/api/widgets/{id}", (string id, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger(nameof(RequestLoggingTests));
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation(WidgetReadTemplate);
            return TypedResults.Ok(id.Length);
        });
        app.MapGet("/api/fail/{id}", string (string id) => throw new InvalidOperationException("Synthetic failure."));
        app.MapGet("/api/events", () => TypedResults.Text("data: synthetic\n\n", "text/event-stream"));
        app.MapGet("/healthz", () => TypedResults.Ok());
        app.MapGet("/healthz/failing", () => TypedResults.StatusCode(StatusCodes.Status503ServiceUnavailable));
        app.MapGet("/app.js", () => TypedResults.Text(string.Empty, "text/javascript"));
        await app.StartAsync(cancellationToken);

        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path, cancellationToken);

        // The summary is written after the response completes, so wait briefly for it.
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (sink.Events.FirstOrDefault(logEvent => logEvent.Properties.ContainsKey(RouteProperty)) is { } summary)
                return (summary, sink.Events, forwardedProvider.Records);
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }

        Assert.Fail($"No request summary was logged for {path} (status {(int)response.StatusCode}).");
        return default;
    }

    private static string Render(LogEvent logEvent)
    {
        using var writer = new StringWriter();
        logEvent.RenderMessage(writer);
        foreach (var (name, value) in logEvent.Properties)
            writer.Write($" {name}={value}");
        return writer.ToString();
    }
}
