using Microsoft.Extensions.Configuration;

namespace CasCap.Common.Hosting.AspNetCore.Tests;

/// <summary>The health-check noise filter applied by <c>AddCasCapDefaults</c> through a real Serilog pipeline.</summary>
[Trait("Category", "HealthCheckFilter")]
public sealed class HealthCheckFilterTests
{
    private const string FrameworkCategory = "Microsoft.Extensions.Diagnostics.HealthChecks.DefaultHealthCheckService";
    private const string ApplicationCategory = "Example.Application.Worker";
    private const string Template = "Synthetic event {Detail}";

    [Theory]
    [InlineData(FrameworkCategory, LogEventLevel.Debug, "Running health checks", false)]
    [InlineData(FrameworkCategory, LogEventLevel.Information, "Health check processing completed", false)]
    [InlineData(FrameworkCategory, LogEventLevel.Warning, "Health check degraded", true)]
    [InlineData(FrameworkCategory, LogEventLevel.Error, "Health check failed", true)]
    [InlineData(ApplicationCategory, LogEventLevel.Information, "HealthCheck mentioned in a property", true)]
    [InlineData(ApplicationCategory, LogEventLevel.Debug, "Unrelated detail", true)]
    public void AddCasCapDefaults_HealthCheckFilter(string category, LogEventLevel level, string detail, bool expectedWritten)
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .AddCasCapDefaults(new ConfigurationBuilder().Build())
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.ForContext(Constants.SourceContextPropertyName, category).Write(level, Template, detail);

        Assert.Equal(expectedWritten, sink.Events.Any(logEvent => logEvent.MessageTemplate.Text == Template));
    }

    [Fact]
    public void IsHealthCheckNoise_WithoutSourceContext()
    {
        var sink = new CapturingSink();
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information(Template, "HealthCheck");

        Assert.False(SerilogExtensions.IsHealthCheckNoise(Assert.Single(sink.Events)));
    }
}
