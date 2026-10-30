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
    public void RejectsInvalidMemberType() =>
        Assert.Throws<JsonException>(() => _codec.Deserialize("""{"Verbosity":"not-an-integer"}"""));

    [Theory]
    [InlineData("""{"Level":99}""")]
    [InlineData("""{"Child":{"Count":-1}}""")]
    [InlineData("""{"Minimum":5,"Maximum":1}""")]
    public void RejectsNestedEnumAndCrossPropertyFailures(string json)
    {
        var codec = new RuntimeConfigurationCodec<ValidatedControls>([]);
        Assert.Throws<ValidationException>(() => codec.Deserialize(json));
    }

    [Fact]
    public void RejectsCycles()
    {
        var value = new ValidatedControls();
        value.Next = value;
        var codec = new RuntimeConfigurationCodec<ValidatedControls>([]);
        Assert.Throws<ValidationException>(() => codec.Serialize(value));
    }
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

    private sealed class ValidatedControls : IValidatableObject
    {
        public DayOfWeek Level { get; set; }

        public NestedControls Child { get; set; } = new();

        public int Minimum { get; set; }

        public int Maximum { get; set; } = 10;

        public ValidatedControls? Next { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (Minimum > Maximum)
                yield return new ValidationResult("Minimum cannot exceed maximum.");
        }
    }

    private sealed class NestedControls
    {
        [Range(0, 5)]
        public int Count { get; set; }
    }
}
