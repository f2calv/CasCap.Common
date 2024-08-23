namespace CasCap.Common.Extensions;

public class Utils
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
}
