using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace CasCap.Common.Extensions;

public static class EnumExtensions
{
    /// <summary>
    /// Gets all items for an enum value.
    /// </summary>
    public static IEnumerable<TENum> GetAllItems<TENum>() where TENum : Enum => (TENum[])Enum.GetValues(typeof(TENum));

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

    /// <summary>
    /// Handy to find originating method name when debugging.
    /// </summary>
    public static string GetCallingMethodName([CallerMemberName] string caller = "") => caller;

    private static Dictionary<Enum, string> enumStringValues = new();

    public static string ToStringCached(this Enum myEnum)
    {
        string textValue;
        if (enumStringValues.TryGetValue(myEnum, out textValue))
            return textValue;
        else
        {
            textValue = myEnum.ToString();
            enumStringValues[myEnum] = textValue;
            return textValue;
        }
    }

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
}
