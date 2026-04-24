using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CasCap.Extensions;

/// <summary>Chat client, agent run, and function-calling middleware plus audio transcoding.</summary>
public static partial class AgentExtensions
{
    /// <summary>
    /// Resolves the system instructions for an <see cref="AgentConfig"/> by reading from
    /// <see cref="AgentConfig.InstructionsSource"/> when set — first as an embedded resource name,
    /// then as an absolute file system path — falling back to <see cref="AgentConfig.Instructions"/>.
    /// When <paramref name="aiConfig"/> is supplied, <see cref="AIConfig.InstructionsPrefix"/> and
    /// <see cref="AIConfig.InstructionsSuffix"/> are prepended and appended respectively.
    /// </summary>
    /// <param name="agentConfig">The agent configuration to resolve instructions for.</param>
    /// <param name="instructionsAssembly">
    /// The assembly containing embedded instruction resources. When <see langword="null"/>
    /// the assembly containing <see cref="AgentExtensions"/> is used.
    /// </param>
    /// <param name="aiConfig">
    /// Optional root AI configuration supplying shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/>. When <see langword="null"/> no wrapping is applied.
    /// </param>
    /// <returns>The resolved, non-empty instructions string.</returns>
    /// <exception cref="FileNotFoundException">
    /// <see cref="AgentConfig.InstructionsSource"/> is set but could not be found as an embedded resource or file.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Neither <see cref="AgentConfig.Instructions"/> nor <see cref="AgentConfig.InstructionsSource"/> yields a value.
    /// </exception>
    public static string ResolveInstructions(AgentConfig agentConfig, Assembly? instructionsAssembly = null, AIConfig? aiConfig = null)
    {
        var instructions = agentConfig.Instructions;
        if (!string.IsNullOrWhiteSpace(agentConfig.InstructionsSource))
        {
            var source = agentConfig.InstructionsSource;

            // 1) Try embedded resource.
            instructions = (instructionsAssembly ?? typeof(AgentExtensions).Assembly)
                .GetManifestResourceString(source);

            // 2) Fall back to file system.
            if (string.IsNullOrWhiteSpace(instructions) && File.Exists(source))
                instructions = File.ReadAllText(source);

            if (string.IsNullOrWhiteSpace(instructions))
                throw new FileNotFoundException(
                    $"InstructionsSource '{source}' not found as an embedded resource or file.", source);
        }
        if (string.IsNullOrWhiteSpace(instructions))
            throw new NotSupportedException(
                $"either {nameof(agentConfig.Instructions)} or {nameof(agentConfig.InstructionsSource)} must be set");
        return WrapInstructions(instructions, aiConfig);
    }

    /// <summary>
    /// Wraps agent-specific instructions with the shared <see cref="AIConfig.InstructionsPrefix"/>
    /// and <see cref="AIConfig.InstructionsSuffix"/> when configured.
    /// </summary>
    internal static string WrapInstructions(string agentInstructions, AIConfig? aiConfig)
    {
        if (aiConfig is null)
            return agentInstructions;

        var prefix = aiConfig.InstructionsPrefix;
        var suffix = aiConfig.InstructionsSuffix;
        if (string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(suffix))
            return agentInstructions;

        return string.Concat(
            string.IsNullOrWhiteSpace(prefix) ? string.Empty : prefix + " ",
            agentInstructions,
            string.IsNullOrWhiteSpace(suffix) ? string.Empty : " " + suffix);
    }

    #region middleware

    /// <summary>
    /// Chat-client-level response middleware that logs the inbound request message count and outbound response message count.
    /// </summary>
    internal static async Task<ChatResponse> ChatResponseMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        CancellationToken cancellationToken)
    {
        Log.Information("{ClassName} chat request, messageCount={Count}", nameof(AgentExtensions), messages.Count());
        var response = await innerClient.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
        Log.Information("{ClassName} chat response, messageCount={Count}, hasUsage={HasUsage}, inputTokens={InputTokens}, outputTokens={OutputTokens}",
            nameof(AgentExtensions), response.Messages.Count,
            response.Usage is not null,
            response.Usage?.InputTokenCount,
            response.Usage?.OutputTokenCount);

        // Accumulate usage across multiple chat round-trips (tool-call loops).
        // ChatClientAgent calls IChatClient.GetResponseAsync for each round-trip,
        // but only the Messages are surfaced through AgentRunResponse — the
        // ChatResponse.Usage property is lost. We accumulate it here via AsyncLocal
        // so RunAnalysisAsync can read the aggregate.
        if (response.Usage is not null)
        {
            var prev = _accumulatedUsage.Value;
            _accumulatedUsage.Value = new UsageDetails
            {
                InputTokenCount = (prev?.InputTokenCount ?? 0) + (response.Usage.InputTokenCount ?? 0),
                OutputTokenCount = (prev?.OutputTokenCount ?? 0) + (response.Usage.OutputTokenCount ?? 0),
                TotalTokenCount = (prev?.TotalTokenCount ?? 0) + (response.Usage.TotalTokenCount ?? 0),
            };
        }

        return response;
    }

    /// <summary>
    /// Chat-client-level streaming response middleware that logs the inbound request message count and total streamed update count.
    /// Also accumulates <see cref="UsageContent"/> from streamed updates into <see cref="_accumulatedUsage"/>
    /// so that <see cref="RunAnalysisAsync"/> can report token counts even when the agent framework uses streaming internally.
    /// </summary>
    internal static async IAsyncEnumerable<ChatResponseUpdate> ChatStreamingResponseMiddleware(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        IChatClient innerClient,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Debug("{ClassName} streaming chat message count={Count}", nameof(AgentExtensions), messages.Count());
        var updateCount = 0;
        await foreach (var update in innerClient.GetStreamingResponseAsync(messages, options, cancellationToken))
        {
            updateCount++;

            // Capture usage from streaming updates (typically the final chunk contains token counts).
            if (update.Contents is not null)
            {
                foreach (var content in update.Contents)
                {
                    if (content is UsageContent uc && uc.Details is not null)
                    {
                        var prev = _accumulatedUsage.Value;
                        _accumulatedUsage.Value = new UsageDetails
                        {
                            InputTokenCount = (prev?.InputTokenCount ?? 0) + (uc.Details.InputTokenCount ?? 0),
                            OutputTokenCount = (prev?.OutputTokenCount ?? 0) + (uc.Details.OutputTokenCount ?? 0),
                            TotalTokenCount = (prev?.TotalTokenCount ?? 0) + (uc.Details.TotalTokenCount ?? 0),
                        };
                        Log.Debug("{ClassName} streaming usage captured: input={InputTokens}, output={OutputTokens}",
                            nameof(AgentExtensions), uc.Details.InputTokenCount, uc.Details.OutputTokenCount);
                    }
                }
            }

            yield return update;
        }
        Log.Debug("{ClassName} streaming chat update count={Count}", nameof(AgentExtensions), updateCount);
    }

    /// <summary>
    /// Agent run middleware that logs the inbound message count and outbound response message count.
    /// </summary>
    /// <remarks>
    /// See <see href="https://learn.microsoft.com/en-us/agent-framework/agents/middleware/?pivots=programming-language-csharp">Agent middleware documentation</see>.
    /// </remarks>
    internal static async Task<AgentResponse> AgentRunMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken cancellationToken)
    {
        Log.Information("{ClassName} agent run, messageCount={Count}", nameof(AgentExtensions), messages.Count());
        var response = await innerAgent.RunAsync(messages, session, options, cancellationToken).ConfigureAwait(false);
        Log.Information("{ClassName} agent response, messageCount={Count}", nameof(AgentExtensions), response.Messages.Count);
        return response;
    }

    /// <summary>
    /// Agent run streaming middleware that logs the inbound message count and total streamed update count.
    /// </summary>
    internal static async IAsyncEnumerable<AgentResponseUpdate> AgentRunStreamingMiddleware(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Log.Debug("{ClassName} streaming agent message count={Count}", nameof(AgentExtensions), messages.Count());
        List<AgentResponseUpdate> updates = [];
        await foreach (var update in innerAgent.RunStreamingAsync(messages, session, options, cancellationToken))
        {
            updates.Add(update);
            yield return update;
        }
        Log.Debug("{ClassName} streaming agent update count={Count}", nameof(AgentExtensions), updates.ToAgentResponse().Messages.Count);
    }

    /// <summary>
    /// Function calling middleware that logs the function name, arguments and result for each tool invocation.
    /// </summary>
    internal static async ValueTask<object?> FunctionCallingMiddleware(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        StringBuilder sb = new();
        sb.Append($"Tool Call: '{context.Function.Name}'");
        if (context.Arguments.Count > 0)
            sb.Append($" (Args: {string.Join(",", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))})");
        Log.Information("{ClassName} {FunctionCallDetails}",
            nameof(AgentExtensions), sb);

        object? result;
        try
        {
            result = await next(context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{ClassName} Function {FunctionName} threw an exception",
                nameof(AgentExtensions), context.Function.Name);
            return $"Error: tool '{context.Function.Name}' failed — {ex.GetType().Name}: {ex.Message}";
        }

        // Strip image blobs to prevent context overflow.
        // Image bytes serialised as base64 text in FunctionResultContent consume massive token counts
        // (a 30 KB JPEG → ~42 K chars → ~32 K tokens). Extract the image as an ambient attachment and
        // return metadata-only so the LLM sees a compact result instead of raw base64 text.
        if (result is JsonElement je
            && je.ValueKind is JsonValueKind.Object
            && je.TryGetProperty("hasImage", out var hasImg) && hasImg.GetBoolean()
            && je.TryGetProperty("bytes", out var bytesEl) && bytesEl.ValueKind is JsonValueKind.String)
        {
            var base64 = bytesEl.GetString();
            if (!string.IsNullOrEmpty(base64))
            {
                var fileName = je.TryGetProperty("blobName", out var nameProp) ? nameProp.GetString() : null;
                var sizeKb = base64.Length * 3 / 4 / 1024;

                Log.Information("{ClassName} stripped image blob from tool result {FunctionName} (~{SizeKb}KB), stored as ambient attachment",
                    nameof(AgentExtensions), context.Function.Name, sizeKb);

                _ambientAttachments.Value?.Add(new AgentRunAttachment
                {
                    Base64Content = base64,
                    MimeType = "image/jpeg",
                    FileName = fileName,
                });

                // Return compact metadata-only result for the LLM.
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    hasImage = true,
                    blobName = fileName,
                    sizeInBytes = base64.Length * 3 / 4,
                    note = $"Image captured (~{sizeKb}KB JPEG). The image will be delivered to the user as an attachment.",
                }));
                result = doc.RootElement.Clone();
            }
        }

        var resultPreview = result switch
        {
            string s when s.Length > 500 => $"{s[..500]}... ({s.Length} chars)",
            JsonElement jsonEl => jsonEl.ToString().Length > 500
                ? $"{jsonEl.ToString()[..500]}... ({jsonEl.ToString().Length} chars)"
                : jsonEl.ToString(),
            _ => result?.ToString()
        };
        Log.Debug("{ClassName} Function Call Result: {Result}",
            nameof(AgentExtensions), resultPreview);

        return result;
    }

    #endregion

    /// <summary>
    /// Transcodes audio bytes to 16-bit PCM WAV via <c>ffmpeg</c> (stdin → stdout, no temp files).
    /// Returns <see langword="null"/> if <c>ffmpeg</c> is not installed or the process fails.
    /// </summary>
    public static async Task<byte[]?> TranscodeToWavAsync(byte[] inputBytes, CancellationToken cancellationToken)
    {
        // -i pipe:0        read from stdin
        // -f wav           output WAV format
        // -ar 16000        16 kHz sample rate (Whisper native)
        // -ac 1            mono
        // -sample_fmt s16  16-bit PCM
        // pipe:1           write to stdout
        const string args = "-i pipe:0 -f wav -ar 16000 -ac 1 -sample_fmt s16 pipe:1 -loglevel error";

        var (output, error, exitCode) = await ShellExtensions.RunProcessWithStdinAsync(
            "ffmpeg", args, inputBytes, cancellationToken);

        if (exitCode != 0)
        {
            Log.Warning("{ClassName} ffmpeg exited with code {ExitCode}: {StdErr}",
                nameof(AgentExtensions), exitCode, error);
            return null;
        }

        return output.Length > 0 ? output : null;
    }
}
