using System.Collections.Frozen;
using System.Diagnostics;
using Microsoft.Net.Http.Headers;

namespace NeoIPC.Reporting;

abstract class RScriptReportGenerator(string mediaType, string language, IWebHostEnvironment environment, ILogger logger)
    : ExternalProcessReportGenerator(mediaType, environment, logger)
{
    public static readonly FrozenDictionary<string, MediaTypeHeaderValue> SupportedMediaTypeHeaderValues =
        new[] { "application/json" }.Select(s => new KeyValuePair<string, MediaTypeHeaderValue>(s, new MediaTypeHeaderValue(s)))
            .ToFrozenDictionary(StringComparer.Ordinal);

    protected override ProcessStartInfo GetProcessStartInfo()
    {
        throw new NotImplementedException();
    }
}