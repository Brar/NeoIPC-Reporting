using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace NeoIPC.Reporting;

abstract class QuartoReport : IDisposable
{
    private bool _disposedValue;
    protected IWebHostEnvironment Environment { get; }
    protected ILogger Logger { get; }
    protected DirectoryInfo ReportDir { get; }
    protected string LogFilePath { get; }
    protected abstract string ReportFileName { get; }
    protected abstract string? ResponseContentType { get; }
    protected abstract string SessionId { get; }
    protected abstract string[] ReportParameters { get; }

    protected virtual string GetFileDownloadBaseName(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(extension));

        return string.Concat(
            "NeoIPC-Surveillance-",
            Path.GetFileNameWithoutExtension(ReportFileName).Replace(' ', '-'),
            "_",
            DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss"),
            extension[0] == '.' ? "" : ".",
            extension);
    }

    protected virtual string? GetFileDownloadName()
    {
        return ResponseContentType switch
        {
            "application/pdf" =>
                GetFileDownloadBaseName("pdf"),
            "application/json" =>
                GetFileDownloadBaseName("json"),
            "text/html" =>
                GetFileDownloadBaseName("html"),
            _ => null
        };
    }

    static QuartoReport()
    {
        var filtersDir = new DirectoryInfo(Path.Join(Path.GetTempPath(), "filters"));
        if (filtersDir.Exists)
            return;

        filtersDir.Create();
        var srcDir = new DirectoryInfo("/reports/filters");
        if (!srcDir.Exists)
            throw new DirectoryNotFoundException($"Report directory '{srcDir.FullName}' not found.");

        Parallel.ForEach(srcDir.EnumerateDirectories("*", SearchOption.AllDirectories),
            srcChild => Directory.CreateDirectory(Path.Join(filtersDir.FullName,
                Path.GetRelativePath(srcDir.FullName, srcChild.FullName))));
        Parallel.ForEach(srcDir.EnumerateFiles("*", SearchOption.AllDirectories),
            srcFile => File.CreateSymbolicLink(
                Path.Join(filtersDir.FullName, Path.GetRelativePath(srcDir.FullName, srcFile.FullName)),
                srcFile.FullName));
    }

    protected QuartoReport(string reportDir, IWebHostEnvironment environment, ILogger logger)
    {
        Environment = environment;
        Logger = logger;
        var srcDir = new DirectoryInfo(Path.Join("/reports", reportDir));
        if (!srcDir.Exists)
            throw new DirectoryNotFoundException($"Report directory '{srcDir.FullName}' not found.");

        ReportDir = Directory.CreateTempSubdirectory("quarto_report_");
        LogFilePath = Path.Join(ReportDir.FullName, "quarto-log.json");

        Parallel.ForEach(srcDir.EnumerateDirectories("*", SearchOption.AllDirectories),
            srcChild => Directory.CreateDirectory(Path.Join(ReportDir.FullName,
                Path.GetRelativePath(srcDir.FullName, srcChild.FullName))));
        Parallel.ForEach(srcDir.EnumerateFiles("*", SearchOption.AllDirectories),
            srcFile =>
            {
                if (srcFile.Name != ".gitignore")
                    File.CreateSymbolicLink(
                        Path.Join(ReportDir.FullName, Path.GetRelativePath(srcDir.FullName, srcFile.FullName)),
                        srcFile.FullName);
            });
    }

    protected async Task<IResult> Render(CancellationToken cancellationToken)
    {
        if (ResponseContentType == null)
            return Results.StatusCode(415);

        var startInfo = new ProcessStartInfo("quarto", GetArguments())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = ReportDir.FullName,
            EnvironmentVariables =
            {
                ["NEOIPC_DHIS2_SESSION_ID"] = SessionId,
                ["LANGUAGE="] = "en_GB:en",
                ["LANG"] = "C.utf8",
                ["LC_ALL"] = "C.utf8"
            }
        };
        using var quartoRenderProcess = Process.Start(startInfo);

        if (quartoRenderProcess == null)
            return Results.InternalServerError();

        var buffer = new MemoryStream();
        var stdOut = quartoRenderProcess.StandardOutput.BaseStream.CopyToAsync(buffer, cancellationToken);
        var stdErr = quartoRenderProcess.StandardError.ReadToEndAsync(cancellationToken);

        await quartoRenderProcess.WaitForExitAsync(cancellationToken);

        var success = false;
        if (quartoRenderProcess.ExitCode != 0)
        {
            var stdErrString = await stdErr;
            if (!string.IsNullOrWhiteSpace(stdErrString))
                Console.WriteLine(stdErrString);
            if (!File.Exists(LogFilePath))
                return Results.InternalServerError();

            var jsonDoc = new JsonArray();
            var minLevel = 6;

            for (var i = 0; i < 5; i++)
                if (Logger.IsEnabled((LogLevel)i))
                {
                    minLevel = i;
                    break;
                }

            var previousLogLevel = LogLevel.None;
            var sb = new StringBuilder();
            await foreach (var line in File.ReadLinesAsync(LogFilePath, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var jsonLine = JsonNode.Parse(line);
                jsonDoc.Add(jsonLine);

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
                if (quartoRenderProcess.ExitCode == 1 &&
                    currentLogLevel == LogLevel.Error &&
                    Regex.IsMatch(message, @"NotFound: No such file or directory \(os error 2\): rename '/tmp/quarto_report_.+/-' -> '/tmp/quarto_report_.+/_output/-'") )
                {
                    if (sb.Length > 0)
                        Logger.Log(previousLogLevel, "Quarto render process {QuartoRenderProcessId}: {Message}",
                            quartoRenderProcess.Id, sb.ToString());

                    Logger.LogDebug(
                        "Quarto render process {QuartoRenderProcessId}: Hit well-known Quarto bug (https://github.com/quarto-dev/quarto-cli/issues/13394)\n{ Message}",
                        quartoRenderProcess.Id, message);
                    sb.Length = 0;
                    success = true;
                    continue;
                }

                if ((int)currentLogLevel < minLevel)
                    continue;

                if (previousLogLevel != currentLogLevel)
                {
                    Logger.Log(previousLogLevel, "Quarto render process {QuartoRenderProcessId}: {Message}",
                        quartoRenderProcess.Id, sb.ToString());
                    previousLogLevel = currentLogLevel;
                    sb.Length = 0;
                }

                sb.AppendLine(message);
            }

            if (sb.Length > 0)
                Logger.Log(previousLogLevel, "Quarto render process {QuartoRenderProcessId}: {Message}",
                    quartoRenderProcess.Id, sb.ToString());

            if (!success)
                return Environment.IsDevelopment() ? Results.InternalServerError(jsonDoc) : Results.InternalServerError();
        }

        await stdOut;
        buffer.Seek(0, SeekOrigin.Begin);
        return Results.Stream(buffer, ResponseContentType, GetFileDownloadName());
    }

    private IEnumerable<string> GetArguments()
    {
        yield return "render";
        yield return ReportFileName;

        yield return "--log";
        yield return LogFilePath;

        yield return "--log-level";
        if (Environment.IsDevelopment())
            yield return "debug";
        else
            yield return "warning";

        yield return "--log-format";
        yield return "json-stream";

        yield return "--quiet";
        yield return "--to";
        switch (ResponseContentType)
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

        foreach (var param in ReportParameters)
        {
            yield return "-P";
            yield return param;
        }

        yield return "--output";
        yield return "-";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
        if (_disposedValue)
            return;

        if (disposing)
        {
            try
            {
                ReportDir.Delete(true);
            }
            catch
            {
                // ignored
            }
        }
        _disposedValue = true;
    }
}