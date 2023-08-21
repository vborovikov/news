namespace News.Service;

using Microsoft.Extensions.Logging.EventLog;

record ServiceOptions
{
    public const string ServiceName = "Newsmaker";
}

static class Program
{
    public static void Main(string[] args)
    {
        IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var connectionString =
                    hostContext.Configuration.GetConnectionString("DefaultConnection") ??
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                services.Configure<ServiceOptions>(hostContext.Configuration.GetSection(ServiceOptions.ServiceName));
                services.Configure<EventLogSettings>(settings =>
                {
                    // AddEventLog() is called in UseWindowsService()

                    // The event log source must exist in the log or the app must have permissions to create it.
                    // PowerShell command to create the source: New-EventLog -Source "Newsmaker" -LogName "Application"
                    settings.SourceName = ServiceOptions.ServiceName;
                    settings.LogName = "Application";
                });

                services.AddHostedService<Worker>();
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = ServiceOptions.ServiceName;
            }).Build();

        host.Run();
    }
}