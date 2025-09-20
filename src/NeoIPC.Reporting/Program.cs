using NeoIPC.Reporting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddRequestTimeouts();

var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("reference-report", ReferenceReport.Get)
    .WithName("GetReferenceReport")
    .WithRequestTimeout(TimeSpan.FromSeconds(360));

app.Run();
