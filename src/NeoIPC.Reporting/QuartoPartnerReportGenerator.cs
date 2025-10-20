using Microsoft.AspNetCore.Mvc.Formatters;

namespace NeoIPC.Reporting;

class QuartoPartnerReportGenerator(string mediaType, string language, IWebHostEnvironment environment, ILogger logger) :
    QuartoReportGenerator("", mediaType, "", language, environment, logger)
{
    protected override string? ReportFileDownloadName { get; }

    protected override IEnumerable<string> GetReportParameters()
    {
        throw new NotImplementedException();
    }

    protected override string ReportFileName { get; }

    public static QuartoPartnerReportGenerator Create(string mediaType, string[] acceptedLanguages)
    {
        throw new NotImplementedException();
    }
}