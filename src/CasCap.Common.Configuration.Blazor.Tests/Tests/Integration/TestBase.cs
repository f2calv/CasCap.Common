using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CasCap.Common.Configuration.Blazor.Tests.Tests.Integration;

public abstract class TestBase(ITestOutputHelper output) : IDisposable
{
    protected readonly ITestOutputHelper _output = output;
    protected readonly BunitContext _context = new();
    protected readonly ManagementHandler _handler = new();
    private HttpClient? _http;

    protected IRenderedComponent<EditorHarness> Render()
    {
        _context.JSInterop.Mode = JSRuntimeMode.Loose;
        _http = new HttpClient(_handler) { BaseAddress = new Uri("https://example.com") };
        _context.Services.AddLogging(logging => logging.ClearProviders());
        _context.Services.AddSingleton(_http);
        _context.Services.AddSingleton<RuntimeConfigurationClient<EditorControls>>();
        return _context.Render<EditorHarness>();
    }

    public void Dispose()
    {
        _context.Dispose();
        _http?.Dispose();
    }

    protected sealed class ManagementHandler : HttpMessageHandler
    {
        public HttpStatusCode ReadStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode PreviewStatus { get; set; } = HttpStatusCode.OK;
        public HttpStatusCode PublishStatus { get; set; } = HttpStatusCode.OK;
        public bool InvalidSuccess { get; set; }
        public bool TransportFailure { get; set; }
        public TaskCompletionSource<HttpResponseMessage>? PendingRead { get; set; }
        public TaskCompletionSource<bool> ReadCancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Reads { get; private set; }
        public int Previews { get; private set; }
        public int Publications { get; private set; }
        public PublishRuntimeConfigurationRequest<EditorControls>? Published { get; private set; }
        public PreviewRuntimeConfigurationRequest<EditorControls>? Previewed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (TransportFailure)
                throw new HttpRequestException("Private transport information.");
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/antiforgery", StringComparison.Ordinal))
                return Json(new RuntimeConfigurationAntiforgeryResponse("RequestVerificationToken", "test-token"));
            if (request.Method == HttpMethod.Get)
            {
                Reads++;
                if (PendingRead is { } pending)
                {
                    try
                    {
                        return await pending.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        ReadCancelled.TrySetResult(true);
                        throw;
                    }
                }
                if (ReadStatus != HttpStatusCode.OK)
                    return Problem(ReadStatus);
                return InvalidSuccess ? Json(new { }) : Json(Document(new EditorControls(), 9007199254740993));
            }
            Assert.Equal("test-token", Assert.Single(request.Headers.GetValues("RequestVerificationToken")));
            if (path.EndsWith("/preview", StringComparison.Ordinal))
            {
                Previews++;
                Previewed = await request.Content!.ReadFromJsonAsync<PreviewRuntimeConfigurationRequest<EditorControls>>(cancellationToken);
                return PreviewStatus == HttpStatusCode.OK ? Json(new PreviewRuntimeConfigurationResponse<EditorControls>(Previewed!.Value))
                    : Problem(PreviewStatus);
            }
            Publications++;
            Published = await request.Content!.ReadFromJsonAsync<PublishRuntimeConfigurationRequest<EditorControls>>(cancellationToken);
            return PublishStatus == HttpStatusCode.OK ? Json(new PublishRuntimeConfigurationResponse<EditorControls>(Document(Published!.Value, Published.ExpectedRevision + 1)))
                : Problem(PublishStatus);
        }

        private static RuntimeConfigurationDocument<EditorControls> Document(EditorControls value, long revision) =>
            new(revision, value, DateTimeOffset.UtcNow, null);

        private static HttpResponseMessage Json<T>(T value) => new(HttpStatusCode.OK) { Content = JsonContent.Create(value) };

        private static HttpResponseMessage Problem(HttpStatusCode status) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                title = "Safe server problem",
                errors = status == HttpStatusCode.BadRequest ? new Dictionary<string, string[]> { ["value.verbosity"] = ["The chosen verbosity is not allowed."] } : [],
            }), System.Text.Encoding.UTF8, "application/problem+json"),
        };
    }
}
