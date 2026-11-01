namespace CasCap.Common.Services.Tests;

public sealed class ApplicationExitStateTests
{
    [Fact, Trait("Category", "Hosting")]
    public void Registration_ProvidesSuccessfulInitialState()
    {
        var services = new ServiceCollection();
        services.AddApplicationExitState();
        using var provider = services.BuildServiceProvider();

        Assert.Equal(0, provider.GetApplicationExitCode());
    }

    [Fact, Trait("Category", "Hosting")]
    public void MarkFailure_IsIdempotent()
    {
        var state = new ApplicationExitState();

        state.MarkFailure();
        state.MarkFailure();

        Assert.Equal(1, state.ExitCode);
    }
}
