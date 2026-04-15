using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CasCap.Common.Extensions;

/// <summary>Extension methods for <see cref="System.Enum"/> types.</summary>
public static class EnumExtensions
{
    /// <summary>Gets all items for an enum value.</summary>
    public static IEnumerable<TENum> GetAllItems<TENum>() where TENum : Enum => (TENum[])Enum.GetValues(typeof(TENum));

    /// <summary>Gets all combinations of a flags enum up to twice the highest defined value.</summary>
    /// <typeparam name="TEnum">A flags <see cref="Enum"/> type.</typeparam>
    /// <returns>All possible flag combination values.</returns>
    public static IEnumerable<TEnum> GetAllCombinations<TEnum>() where TEnum : Enum
    {
        var highestEnum = Enum.GetValues(typeof(TEnum)).Cast<int>().Max();
        var upperBound = highestEnum * 2;
        for (var x = 0; x < upperBound; x++)
        {
            var value = (TEnum)(object)x;
            //l.Add(value);
            yield return value;
        }
    }

    /// <summary>Handy to find originating method name when debugging.</summary>
    public static string GetCallingMethodName([CallerMemberName] string caller = "") => caller;

    private static Dictionary<Enum, string> enumStringValues = new();

    /// <summary>Returns the cached string representation of the specified <see cref="Enum"/> value.</summary>
    /// <param name="myEnum">The enum value.</param>
    /// <returns>The cached string representation.</returns>
    public static string ToStringCached(this Enum myEnum)
    {
        if (enumStringValues.TryGetValue(myEnum, out var textValue))
            return textValue;
        else
        {
            textValue = myEnum.ToString();
            enumStringValues[myEnum] = textValue;
            return textValue;
        }
    }

    /// <summary>
    /// Gets the <see cref="System.ComponentModel.DataAnnotations.DisplayAttribute"/> name for the specified <see cref="Enum"/> value.
    /// </summary>
    /// <param name="enumValue">The enum value.</param>
    /// <returns>The display name if defined; otherwise the enum value's string representation.</returns>
    public static string GetDisplayName(this Enum enumValue)
    {
#pragma warning disable CS8603 // Possible null reference return.
#pragma warning disable CS8602 // Dereference of a possibly null reference.
        return enumValue.GetType()
                        .GetMember(enumValue.ToString())
                        .First()
                        .GetCustomAttribute<DisplayAttribute>()
                        .GetName();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8603 // Possible null reference return.
    }

    /// <summary>Determines whether the enum value has any of the specified flags set.</summary>
    /// <typeparam name="TEnum">A flags <see cref="Enum"/> type.</typeparam>
    /// <param name="value">The enum value to test.</param>
    /// <param name="flags">The list of flags to test against.</param>
    /// <returns><see langword="true"/> if any of the specified flags are set; otherwise <see langword="false"/>.</returns>
    public static bool HasFlag<TEnum>(this TEnum value, List<TEnum> flags) where TEnum : Enum
    {
        foreach (var flag in flags)
        {
            if (value.HasFlag(flag))
                return true;
        }
        return false;
    }
}
