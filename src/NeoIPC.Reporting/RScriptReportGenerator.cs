using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.Net.Http.Headers;

namespace NeoIPC.Reporting;

abstract class RScriptReportGenerator : ExternalProcessReportGenerator
{
    protected RScriptReportGenerator(string mediaType, string language, string sessionId, IWebHostEnvironment environment, ILogger logger) : base(mediaType, environment, logger)
    {
        Language = language;
        SessionId = sessionId;
    }

    public string SessionId { get; }
    public string Language { get; }
    protected abstract IEnumerable<string> GetReportParameters();
    protected abstract string ReportFilePath { get; }

    protected sealed override ProcessStartInfo GetProcessStartInfo()
    {
        return new ProcessStartInfo("Rscript", GetArguments())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            EnvironmentVariables =
            {
                ["NEOIPC_DHIS2_SESSION_ID"] = SessionId,
                ["LANGUAGE="] = "en_GB:en",
                ["LANG"] = "C.utf8",
                ["LC_ALL"] = "C.utf8"
            }
        };

        IEnumerable<string> GetArguments()
        {
            yield return "--vanilla";
            yield return ReportFilePath;
            foreach (var arg in GetReportParameters())
                yield return arg;
        }
    }

    public static readonly FrozenDictionary<string, MediaTypeHeaderValue> SupportedMediaTypeHeaderValues =
        new[] { "application/json" }.Select(s => new KeyValuePair<string, MediaTypeHeaderValue>(s, new MediaTypeHeaderValue(s)))
            .ToFrozenDictionary(StringComparer.Ordinal);
}
