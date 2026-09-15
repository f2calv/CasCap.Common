using System.Runtime.CompilerServices;
using System.Text.Json;

namespace CasCap.Common.Extensions;

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
    /// Creates function-calling middleware that traces each tool invocation and strips image blobs
    /// from tool results, closing over the supplied <paramref name="logger"/>.
    /// </summary>
    /// <remarks>
    /// A factory rather than a plain method group because the middleware delegate signature is
    /// fixed by the framework and offers no way to pass a logger.
    /// </remarks>
    internal static Func<AIAgent, FunctionInvocationContext, Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>>, CancellationToken, ValueTask<object?>>
        CreateFunctionCallingMiddleware(ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        return FunctionCallingMiddleware;

        async ValueTask<object?> FunctionCallingMiddleware(
            AIAgent agent,
            FunctionInvocationContext context,
            Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
            CancellationToken cancellationToken)
        {
            if (logger.IsEnabled(LogLevel.Trace))
            {
                StringBuilder sb = new();
                sb.Append($"Tool call: '{context.Function.Name}'");
                if (context.Arguments.Count > 0)
                    sb.Append($" (args: {string.Join(",", context.Arguments.Select(x => $"[{x.Key} = {x.Value}]"))})");
                logger.LogTrace("{FunctionCallDetails}", sb);
            }

            object? result;
            try
            {
                result = await next(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tool {FunctionName} threw an exception", context.Function.Name);
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

                    logger.LogDebug("Stripped image blob from tool result {FunctionName} (~{SizeKb}KB), stored as ambient attachment",
                        context.Function.Name, sizeKb);

                    _ambientAttachments.Value?.Add(new AgentRunAttachment
                    {
                        Base64Content = base64,
                        MimeType = "image/jpeg",
                        FileName = fileName,
                    });

                    // Return compact metadata-only result for the LLM.
                    using var doc = JsonDocument.Parse(new
                    {
                        hasImage = true,
                        blobName = fileName,
                        sizeInBytes = base64.Length * 3 / 4,
                        note = $"Image captured (~{sizeKb}KB JPEG). The image will be delivered to the user as an attachment.",
                    }.ToJson());
                    result = doc.RootElement.Clone();
                }
            }

            if (logger.IsEnabled(LogLevel.Trace))
            {
                var resultPreview = result switch
                {
                    string s when s.Length > 500 => $"{s[..500]}... ({s.Length} chars)",
                    JsonElement jsonEl => jsonEl.ToString().Length > 500
                        ? $"{jsonEl.ToString()[..500]}... ({jsonEl.ToString().Length} chars)"
                        : jsonEl.ToString(),
                    _ => result?.ToString()
                };
                logger.LogTrace("Tool call result: {Result}", resultPreview);
            }

            return result;
        }
    }

    #endregion

    /// <summary>
    /// Transcodes audio bytes to 16-bit PCM WAV via <c>ffmpeg</c> (stdin → stdout, no temp files).
    /// Returns <see langword="null"/> if <c>ffmpeg</c> is not installed or the process fails.
    /// </summary>
    /// <remarks>
    /// Live — called by <c>CommunicationsBgService.TranscribeAudioAsync</c> and by the (dead)
    /// <c>forwardAttachment</c> branch of <see cref="CreateAgentTool"/>.
    /// <para>
    /// TODO (C4): an ffmpeg shell-out is an audio concern sitting in an AI-agent library, and it
    /// imposes an <c>ffmpeg</c> binary on every consumer image. Extract to an injected
    /// <c>IAudioTranscoder</c> owned by the host. Coordinate with the concurrent Signal
    /// audio-handling rework rather than moving it unilaterally.
    /// </para>
    /// </remarks>
    public static async Task<byte[]?> TranscodeToWavAsync(byte[] inputBytes, CancellationToken cancellationToken, ILogger? logger = null)
    {
        // -i pipe:0        read from stdin
        // -f wav           output WAV format
        // -ar 16000        16 kHz sample rate (Whisper native)
        // -ac 1            mono
        // -sample_fmt s16  16-bit PCM
        // pipe:1           write to stdout
        const string args = "-i pipe:0 -f wav -ar 16000 -ac 1 -sample_fmt s16 pipe:1 -loglevel error";

        var (output, error, exitCode) = await ShellExtensions.RunProcessWithStdinAsync(
            "ffmpeg", args, inputBytes, cancellationToken).ConfigureAwait(false);

        if (exitCode != 0)
        {
            (logger ?? NullLogger.Instance).LogWarning("ffmpeg exited with code {ExitCode}: {StdErr}",
                exitCode, error);
            return null;
        }

        return output.Length > 0 ? output : null;
    }
}
