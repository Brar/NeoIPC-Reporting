namespace NeoIPC.Reporting;

interface IDataGenerator : IAsyncDisposable
{
    Task<DataResult> Generate(CancellationToken cancellationToken);
}
