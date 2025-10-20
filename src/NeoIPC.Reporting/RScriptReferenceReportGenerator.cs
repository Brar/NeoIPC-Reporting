using System.Collections.Frozen;

namespace NeoIPC.Reporting;

sealed class RScriptReferenceReportGenerator(string mediaType, string language, IWebHostEnvironment environment, ILogger logger)
    : RScriptReportGenerator(mediaType, language, environment, logger)
{
    protected sealed override ValueTask<DataResult> HandleError(int processId, int exitCode, Stream stdOutBuffer, string stdErrString,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    protected override string? ReportFileDownloadName { get; }
    public override ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public static FrozenDictionary<string, string> SupportedLanguageDictionary { get; } = new Dictionary<string, string>
        { { "en", "TODO.R" }, { "en-GB", "TODO.R" } }.ToFrozenDictionary();
}