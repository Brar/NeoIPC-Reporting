using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;

namespace NeoIPC.Reporting;

abstract partial class QuartoReportGenerator : ExternalProcessReportGenerator
{
    readonly DirectoryInfo _workingDirectory;
    readonly string _quartoLogFilePath;
    public string SessionId { get; }
    public string Language { get; }

    protected QuartoReportGenerator(string reportSourceDir, string mediaType, string language, string sessionId, IWebHostEnvironment environment, ILogger logger)
        : base(mediaType, environment, logger)
    {
        Language = language;
        SessionId = sessionId;

        var srcDir = new DirectoryInfo(reportSourceDir);
        if (!srcDir.Exists)
            throw new DirectoryNotFoundException($"Report directory '{srcDir.FullName}' not found.");

        var attempts = 0;
        const int maxAttempts = ushort.MaxValue;
        DirectoryInfo? reportDir = null;
        do
        {
            attempts++;
            var reportDirName  = Path.Join(ReportsTempDir, $"quarto_report_{Path.GetRandomFileName()}");
            if (Directory.Exists(reportDirName))
                continue;
            reportDir = new DirectoryInfo(reportDirName);
            reportDir.Create();
            break;
        } while (attempts < maxAttempts);

        if (reportDir == null)
            throw new Exception("Failed to create an temporary directory.");

        Parallel.ForEach(srcDir.EnumerateDirectories("*", SearchOption.AllDirectories),
            srcChild => Directory.CreateDirectory(Path.Join(reportDir.FullName,
                Path.GetRelativePath(srcDir.FullName, srcChild.FullName))));
        Parallel.ForEach(srcDir.EnumerateFiles("*", SearchOption.AllDirectories),
            srcFile =>
            {
                if (srcFile.Name != ".gitignore")
                    File.CreateSymbolicLink(
                        Path.Join(reportDir.FullName, Path.GetRelativePath(srcDir.FullName, srcFile.FullName)),
                        srcFile.FullName);
            });
        _workingDirectory = reportDir;
        _quartoLogFilePath = Path.Join(reportDir.FullName, "quarto-log.json");
    }

    protected abstract IEnumerable<string> GetReportParameters();
    protected abstract string ReportFileName{ get; }

    protected sealed override ProcessStartInfo GetProcessStartInfo()
    {
        return new ProcessStartInfo("quarto", GetArguments())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _workingDirectory.FullName,
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
            yield return "render";
            yield return ReportFileName;

            yield return "--log";
            yield return _quartoLogFilePath;

            yield return "--log-level";
            if (Environment.IsDevelopment())
                yield return "debug";
            else
                yield return "warning";

            yield return "--log-format";
            yield return "json-stream";

            yield return "--quiet";
            yield return "--to";
            switch (MediaType)
            {
                case "text/html":
                    yield return "html";
                    yield return "--embed-resources";
                    yield return "--profile";
                    yield return "minimal";
                    break;
                case "application/pdf":
                    yield return "pdf";
                    yield return "--pdf-engine=lualatex";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            foreach (var arg in GetReportParameters())
            {
                yield return "-P";
                yield return arg;
            }

            yield return "--output";
            yield return "-";
        }
    }

    protected sealed override async ValueTask<DataResult> HandleError(int processId, int exitCode, Stream stdOutBuffer,
        string stdErrString, CancellationToken cancellationToken)
    {
        var success = false;
        if (!string.IsNullOrWhiteSpace(stdErrString))
            Logger.LogDebug(stdErrString);

        if (!File.Exists(_quartoLogFilePath))
            return new DataResult(detail: "The Quarto log file does not exist.", statusCode: 500,
                showMessage: Environment.IsDevelopment());

        // Get the minimum log level that is currently enabled
        var minLevel = LogLevel.None;
        for (var i = LogLevel.Trace; i < LogLevel.Critical; i++)
            if (Logger.IsEnabled(i))
            {
                minLevel = i;
                break;
            }

        var previousLogLevel = LogLevel.None;
        var sb = new StringBuilder();
        var jsonData = new JsonArray();
        await foreach (var line in File.ReadLinesAsync(_quartoLogFilePath, cancellationToken))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var jsonLine = JsonNode.Parse(line);
            jsonData.Add(jsonLine);

            if (jsonLine is not JsonObject jsonObject ||
                !jsonObject.TryGetPropertyValue("levelName", out var levelNode) ||
                !jsonObject.TryGetPropertyValue("msg", out var messageNode))
                continue;

            var message = messageNode?.ToString();
            if (string.IsNullOrWhiteSpace(message))
                continue;

            var currentLogLevel = levelNode?.ToString() switch
            {
                "INFO" => LogLevel.Information,
                "WARNING" => LogLevel.Warning,
                "ERROR" => LogLevel.Error,
                "CRITICAL" => LogLevel.Critical,
                _ => LogLevel.Debug
            };

            // Special case for quarto bug
            // See: https://github.com/quarto-dev/quarto-cli/issues/13394
            if (exitCode == 1 &&
                currentLogLevel == LogLevel.Error &&
                QuartoIssue13394DetectionRegex().IsMatch(message))
            {
                if (sb.Length > 0)
                    Logger.Log(previousLogLevel, "Quarto render process {QuartoRenderProcessId}: {Message}", processId,
                        sb.ToString());

                Logger.LogTrace(
                    "Quarto render process {QuartoRenderProcessId}: Hit well-known Quarto bug (https://github.com/quarto-dev/quarto-cli/issues/13394)\n{ Message}",
                    processId, message);
                sb.Length = 0;
                success = true;
                continue;
            }

            if (currentLogLevel < minLevel)
                continue;

            if (previousLogLevel != currentLogLevel)
            {
                Logger.Log(previousLogLevel, "Quarto render process {QuartoRenderProcessId}: {Message}", processId, sb.ToString());
                previousLogLevel = currentLogLevel;
                sb.Length = 0;
            }

            sb.AppendLine(message);
        }

        if (sb.Length > 0)
            Logger.Log(previousLogLevel, "Quarto render process {QuartoRenderProcessId}: {Message}", processId, sb.ToString());

        return success
            ? DataResult.SimpleSuccess
            : new DataResult(
                title: "Quarto Error",
                detail: "An error occurred while executing Quarto to create a report",
                statusCode: 500,
                extensions: new Dictionary<string, object?> { { "quartoLog", jsonData } },
                showMessage: Environment.IsDevelopment());
    }

    public override ValueTask DisposeAsync()
    {
        if (_workingDirectory.Exists)
            _workingDirectory.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }


    protected const string ReportsSourceDir = "/reports";
    protected static readonly string ReportsTempDir;

    [GeneratedRegex(@"NotFound: No such file or directory \(os error 2\): rename '.+?(/|\\)-' -> '.+?(/|\\)_output(/|\\)-'")]
    private static partial Regex QuartoIssue13394DetectionRegex();

    public static bool IsMediaTypeSupported(string mediaType)
        => SupportedMediaTypeHeaderValues.ContainsKey(mediaType) || SupportedMediaTypeHeaderValues.Values.Any(v => v.IsSubsetOf(new MediaTypeHeaderValue(mediaType)));

    public static readonly FrozenDictionary<string, MediaTypeHeaderValue> SupportedMediaTypeHeaderValues =
        new[] { "text/html", "application/pdf" }.Select(s => new KeyValuePair<string, MediaTypeHeaderValue>(s, new MediaTypeHeaderValue(s)))
            .ToFrozenDictionary(StringComparer.Ordinal);

    static QuartoReportGenerator()
    {
        var sourceReportsDir = new DirectoryInfo(ReportsSourceDir);
        if (!sourceReportsDir.Exists)
            throw new DirectoryNotFoundException($"Report directory '{sourceReportsDir.FullName}' not found.");

        var tempReportsDir = new DirectoryInfo(Path.Join(Path.GetTempPath(), $"{nameof(NeoIPC)}.{nameof(Reporting)}"));
        if (!tempReportsDir.Exists)
            tempReportsDir.Create();

        ReportsTempDir = tempReportsDir.FullName;

        var tempFiltersDir = new DirectoryInfo(Path.Join(tempReportsDir.FullName, "filters"));
        if (tempFiltersDir.Exists)
            return;

        tempFiltersDir.Create();

        var srcFiltersDir = new DirectoryInfo(Path.Join(sourceReportsDir.FullName, "filters"));
        if (!srcFiltersDir.Exists)
            throw new DirectoryNotFoundException($"Report directory '{srcFiltersDir.FullName}' not found.");

        Parallel.ForEach(srcFiltersDir.EnumerateDirectories("*", SearchOption.AllDirectories),
            srcChild => Directory.CreateDirectory(Path.Join(tempFiltersDir.FullName,
                Path.GetRelativePath(srcFiltersDir.FullName, srcChild.FullName))));
        Parallel.ForEach(srcFiltersDir.EnumerateFiles("*", SearchOption.AllDirectories),
            srcFile => File.CreateSymbolicLink(
                Path.Join(tempFiltersDir.FullName, Path.GetRelativePath(srcFiltersDir.FullName, srcFile.FullName)),
                srcFile.FullName));
    }
}
