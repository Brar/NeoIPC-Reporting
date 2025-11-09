using System.Collections.Frozen;

namespace NeoIPC.Reporting;

sealed class RScriptReferenceReportGenerator : RScriptReportGenerator
{
    readonly ReferenceReportParameters _referenceReportParameters;

    public RScriptReferenceReportGenerator(string mediaType, string language, ReferenceReportParameters referenceReportParameters, IWebHostEnvironment environment, ILogger logger) : base(mediaType, language, referenceReportParameters.SessionId, environment, logger)
    {
        _referenceReportParameters = referenceReportParameters;
    }

    protected sealed override ValueTask<DataResult> HandleError(int processId, int exitCode, Stream stdOutBuffer, string stdErrString,
        CancellationToken cancellationToken)
    {
        return ValueTask.FromResult(new DataResult(stdErrString, statusCode:500, showMessage: Environment.IsDevelopment()));
    }

    protected override string? ReportFileDownloadName { get; }
    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public static FrozenDictionary<string, string> SupportedLanguageDictionary { get; } = new Dictionary<string, string>
        { { "en", "en" }, { "en-GB", "en" } }.ToFrozenDictionary();

    protected override IEnumerable<string> GetReportParameters()
    {
        if (_referenceReportParameters.ReportingPeriodFrom.HasValue)
        {
            yield return "--date-from";
            yield return _referenceReportParameters.ReportingPeriodFrom.Value.ToString("o");
        }

        if (_referenceReportParameters.ReportingPeriodTo.HasValue)
        {
            yield return "--date-to";
            yield return _referenceReportParameters.ReportingPeriodTo.Value.ToString("o");
        }

        if (_referenceReportParameters.BirthWeightFrom.HasValue)
        {
            yield return "--birth-weight-from";
            yield return _referenceReportParameters.BirthWeightFrom.Value.ToString();
        }

        if (_referenceReportParameters.BirthWeightTo.HasValue)
        {
            yield return "--birth-weight-to";
            yield return _referenceReportParameters.BirthWeightTo.Value.ToString();
        }

        if (_referenceReportParameters.GestationalAgeFrom.HasValue)
        {
            yield return "--gestational-age-from";
            yield return _referenceReportParameters.GestationalAgeFrom.Value.ToString();
        }

        if (_referenceReportParameters.GestationalAgeTo.HasValue)
        {
            yield return "--gestational-age-to";
            yield return _referenceReportParameters.GestationalAgeTo.Value.ToString();
        }

        if (!_referenceReportParameters.CountryFilter.IsDefaultOrEmpty)
        {
            yield return "--countries";
            yield return string.Join(",", _referenceReportParameters.CountryFilter);
        }

        if (!_referenceReportParameters.HospitalFilter.IsDefaultOrEmpty)
        {
            yield return "--hospitals";
            yield return string.Join(",", _referenceReportParameters.HospitalFilter);
        }

        if (_referenceReportParameters.Save.HasValue && _referenceReportParameters.Save.Value)
        {
            yield return "--tee";
            yield return Path.Join("/home/app/NeoIPC/ReferenceData", string.Concat(Guid.CreateVersion7().ToString(), ".json"));
        }

        //if (_referenceReportParameters.TestUnitFilter != null)
        //{
        //    yield return "--test-unit";
        //    yield return _referenceReportParameters.TestUnitFilter;
        //}

        //if (_referenceReportParameters.DefaultPatientFilter != null)
        //{
        //    yield return "--default-patient";
        //    yield return _referenceReportParameters.DefaultPatientFilter;
        //}
    }

    protected override string ReportFilePath => "./R/getReferenceData.R";
}