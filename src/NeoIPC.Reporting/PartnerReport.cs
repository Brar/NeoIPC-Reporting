namespace NeoIPC.Reporting;

class PartnerReport : QuartoReport
{
    public PartnerReport(HttpRequest httpRequest, IWebHostEnvironment environment, ILogger logger) :
        base("Partner Report", environment, logger)
    {
        var headers = httpRequest.GetTypedHeaders();
        SessionId = headers.Cookie.FirstOrDefault(cookieHeaderValue => cookieHeaderValue is
                { Name: { HasValue: true, Value: "JSESSIONID" }, Value.HasValue: true })
            ?.Value.ToString() ?? throw new ArgumentException("JSESSIONID is missing.");
        ReportFileName = "Partner Report.qmd";
        ResponseContentType = "application/html";
        ReportParameters = [];

    }
    protected override string SessionId { get; }
    protected override string ReportFileName { get; }
    protected override string? ResponseContentType { get; }
    protected override string[] ReportParameters { get; }
}