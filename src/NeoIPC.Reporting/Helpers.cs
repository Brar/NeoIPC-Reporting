namespace NeoIPC.Reporting;

public static class Helpers
{
    public static string FileExtensionFromMediaType(string mediaType)
    {
        return mediaType switch
        {
            "application/pdf" =>
                ".pdf",
            "application/json" =>
                ".json",
            "text/html" =>
                ".html",
            _ => throw new NotSupportedException()
        };

    }

}