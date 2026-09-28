using System.Diagnostics.CodeAnalysis;

namespace CasCap.Common.Models;

/// <summary>
/// A deterministic check applied to the final answer text of an agent run.
/// </summary>
/// <param name="Description">Human-readable statement of what the answer must contain, used in reports.</param>
/// <param name="IsSatisfiedBy">Returns <see langword="true"/> when the answer satisfies the expectation.</param>
/// <remarks>
/// Deterministic graders keep evaluation cheap and repeatable. They accept any answer that contains the
/// expected value, so design fixtures where the expected number does not also appear as a distractor.
/// </remarks>
public sealed partial record AnswerExpectation(string Description, Func<string, bool> IsSatisfiedBy)
{
    private static readonly string[] NumberWords =
    [
        "zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
        "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen", "twenty",
    ];

    /// <summary>Expects the answer to state <paramref name="expected"/>, as digits or as an English number word.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="tolerance">The permitted absolute difference, for example <c>0.05</c> for one decimal place.</param>
    public static AnswerExpectation Number(double expected, double tolerance = 0d) =>
        new($"states {expected.ToString(CultureInfo.InvariantCulture)}",
            answer => ExtractNumbers(answer).Any(n => Math.Abs(n - expected) <= tolerance));

    /// <summary>Expects the answer to contain at least one of <paramref name="phrases"/>, ignoring case.</summary>
    /// <param name="phrases">Acceptable phrases.</param>
    public static AnswerExpectation ContainsAny(params string[] phrases) =>
        new($"mentions {string.Join(" or ", phrases.Select(p => $"'{p}'"))}",
            answer => phrases.Any(p => answer.Contains(p, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Expects the answer to contain none of <paramref name="phrases"/>, ignoring case.</summary>
    /// <param name="phrases">Phrases that indicate a wrong answer.</param>
    public static AnswerExpectation ContainsNone(params string[] phrases) =>
        new($"never mentions {string.Join(" or ", phrases.Select(p => $"'{p}'"))}",
            answer => !phrases.Any(p => ContainsWord(answer, p)));

    /// <summary>Expects the answer to match a regular expression, ignoring case.</summary>
    /// <param name="pattern">The pattern, for example one anchoring a state to the entity it describes.</param>
    /// <param name="description">Human-readable statement of the expectation.</param>
    public static AnswerExpectation Matches([StringSyntax(StringSyntaxAttribute.Regex)] string pattern, string description) =>
        new(description, answer => Regex.IsMatch(answer, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    /// <summary>Expects every one of <paramref name="expectations"/> to be satisfied.</summary>
    /// <param name="expectations">The expectations to combine.</param>
    public static AnswerExpectation All(params AnswerExpectation[] expectations) =>
        new(string.Join(" and ", expectations.Select(e => e.Description)),
            answer => expectations.All(e => e.IsSatisfiedBy(answer)));

    /// <summary>
    /// Extracts every number written as digits (accepting a decimal comma) or as an English word up to twenty.
    /// </summary>
    /// <param name="text">The text to scan.</param>
    public static IReadOnlyList<double> ExtractNumbers(string text)
    {
        var numbers = new List<double>();
        foreach (Match match in DigitsRegex().Matches(text))
            if (double.TryParse(match.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                numbers.Add(value);

        foreach (Match match in WordRegex().Matches(text))
        {
            var index = Array.IndexOf(NumberWords, match.Value.ToLowerInvariant());
            if (index >= 0)
                numbers.Add(index);
        }

        return numbers;
    }

    private static bool ContainsWord(string text, string phrase) =>
        Regex.IsMatch(text, $@"(?<![\p{{L}}]){Regex.Escape(phrase)}(?![\p{{L}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [GeneratedRegex(@"-?\d+(?:[.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsRegex();

    [GeneratedRegex(@"\b[A-Za-z]+\b", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();

    /// <inheritdoc/>
    public override string ToString() => Description;
}
