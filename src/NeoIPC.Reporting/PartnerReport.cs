namespace NeoIPC.Reporting;

static class PartnerReport
{
    public static async Task<IResult> Get(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        return Results.StatusCode(415);
    }
}
