namespace News.Service;

using Dodkin;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using News.Service.Data;
using Spryer;

record ServiceOptions
{
    public const string ServiceName = "Newsmaker";

    private DirectoryInfo? opmlDirectory;

    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromHours(3);

    public string? UserAgent { get; init; }

    public required string UserAgentQueue {  get; init; }

    public required string OpmlPath { get; init; } = @"C:\Tools\News\opml";
    public DirectoryInfo OpmlDirectory => this.opmlDirectory ??= new(this.OpmlPath);
}

static class Program
{
    static Program()
    {
        DbEnum<FeedUpdateStatus>.Initialize();
    }

    public static Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                var connectionString =
                    hostContext.Configuration.GetConnectionString("DefaultConnection") ??
                    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

                services.Configure<ServiceOptions>(hostContext.Configuration.GetSection(ServiceOptions.ServiceName));
#pragma warning disable CA1416 // Validate platform compatibility
                services.Configure<EventLogSettings>(settings =>
                {
                    settings.SourceName = ServiceOptions.ServiceName;
                    settings.LogName = "Application";
                });
#pragma warning restore CA1416 // Validate platform compatibility

                services.AddHttpClient("Feed", (sp, httpClient) =>
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new("application/rss+xml"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("application/atom+xml"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("application/xml"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("text/xml"));

                    var options = sp.GetRequiredService<IOptions<ServiceOptions>>();
                    if (options.Value.UserAgent is not null)
                    {
                        httpClient.DefaultRequestHeaders.UserAgent.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", options.Value.UserAgent);
                    }
                });
                services.AddSingleton<UserAgent>();
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