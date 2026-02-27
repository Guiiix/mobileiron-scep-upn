using DMIPatchSCEP;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService();
builder.Services.AddHostedService<Worker>();
builder.Logging.AddEventLog(options =>
{
    options.SourceName = "DMIPatchSCEP";
});

var host = builder.Build();
host.Run();
