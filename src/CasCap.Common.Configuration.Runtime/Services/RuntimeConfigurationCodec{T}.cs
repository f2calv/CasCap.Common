using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CasCap.Common.Configuration.Runtime.Services;

/// <summary>Enforces typed serialization and complete validation at persistence and activation boundaries.</summary>
public sealed class RuntimeConfigurationCodec<T> where T : class
{
    private readonly IEnumerable<IValidateOptions<T>> _validators;
    private readonly JsonTypeInfo<T> _typeInfo;
    private readonly Action<T> _validate;

    /// <summary>Creates a reflection-based codec for managed applications.</summary>
    [RequiresDynamicCode("Use the constructor accepting generated JSON metadata and an explicit validator for Native AOT.")]
    [RequiresUnreferencedCode("Reflection-based serialization and recursive validation require the complete DTO graph.")]
    public RuntimeConfigurationCodec(IEnumerable<IValidateOptions<T>> validators)
    {
        _validators = validators;
        _typeInfo = (JsonTypeInfo<T>)new JsonSerializerOptions
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32,
        }.GetTypeInfo(typeof(T));
        _validate = value => ValidateObject(value, [with(ReferenceEqualityComparer.Instance)], 0);
    }

    /// <summary>Creates a reflection-free codec with generated metadata and complete DTO validation.</summary>
    /// <remarks>The validator must cover nested objects, collections, enum bounds and cross-property invariants.</remarks>
    public RuntimeConfigurationCodec(
        JsonTypeInfo<T> typeInfo,
        IValidateOptions<T> validator,
        IEnumerable<IValidateOptions<T>> validators
    )
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        ArgumentNullException.ThrowIfNull(validator);
        _validators = validators;
        _typeInfo = (JsonTypeInfo<T>)new JsonSerializerOptions(typeInfo.Options)
        {
            TypeInfoResolver = typeInfo.OriginatingResolver
                ?? throw new ArgumentException("Runtime configuration metadata must have an originating JSON resolver.", nameof(typeInfo)),
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32,
        }.GetTypeInfo(typeof(T));
        _validate = value =>
        {
            var result = validator.Validate(Options.DefaultName, value);
            if (result.Failed)
                throw new ValidationException(string.Join("; ", result.Failures ?? []));
            if (result.Skipped)
                throw new InvalidOperationException("The runtime configuration contract validator must validate the default options name.");
        };
    }

    /// <summary>Serializes a validated value without exposing mutable references to accepted state.</summary>
    public string Serialize(T value)
    {
        Validate(value);
        var json = JsonSerializer.Serialize(value, _typeInfo);
        if (Encoding.UTF8.GetByteCount(json) > 65_536)
            throw new ValidationException("Runtime configuration exceeds the supported snapshot size.");
        return json;
    }

    /// <summary>Deserializes only the registered DTO shape and runs its validators.</summary>
    public T Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > 65_536)
            throw new ValidationException("Runtime configuration exceeds the supported snapshot size.");
        var value = JsonSerializer.Deserialize(json, _typeInfo)
            ?? throw new ValidationException("Runtime configuration must contain a typed object.");
        Validate(value);
        return value;
    }

    private void Validate(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _validate(value);
        var failures = _validators.Select(validator => validator.Validate(Options.DefaultName, value))
            .Where(result => result.Failed)
            .SelectMany(result => result.Failures ?? []).ToArray();
        if (failures.Length != 0)
            throw new OptionsValidationException(Options.DefaultName, typeof(T), failures);
    }

    [RequiresUnreferencedCode("Recursive data annotations inspect every property in the DTO graph.")]
    private static void ValidateObject(object value, HashSet<object> visited, int depth)
    {
        if (value.GetType().IsEnum && !Enum.IsDefined(value.GetType(), value))
            throw new ValidationException("Configuration enum values must be defined.");
        if (value is string || value.GetType().IsValueType)
            return;
        if (depth > 32 || !visited.Add(value))
            throw new ValidationException("Configuration graphs must be bounded and acyclic.");
        Validator.ValidateObject(value, new ValidationContext(value), validateAllProperties: true);
        if (value is System.Collections.IEnumerable collection)
        {
            foreach (var item in collection)
                if (item is not null)
                    ValidateObject(item, visited, depth + 1);
        }
        else
        {
            foreach (var property in value.GetType().GetProperties())
                if (property.CanRead && property.GetIndexParameters().Length == 0
                    && property.GetValue(value) is { } child)
                    ValidateObject(child, visited, depth + 1);
        }
        visited.Remove(value);
    }
}
