namespace NeoIPC.Reporting;

readonly struct DataResult : IAsyncDisposable
{
    public DataResult()
        => Result = Results.InternalServerError();

    public DataResult(Stream stream, string? contentType, string? fileDownloadName)
    {
        stream.Seek(0, SeekOrigin.Begin);
        Data = stream;
        Result = Results.Stream(stream, contentType, fileDownloadName);
        Success = true;
    }
    public DataResult(string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null, IDictionary<string, object?>? extensions = null, bool showMessage = false)
    {
        Result = showMessage
            ? Results.Problem(detail, instance, statusCode, title, type, extensions)
            : Results.InternalServerError();
        Success = false;
    }

    public DataResult(Exception exception, bool showMessage = false)
    {
        Result = showMessage
            ? Results.Problem(exception.Message, statusCode: 500, title: "Internal Server Error")
            : Results.InternalServerError();
        Success = false;
    }

    public DataResult(int statusCode)
    {
        Result = Results.StatusCode(statusCode);
        Success = false;
    }

    public bool Success { get; }
    public Stream? Data { get; }
    public IResult Result { get; }

    public ValueTask DisposeAsync()
        => Data?.DisposeAsync() ?? ValueTask.CompletedTask;

    public static readonly DataResult SimpleSuccess = new(new MemoryStream(), null, null);
    public static readonly DataResult SimpleFailure = new();
}