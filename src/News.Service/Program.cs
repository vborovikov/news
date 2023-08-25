namespace News.Service;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.EventLog;

record ServiceOptions
{
    public const string ServiceName = "Newsmaker";

    private DirectoryInfo? opmlDirectory;

    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromHours(3);

    public string OpmlPath { get; init; } = @"C:\Tools\News\opml";
    public DirectoryInfo OpmlDirectory => this.opmlDirectory ??= new(this.OpmlPath);
}

static class Program
{
    public static Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
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

                services.AddSingleton(_ => SqlClientFactory.Instance.CreateDataSource(connectionString));
                services.AddHostedService<Worker>();
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = ServiceOptions.ServiceName;
            })
            .Build();

        return host.RunAsync();
    }
}