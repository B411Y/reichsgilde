using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// MCP-Server für den Kartenbau: nur stdio, kein Netz-Port. Alles Protokoll läuft über stdin/stdout, Meldungen gehen nach stderr.
var bauer = Host.CreateApplicationBuilder(args);
bauer.Logging.ClearProviders();
bauer.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
bauer.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();
await bauer.Build().RunAsync();
