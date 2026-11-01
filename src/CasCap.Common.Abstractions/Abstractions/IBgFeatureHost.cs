namespace CasCap.Common.Abstractions;

/// <summary>Marks a hosted service that launches registered <see cref="IBgFeature"/> implementations.</summary>
/// <remarks>
/// Libraries that register an <see cref="IBgFeature"/> without depending on the feature host package can
/// look for this marker among the hosted services and run their feature directly when it is absent.
/// </remarks>
public interface IBgFeatureHost;
