using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace NeoIPC.Reporting;

sealed class ReferenceReport : QuartoReport
{
    protected override string SessionId { get; }
    protected override string[] ReportParameters { get; }
    protected override string? ResponseContentType { get; }
    protected override string ReportFileName { get; }

    public ReferenceReport(DateOnly? reportingPeriodFrom,
        DateOnly? reportingPeriodTo,
        ushort? birthWeightFrom,
        ushort? birthWeightTo,
        ushort? gestationalAgeFrom,
        ushort? gestationalAgeTo,
        string[] countryFilter,
        string[] hospitalFilter,
        bool? testUnitFilter,
        bool? defaultPatientFilter,
        HttpRequest httpRequest,
        IWebHostEnvironment environment,
        ILogger<ReferenceReport> logger) : base("Reference Report", environment, logger)
    {
        List<string> reportParameters = [];
        if (reportingPeriodFrom.HasValue)
            reportParameters.Add($"reportingPeriodFrom:{reportingPeriodFrom:yyyy-MM-dd}");

        if (reportingPeriodTo.HasValue)
            reportParameters.Add($"reportingPeriodTo:{reportingPeriodTo:yyyy-MM-dd}");

        if (birthWeightFrom.HasValue)
            reportParameters.Add($"birthWeightFrom:{birthWeightFrom}");

        if (birthWeightTo.HasValue)
            reportParameters.Add($"birthWeightTo:{birthWeightTo}");

        if (gestationalAgeFrom.HasValue)
            reportParameters.Add($"gestationalAgeFrom:{gestationalAgeFrom}");

        if (birthWeightTo.HasValue)
            reportParameters.Add($"gestationalAgeTo:{gestationalAgeTo}");

        if (countryFilter.Length > 0)
            reportParameters.Add($"countryFilter:{string.Join(",", countryFilter)}");

        if (hospitalFilter.Length > 0)
            reportParameters.Add($"hospitalFilter:{string.Join(",", hospitalFilter)}");

        if (testUnitFilter != null)
            reportParameters.Add($"testUnitFilter:{testUnitFilter}");

        if (defaultPatientFilter != null)
            reportParameters.Add($"defaultPatientFilter:{defaultPatientFilter}");
        ReportParameters = reportParameters.ToArray();

        var headers = httpRequest.GetTypedHeaders();
        SessionId = headers.Cookie.FirstOrDefault(cookieHeaderValue => cookieHeaderValue is
        { Name: { HasValue: true, Value: "JSESSIONID" }, Value.HasValue: true })
            ?.Value.ToString() ?? throw new ArgumentException("JSESSIONID is missing.");

        var mediaTypeSort = new List<(double Quality, int Location, string? MediaType)>();
        for (var i = 0; i < headers.Accept.Count; i++)
        {
            var requestedMediaTypeHeaderValue = headers.Accept[i];
            var mediaType = requestedMediaTypeHeaderValue.MediaType.ToString();
            var quality = requestedMediaTypeHeaderValue.Quality ?? 1.0;
            if (SupportedMediaTypeHeaderValues.ContainsKey(mediaType))
                mediaTypeSort.Add((quality, i, mediaType));
            else
                mediaTypeSort.AddRange(SupportedMediaTypeHeaderValues.Values
                    .Where(supportedMediaTypeHeaderValue =>
                        supportedMediaTypeHeaderValue.IsSubsetOf(requestedMediaTypeHeaderValue))
                    .Select(mediaTypeHeaderValue =>
                        (quality * 0.9, i, (string?)mediaTypeHeaderValue.MediaType.ToString())));
        }

        ResponseContentType = mediaTypeSort.OrderByDescending(s => s.Quality).ThenBy(s => s.Location).FirstOrDefault().MediaType;

        var acceptLanguageSort = new List<(double Quality, int Location, string? FileName)>();
        // Todo: Look at RFC 4647 and see if we can do better language matching
        for (var i = 0; i < headers.AcceptLanguage.Count; i++)
        {
            var acceptLanguageHeader = headers.AcceptLanguage[i];
            var language = acceptLanguageHeader.Value.ToString();
            var quality = acceptLanguageHeader.Quality ?? 1.0;
            if (TranslatedFiles.TryGetValue(language, out var f))
                acceptLanguageSort.Add((quality, i, f));
            else if (language.Contains('-'))
            {
                var neutralLanguage = language.Split('-')[0];
                if (TranslatedFiles.TryGetValue(neutralLanguage, out f))
                {
                    acceptLanguageSort.Add((quality * 0.9, i, f));
                }
            }
        }

        ReportFileName = acceptLanguageSort.OrderByDescending(s => s.Quality).ThenBy(s => s.Location).FirstOrDefault().FileName ?? "Reference Report.qmd";

    }

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
        [FromServices] IWebHostEnvironment environment,
        [FromServices] ILogger<ReferenceReport> logger,
        HttpRequest httpRequest,
        CancellationToken cancellationToken
    )
    {
       using var report = new ReferenceReport(reportingPeriodFrom, reportingPeriodTo, birthWeightFrom, birthWeightTo, gestationalAgeFrom,
                gestationalAgeTo, countryFilter, hospitalFilter, testUnitFilter, defaultPatientFilter, httpRequest, environment, logger);

       return await report.Render(cancellationToken);
    }

    private static readonly FrozenDictionary<string, string> TranslatedFiles;
    private static readonly FrozenDictionary<string, MediaTypeHeaderValue> SupportedMediaTypeHeaderValues =
        new[] { "application/json", "text/html", "application/pdf" }.Select(s => new KeyValuePair<string, MediaTypeHeaderValue>(s, new MediaTypeHeaderValue(s)))
            .ToFrozenDictionary(StringComparer.Ordinal);

    static ReferenceReport()
    {

        var baseDir = new DirectoryInfo("/reports/Reference Report");
        var t = new Dictionary<string, string> { { "en", "Reference Report.qmd" }, { "en-GB", "Reference Report.qmd" } };

        foreach (var file in baseDir.EnumerateFiles("Reference Report.*.qmd", SearchOption.TopDirectoryOnly))
        {
#pragma warning disable SYSLIB1045
            var locale = Regex.Replace(file.Name, @"Reference Report\.(.+)\.qmd", "$1");
#pragma warning restore SYSLIB1045
            t.Add(locale, file.Name);
        }
        TranslatedFiles = t.ToFrozenDictionary(StringComparer.Ordinal);
    }

}
