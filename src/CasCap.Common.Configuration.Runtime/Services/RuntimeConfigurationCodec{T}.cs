using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CasCap.Common.Configuration.Runtime.Services;

/// <summary>Enforces typed serialization and complete validation at persistence and activation boundaries.</summary>
public sealed class RuntimeConfigurationCodec<T>(IEnumerable<IValidateOptions<T>> validators) where T : class
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32,
    };

    /// <summary>Serializes a validated value without exposing mutable references to accepted state.</summary>
    public string Serialize(T value)
    {
        Validate(value);
        var json = JsonSerializer.Serialize(value, SerializerOptions);
        if (Encoding.UTF8.GetByteCount(json) > 65_536)
            throw new ValidationException("Runtime configuration exceeds the supported snapshot size.");
        return json;
    }

    /// <summary>Deserializes only the registered DTO shape and runs its validators.</summary>
    public T Deserialize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > 65_536)
            throw new ValidationException("Runtime configuration exceeds the supported snapshot size.");
        var value = JsonSerializer.Deserialize<T>(json, SerializerOptions)
            ?? throw new ValidationException("Runtime configuration must contain a typed object.");
        Validate(value);
        return value;
    }

    private void Validate(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateObject(value, new HashSet<object>(ReferenceEqualityComparer.Instance), 0);
        var failures = validators.Select(validator => validator.Validate(Options.DefaultName, value))
            .Where(result => result.Failed)
            .SelectMany(result => result.Failures ?? []).ToArray();
        if (failures.Length != 0)
            throw new OptionsValidationException(Options.DefaultName, typeof(T), failures);
    }

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
