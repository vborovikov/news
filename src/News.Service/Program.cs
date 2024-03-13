namespace News.Service;

using System.Text;
using Dodkin.Dispatch;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using News.Service.Data;
using Spryer;

static class HttpClients
{
    public const string Feed = "Feed";
    public const string Post = "Post";
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

                // post http client
                services.AddHttpClient(HttpClients.Post, (sp, httpClient) =>
                {
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
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/avif"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/bmp"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/gif"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/jpeg"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/png"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/svg+xml"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/tiff"));
                    httpClient.DefaultRequestHeaders.Accept.Add(new("image/webp"));

                    var options = sp.GetRequiredService<IOptions<ServiceOptions>>();
                    if (options.Value.UserAgent is not null)
                    {
                        httpClient.DefaultRequestHeaders.UserAgent.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", options.Value.UserAgent);
                    }
                });

                services.AddSingleton<IQueueRequestDispatcher>(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ServiceOptions>>().Value;
                    var logging = sp.GetRequiredService<ILoggerFactory>();
                    return new QueueRequestDispatcher(options.UserAgentQueue, options.Endpoint,
                        logging.CreateLogger("News.Service.Data.UserAgentDispatcher"));
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