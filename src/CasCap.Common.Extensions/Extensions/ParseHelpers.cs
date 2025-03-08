namespace CasCap.Common.Extensions;

public static class ParseHelpers
{
    public static int GetDecimalCount(this double val)
    {
        var i = 0;
        //Doubles can be rounded to 15 digits max. ref: https://stackoverflow.com/a/33714700/1266873
        while (i < 16 && Math.Round(val, i) != val)
            i++;
        return i;
    }

    public static int GetDecimalCount(this decimal val)
    {
        var i = 0;
        while (Math.Round(val, i) != val)
            i++;
        return i;
    }

    /// <summary>
    /// Use when converting a DateTime value from a string to an actual DateTime.
    /// </summary>
    /// <param name="f">supports 1) Ticks, 2) ISO 8601 & 3) Time without the Date</param>
    /// <param name="date">Pass in the DateOnly here when Ticks string dosn't contain it for brevity.</param>
    public static DateTime csvStr2Date(this string f, DateTime? date = null)
    {
        DateTime dt;
        if (f.Length == 18)//"635990653080800000".Length
            dt = new DateTime(f.decimal2long());
        else if (f.Length == 23 && DateTime.TryParse(f, out var _dt1))//"yyyy-MM-dd HH:mm:ss.fff".Length
            dt = _dt1;
        else if (f.Length == 14)//"63599065308080".Length
            dt = new DateTime(f.decimal2long(4));
        else if (f.Length == 12 && date.HasValue && DateTime.TryParse(date.Value.To_yyyy_MM_dd() + " " + f, out var _dt2))//"HH:mm:ss.fff".Length
            dt = _dt2;
        else
            throw new NotSupportedException("invalid date format string");
        return dt;
    }

    public static string csvDate2Str(this DateTime date) => date.Ticks.ToString();

    private const char _zero = '0';

    /// <summary>
    /// When you know the input value is a string-ified decimal this is the fastest way to parse
    /// that string into the equivalent number.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int decimal2int(this string input, int exp = 0)
    {
        //Debug.WriteLine($"input={input}, input.Length={input.Length}, dp={dp}");
        var decimalExists = false;
        var output = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var digit = input[i];
            if (digit != 46)
            {
                output = output * 10 + (digit - _zero);
                if (decimalExists)
                {
                    //there are decimal places so reduce the dp value accordingly
                    exp--;
                    if (exp == 0) break;
                }
            }
            else
            {
                decimalExists = true;
                if (exp == 0) break;//we don't care about anything after the decimal point
                                    //var strLenRemaining = input.Length - 1 - i;
                                    //Debug.WriteLine($"decimalPos={decimalPos}, strLenRemaining={strLenRemaining}");
            }
            //Debug.WriteLine($"index i={i}, output={output}");
        }
        output = exp switch
        {
            0 => output,
            1 => output *= 10,
            2 => output *= 100,
            3 => output *= 1000,
            4 => output *= 10000,
            _ => output *= Pow(exp)
        };
        //Debug.WriteLine($"output={output}");
        return output;
    }

    public static decimal string2decimal(this string input)//todo: make this fast and not just a bog standard decimal.TryParse
    {
        if (decimal.TryParse(input, out decimal val))
            return val;
        else
            throw new Exception($"{nameof(string2decimal)} issue! :/");
    }

    //make this a generic? - used for new DateTime(tickCount)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long decimal2long(this string input, int exp = 0)
    {
        //Debug.WriteLine($"input={input}, input.Length={input.Length}, dp={dp}");
        var decimalExists = false;
        var output = 0L;
        for (var i = 0; i < input.Length; i++)
        {
            var digit = input[i];
            if (digit != 46)
            {
                output = output * 10 + (digit - _zero);
                if (decimalExists)
                {
                    //there are decimal places so reduce the dp value accordingly
                    exp--;
                    if (exp == 0) break;
                }
            }
            else
            {
                decimalExists = true;
                if (exp == 0) break;//we don't care about anything after the decimal point
                                    //var strLenRemaining = input.Length - 1 - i;
                                    //Debug.WriteLine($"decimalPos={decimalPos}, strLenRemaining={strLenRemaining}");
            }
            //Debug.WriteLine($"index i={i}, output={output}");
        }
        output = exp switch
        {
            0 => output,
            1 => output *= 10,
            2 => output *= 100,
            3 => output *= 1000,
            4 => output *= 10000,
            _ => output *= Pow(exp)
        };
        //Debug.WriteLine($"output={output}");
        return output;
    }

    public static decimal int2decimal(this int input, int exp = 0)//uses of this should be *very* limited
    {
        if (exp > 0)
            return input / (decimal)Pow(exp);
        else
            return input;
    }

    public static double int2double(this int input, int exp = 0)
    {
        if (exp > 0)
            return input / (double)Pow(exp);
        else
            return input;
    }

    //this will be faster than the bitmask variant below
    private static int Pow(int exp) => exp switch
    {
        0 => exp,
        1 => exp *= 10,
        2 => exp *= 100,
        3 => exp *= 1000,
        4 => exp *= 10000,
        _ => exp *= Pow(exp)
    };

    //https://stackoverflow.com/questions/2065249/c-sharp-efficient-algorithm-integer-based-power-function
    //https://stackoverflow.com/questions/936541/math-pow-vs-multiply-operator-performance (slightly wrong)
    private static int Pow(int exp, int num = 10)
    {
        var result = 1;
        while (exp > 0)
        {
            if ((exp & 1) != 0)
                result *= num;
            exp >>= 1;
            num *= num;
        }
        return result;
    }
}
