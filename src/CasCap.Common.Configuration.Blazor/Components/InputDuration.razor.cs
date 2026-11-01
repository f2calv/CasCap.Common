using Microsoft.AspNetCore.Components.Forms;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace CasCap.Common.Configuration.Blazor.Components;

/// <summary>A typed invariant duration input using the constant hours:minutes:seconds format.</summary>
public sealed partial class InputDuration : InputBase<TimeSpan>
{
    /// <inheritdoc/>
    protected override string FormatValueAsString(TimeSpan value) => value.ToString("c", CultureInfo.InvariantCulture);

    /// <inheritdoc/>
    protected override bool TryParseValueFromString(string? value, out TimeSpan result, [NotNullWhen(false)] out string? validationErrorMessage)
    {
        var valid = TimeSpan.TryParseExact(value, "c", CultureInfo.InvariantCulture, out result);
        validationErrorMessage = valid ? null : "Enter a duration in hours:minutes:seconds format.";
        return valid;
    }
}
