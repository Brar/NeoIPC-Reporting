namespace NeoIPC.Reporting;

/// <summary>
/// Display modes for confidence intervals in rate tables.
/// </summary>
public enum ConfidenceIntervalMode
{
    /// <summary>Show CIs on every metric.</summary>
    All,

    /// <summary>Show CIs on rates only (per-1000-patient-day style); plain counts get no CI.</summary>
    Rate,

    /// <summary>Suppress CI columns entirely.</summary>
    None,
}

/// <summary>
/// Translates the C# enum to the lowercase strings the QMD's
/// <c>includeConfidenceIntervals</c> param expects (<c>"all"</c>,
/// <c>"rate"</c>, <c>"none"</c>). Wired up via
/// <c>[RenderParameter("includeConfidenceIntervals", Converter = typeof(ConfidenceIntervalConverter))]</c>;
/// the source generator emits the conversion call into <c>MapTo()</c>.
/// </summary>
public sealed class ConfidenceIntervalConverter : IQmdValueConverter<ConfidenceIntervalMode?, string?>
{
    public static string? Convert(ConfidenceIntervalMode? input) =>
        input switch
        {
            ConfidenceIntervalMode.All => "all",
            ConfidenceIntervalMode.Rate => "rate",
            ConfidenceIntervalMode.None => "none",
            _ => null,
        };

    /// <summary>
    /// Parses the wire token back to the enum case-insensitively. A null or
    /// empty token yields a <c>null</c> result (the "backend default" the
    /// app sends for an unset selection); a non-empty token that is not a
    /// defined member returns <c>false</c>.
    /// </summary>
    /// <remarks>
    /// The wire format is the lowercase token set (the app's
    /// <c>ConfidenceIntervalModeValues</c> and the vendored report schemas
    /// declare it so), which the default minimal-API enum binding — a
    /// case-sensitive <c>Enum.TryParse</c> against the PascalCase member
    /// names — rejects with a 400. The endpoints therefore bind this query
    /// parameter as a string and run it through this parser. The
    /// <see cref="Enum.IsDefined{TEnum}(TEnum)"/> guard rejects the
    /// out-of-range numeric strings that a bare <c>Enum.TryParse</c> would
    /// otherwise accept (e.g. <c>"99"</c>).
    /// </remarks>
    public static bool TryParse(string? input, out ConfidenceIntervalMode? result)
    {
        result = null;
        if (string.IsNullOrEmpty(input))
            return true;
        if (Enum.TryParse<ConfidenceIntervalMode>(input, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            result = parsed;
            return true;
        }
        return false;
    }
}
