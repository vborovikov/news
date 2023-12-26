namespace News.Service;

using System.Text;
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

    public required MessageQueueName UserAgentQueue {  get; init; }

    public required string OpmlPath { get; init; } = @"C:\Tools\News\opml";
    public DirectoryInfo OpmlDirectory => this.opmlDirectory ??= new(this.OpmlPath);
}

static class HttpClients
{
    public const string Feed = "Feed";
    public const string Image = "Image";
}

static class Program
{
    static Program()
    {
        DbEnum<FeedUpdateStatus>.Initialize();
        DbEnum<FeedSafeguard>.Initialize();
    }

    public static Task Main(string[] args)
    {
        if (Environment.UserInteractive)
        {
            Console.InputEncoding = Encoding.Default;
            Console.OutputEncoding = Encoding.Default;
        }

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

                // feed http client
                services.AddHttpClient(HttpClients.Feed, (sp, httpClient) =>
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

                // image download http client
                services.AddHttpClient(HttpClients.Image, (sp, httpClient) =>
                {
                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/png"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/jpeg"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/gif"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/webp"));

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