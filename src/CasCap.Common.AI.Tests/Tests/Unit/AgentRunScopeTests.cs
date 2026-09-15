namespace CasCap.Common.AI.Tests.Unit;

/// <summary>
/// Tests for <see cref="AgentRunScope"/>, the per-run state carrier that replaced the ambient
/// delegation, completion, compaction, depth and attachment <see cref="AsyncLocal{T}"/> fields.
/// </summary>
[Trait("Category", "Agent Run Scope")]
public class AgentRunScopeTests
{
    [Fact]
    public void Depth_DefaultsToZero() =>
        Assert.Equal(0, new AgentRunScope().Depth);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void ForSubAgent_IncrementsDepth(int levels)
    {
        var scope = new AgentRunScope();
        for (var i = 0; i < levels; i++)
            scope = scope.ForSubAgent();

        Assert.Equal(levels, scope.Depth);
    }

    /// <summary>Child scopes must inherit host callbacks so delegation notifications keep firing.</summary>
    [Fact]
    public void ForSubAgent_InheritsCallbacks()
    {
        var scope = new AgentRunScope
        {
            OnDelegation = (_, _, _, _) => Task.CompletedTask,
            OnCompletion = (_, _, _, _) => Task.CompletedTask,
            OnCompaction = _ => { },
        };

        var child = scope.ForSubAgent();

        Assert.NotNull(child.OnDelegation);
        Assert.NotNull(child.OnCompletion);
        Assert.NotNull(child.OnCompaction);
    }

    /// <summary>
    /// The payload of the shared-attachment design: an attachment produced several levels deep
    /// must be visible to the top-level scope so it reaches the user.
    /// </summary>
    [Fact]
    public void ForSubAgent_SharesAttachmentsWithParent()
    {
        var root = new AgentRunScope();
        var grandchild = root.ForSubAgent().ForSubAgent();

        grandchild.AddAttachment(new AgentRunAttachment { Base64Content = "abc", MimeType = "image/jpeg" });

        Assert.Single(root.Attachments);
        Assert.Equal("abc", root.Attachments[0].Base64Content);
    }

    [Fact]
    public void DrainAttachments_EmptiesTheSharedCollection()
    {
        var root = new AgentRunScope();
        root.ForSubAgent().AddAttachment(new AgentRunAttachment { Base64Content = "a", MimeType = "image/jpeg" });
        root.AddAttachment(new AgentRunAttachment { Base64Content = "b", MimeType = "image/jpeg" });

        var drained = root.DrainAttachments();

        Assert.Equal(2, drained.Count);
        Assert.Empty(root.Attachments);
    }

    /// <summary>Attachments is a snapshot — mutating it must not corrupt the scope.</summary>
    [Fact]
    public void Attachments_ReturnsSnapshot()
    {
        var scope = new AgentRunScope();
        scope.AddAttachment(new AgentRunAttachment { Base64Content = "a", MimeType = "image/jpeg" });

        var first = scope.Attachments;
        scope.AddAttachment(new AgentRunAttachment { Base64Content = "b", MimeType = "image/jpeg" });

        Assert.Single(first);
        Assert.Equal(2, scope.Attachments.Count);
    }

    /// <summary>Concurrent sub-agent branches must not lose attachments.</summary>
    [Fact]
    public async Task AddAttachment_IsThreadSafe()
    {
        var root = new AgentRunScope();

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() =>
            root.ForSubAgent().AddAttachment(
                new AgentRunAttachment { Base64Content = i.ToString(), MimeType = "image/jpeg" }))));

        Assert.Equal(50, root.Attachments.Count);
    }

    [Fact]
    public void CompactionStats_CarriesAllCounts()
    {
        var stats = new CompactionStats(InputCount: 40, OutputCount: 21, ToolDropped: 15, WindowTrimmed: 4, Target: 20);

        Assert.Equal(40, stats.InputCount);
        Assert.Equal(21, stats.OutputCount);
        Assert.Equal(15, stats.ToolDropped);
        Assert.Equal(4, stats.WindowTrimmed);
        Assert.Equal(20, stats.Target);
    }
}
