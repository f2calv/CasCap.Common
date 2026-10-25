using CasCap.Common.Configuration.Runtime.Services;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CasCap.Common.Configuration.Runtime.Tests.Tests.Unit;

[Trait("Category", "Serialization")]
public sealed class RuntimeConfigurationCodecTests
{
    private readonly RuntimeConfigurationCodec<RuntimeControls> _codec = new([]);

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void RejectsOutOfRange(int verbosity) =>
        Assert.Throws<ValidationException>(() => _codec.Serialize(new RuntimeControls { Verbosity = verbosity }));

    [Fact]
    public void RejectsUnknownMembers() =>
        Assert.Throws<JsonException>(() => _codec.Deserialize("""{"Verbosity":2,"Unknown":true}"""));

    [Fact]
    public void RoundTrip()
    {
        var value = _codec.Deserialize(_codec.Serialize(new RuntimeControls { Verbosity = 4, Categories = ["service"] }));
        Assert.Equal(4, value.Verbosity);
        Assert.Equal(["service"], value.Categories);
    }

    [Fact]
    public void RunsRegisteredValidators()
    {
        var codec = new RuntimeConfigurationCodec<RuntimeControls>(
            [new ValidateOptions<RuntimeControls>(Options.DefaultName, value => value.Verbosity != 3, "Unsupported verbosity.")]);
        Assert.Throws<OptionsValidationException>(() => codec.Serialize(new RuntimeControls { Verbosity = 3 }));
    }

    [Fact]
    public void RejectsOversizedSnapshot() =>
        Assert.Throws<ValidationException>(() => _codec.Serialize(new RuntimeControls { Categories = [new string('a', 65_536)] }));
}
