using AzureExtensions.Functions.OpenApi;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Use the ASP.NET Core integration HTTP model (modern isolated-worker HTTP pipeline).
builder.ConfigureFunctionsWebApplication();

// Referencing AzureExtensions.Functions.OpenApi + this single call contributes the
// GET /api/openapi.json and GET /api/openapi.yaml endpoints.
builder.Services.AddOpenApi(options =>
{
    options.Title = "Sample Function App";
    options.Version = "1.0.0";
    options.Description = "Demonstrates AzureExtensions.Functions.OpenApi in an isolated worker v4 app.";
});

builder.Build().Run();
