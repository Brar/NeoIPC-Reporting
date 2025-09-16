using NeoIPC.Reporting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
var app = builder.Build();
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("reference-report", ReferenceReport.Get)
    .WithName("GetReferenceReport");

app.Run();
