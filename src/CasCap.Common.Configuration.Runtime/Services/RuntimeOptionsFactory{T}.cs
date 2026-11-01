using CasCap.Common.Configuration.Runtime.Providers;

namespace CasCap.Common.Configuration.Runtime.Services;

/// <summary>
/// Creates options from the accepted typed snapshot so the bound value matches the published revision exactly.
/// </summary>
/// <remarks>
/// <see cref="Microsoft.Extensions.Configuration.ConfigurationBinder"/> drops empty collections and nulls and
/// appends to initialized collections, so binding the flattened snapshot onto a new instance can diverge from
/// the revision the codec validated.
/// </remarks>
internal sealed class RuntimeOptionsFactory<T>(
    RuntimeSnapshotProvider provider,
    RuntimeConfigurationCodec<T> codec,
    IEnumerable<IConfigureOptions<T>> setups,
    IEnumerable<IPostConfigureOptions<T>> postConfigures,
    IEnumerable<IValidateOptions<T>> validations
) : OptionsFactory<T>(setups, postConfigures, validations) where T : class
{
    /// <inheritdoc/>
    protected override T CreateInstance(string name) => name == Options.DefaultName
        ? codec.Deserialize(provider.AcceptedJson)
        : base.CreateInstance(name);
}
