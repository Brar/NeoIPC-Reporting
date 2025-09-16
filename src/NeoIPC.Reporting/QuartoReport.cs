using System.Diagnostics;
using System.Net.Mime;

namespace NeoIPC.Reporting;

abstract class QuartoReport : IDisposable
{
    private bool _disposedValue;
    protected DirectoryInfo ReportDir { get; }
    protected abstract string ReportFileName { get; }
    protected abstract string? ResponseContentType { get; }
    protected abstract string JSessionId { get; }
    protected abstract string[] ReportParameters { get; }
    protected abstract string? GetFileDownloadName();

    protected QuartoReport(string reportDir)
    {
        var srcDir = new DirectoryInfo(Path.Join("/reports", reportDir));
        if (!srcDir.Exists)
            throw new DirectoryNotFoundException($"Report directory '{srcDir.FullName}' not found.");

        ReportDir = Directory.CreateTempSubdirectory("quarto_report_");

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
                ["NEOIPC_DHIS2_SESSION_ID"] = JSessionId
            }
        };
        using var quartoRenderProcess = Process.Start(startInfo);

        if (quartoRenderProcess == null)
            return Results.InternalServerError();

        var buffer = new MemoryStream();
        var stdOut = quartoRenderProcess.StandardOutput.BaseStream.CopyToAsync(buffer, cancellationToken);
        var stdErr = quartoRenderProcess.StandardError.ReadToEndAsync(cancellationToken);

        await quartoRenderProcess.WaitForExitAsync(cancellationToken);

        if (quartoRenderProcess.ExitCode != 0)
            return Results.InternalServerError(await stdErr);

        await stdOut;
        buffer.Seek(0, SeekOrigin.Begin);
        return Results.Stream(buffer, ResponseContentType, GetFileDownloadName());
    }

    private IEnumerable<string> GetArguments()
    {
        yield return "render";
        yield return ReportFileName;

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
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        yield return "--output";
        yield return "-";

        foreach (var param in ReportParameters)
        {
            yield return "-P";
            yield return param;
        }
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