using CasCap.Common.Configuration.Runtime.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Unit;

[Trait("Category", "Serialization")]
public sealed class RuntimeOptionsFactoryTests
{
    [Fact]
    public void AcceptedSnapshot_ReplacesInitializedCollectionsAndNulls()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRuntimeConfiguration(
            new RuntimeConfigurationOptions
            {
                Scope = new RuntimeConfigurationScope("test-application", "unit"),
                ContractId = "collection-controls",
            },
            new CollectionControls { Names = [], Label = null, Tags = ["single"] });
        using var provider = services.BuildServiceProvider();

        var value = provider.GetRequiredService<IOptionsMonitor<CollectionControls>>().CurrentValue;

        Assert.Empty(value.Names);
        Assert.Null(value.Label);
        Assert.Equal(["single"], value.Tags);
    }

    public sealed class CollectionControls
    {
        public List<string> Names { get; set; } = ["default"];

        public string? Label { get; set; } = "default";

        public List<string> Tags { get; set; } = ["default"];
    }
}
