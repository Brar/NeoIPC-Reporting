using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using NeoIPC.Reporting;
using System.Collections.Immutable;

class ReferenceReport
{
    public static async Task<IResult> Get(
        [FromQuery] DateOnly? reportingPeriodFrom,
        [FromQuery] DateOnly? reportingPeriodTo,
        [FromQuery] ushort? birthWeightFrom,
        [FromQuery] ushort? birthWeightTo,
        [FromQuery] ushort? gestationalAgeFrom,
        [FromQuery] ushort? gestationalAgeTo,
        [FromQuery] string[] countryFilter,
        [FromQuery] string[] hospitalFilter,
        [FromQuery] bool? testUnitFilter,
        [FromQuery] bool? defaultPatientFilter,
        [FromQuery] bool? save,
        [FromServices] IWebHostEnvironment environment,
        [FromServices] ILogger<ReferenceReport> logger,
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        var referenceReportParameters = new ReferenceReportParameters(
            reportingPeriodFrom,
            reportingPeriodTo,
            birthWeightFrom,
            birthWeightTo,
            gestationalAgeFrom,
            gestationalAgeTo,
            countryFilter,
            hospitalFilter,
            testUnitFilter,
            defaultPatientFilter,
            save,
            httpRequest);

        if (referenceReportParameters.AcceptHeaders == null || referenceReportParameters.AcceptLanguageHeaders == null)
            return Results.StatusCode(406);

        await using var generator = GetReportGenerator(referenceReportParameters, environment, logger);
        if (generator == null)
            return Results.StatusCode(415);
        var dataResult = await generator.Generate(cancellationToken);
        return dataResult.Result;
    }

    static IDataGenerator? GetReportGenerator(ReferenceReportParameters referenceReportParameters, IWebHostEnvironment environment, ILogger<ReferenceReport> logger)
    {
        var acceptHeaders = referenceReportParameters.AcceptHeaders;
        var acceptLanguageHeaders = referenceReportParameters.AcceptLanguageHeaders;
        if (acceptHeaders.IsDefaultOrEmpty || acceptLanguageHeaders.IsDefaultOrEmpty)
            return null;

        // Format has priority over language
        foreach (var acceptHeader in acceptHeaders)
        {
            var mediaType = acceptHeader.MediaType.ToString();
            if (QuartoReportGenerator.SupportedMediaTypeHeaderValues.ContainsKey(mediaType))
            {
                var generator = FindByLanguages(acceptLanguageHeaders,
                    language => QuartoReferenceReportGenerator.SupportedLanguageDictionary.ContainsKey(language),
                    language => new QuartoReferenceReportGenerator(mediaType, language, referenceReportParameters, environment, logger));
                if (generator is not null) return generator;
            }

            if (RScriptReportGenerator.SupportedMediaTypeHeaderValues.ContainsKey(mediaType))
            {
                var generator = FindByLanguages(acceptLanguageHeaders,
                    language => RScriptReferenceReportGenerator.SupportedLanguageDictionary.ContainsKey(language),
                    language => new RScriptReferenceReportGenerator(mediaType, language, referenceReportParameters, environment, logger));
                if (generator != null) return generator;
            }
        }

        // Subset matches next
        foreach (var acceptHeader in acceptHeaders)
        foreach (var mediaType in ReturnMediaTypePriorityList)
        {
            if (QuartoReportGenerator.SupportedMediaTypeHeaderValues.TryGetValue(mediaType, out var quartoValue) &&
                quartoValue.IsSubsetOf(acceptHeader))
            {
                var generator = FindByLanguages(acceptLanguageHeaders,
                    language => QuartoReferenceReportGenerator.SupportedLanguageDictionary.ContainsKey(language),
                    language => new QuartoReferenceReportGenerator(mediaType, language, referenceReportParameters, environment, logger));
                if (generator != null) return generator;
            }

            if (RScriptReportGenerator.SupportedMediaTypeHeaderValues.TryGetValue(mediaType, out var rScriptValue) &&
                rScriptValue.IsSubsetOf(acceptHeader))
            {
                var generator = FindByLanguages(acceptLanguageHeaders,
                    language => RScriptReferenceReportGenerator.SupportedLanguageDictionary.ContainsKey(language),
                    language => new RScriptReferenceReportGenerator(mediaType, language, referenceReportParameters, environment, logger));
                if (generator != null) return generator;
            }
        }

        return null;

        // Helper to find a generator based on language preferences. It tries exact matches first, then "neutral" matches (language before '-').
        static IDataGenerator? FindByLanguages(ImmutableArray<StringWithQualityHeaderValue> languages, Func<string, bool> isSupportedLanguage, Func<string, IDataGenerator> factory)
        {
            // Exact matches first
            foreach (var lang in languages)
            {
                var language = lang.Value.ToString();
                if (isSupportedLanguage(language))
                    return factory(language);
            }

            // Neutral matches next
            foreach (var lang in languages)
            {
                var parts = lang.Value.ToString().Split('-');
                if (parts.Length < 2) continue;
                var neutralLanguage = parts[0];
                if (isSupportedLanguage(neutralLanguage))
                    return factory(neutralLanguage);
            }

            return null;
        }
    }

    public static IEnumerable<MediaTypeHeaderValue> SortHeaders(IList<MediaTypeHeaderValue> headers)
        => headers
            .Select((h, index) => (h.MediaType, Quality: h.Quality ?? 1.0, Index: index, Value: h))
            .Where(h => h.MediaType.HasValue)
            .OrderByDescending(h => h.Quality).ThenBy(h => h.Index)
            .Select(h => h.Value);

    public static IEnumerable<StringWithQualityHeaderValue> SortHeaders(IList<StringWithQualityHeaderValue> headers)
        => headers
            .Select((h, index) => (Quality: h.Quality ?? 1.0, Index: index, Language: h.Value, Value: h))
            .Where(h => h.Language.HasValue)
            .OrderByDescending(h => h.Quality).ThenBy(h => h.Index)
            .Select(h => h.Value);

    // Priority list for return media types when doing subset matches.
    // Higher priority types are checked first.
    // This helps ensure that, for example, HTML is returned when multiple types are acceptable
    // even if the client did not explicitly request it in their Accept header.
    // This list must contain all media types we want to consider for subset matching.
    static readonly ImmutableArray<string> ReturnMediaTypePriorityList = ["text/html", "application/json", "application/pdf"];

    static ReferenceReport()
        => Directory.CreateDirectory("/home/app/NeoIPC/ReferenceData", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
}
