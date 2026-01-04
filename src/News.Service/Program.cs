namespace News.Service;

using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using Dodkin.Dispatch;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using News.Service.Scheduling;
using Polly;
using Spryer;
using Storefront.UserAgent;

static class HttpClients
{
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1);

    public const string Feed = "Feed";
    public const string FeedProxy = "FeedProxy";
    public const string Post = "Post";
    public const string Image = "Image";
}

static class Program
{
    static Program()
    {
        DbEnum<FeedType>.Initialize();
        DbEnum<FeedStatus>.Initialize();
        DbEnum<FeedSafeguard>.Initialize();
        DbEnum<PostStatus>.Initialize();
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
                services.Configure((Action<EventLogSettings>)(settings =>
                {
                    settings.SourceName = ServiceOptions.ServiceName;
                    settings.LogName = "Application";
                }));
#pragma warning restore CA1416 // Validate platform compatibility

                services.ConfigureHttpClientDefaults(http =>
                {
                    http.ConfigureHttpClient((sp, httpClient) =>
                    {
                        var options = sp.GetRequiredService<IOptions<ServiceOptions>>().Value;

                        if (options.UserAgent is not null)
                        {
                            httpClient.DefaultRequestHeaders.UserAgent.Clear();
                            httpClient.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
                        }
                        else
                        {
                            httpClient.DefaultRequestHeaders.UserAgent.Clear();
                            httpClient.DefaultRequestHeaders.Add("User-Agent", AppInfo.Instance.UserAgent);
                        }

                        // Overall timeout across all tries
                        //fixme: Timeout is infinite after this call!
                        httpClient.Timeout = HttpClients.Timeout;
                    });

                    http.AddStandardResilienceHandler();
                });

                // feed http client
                services
                    .AddHttpClient(HttpClients.Feed, ConfigureFeedHttpClient)
                    .ConfigurePrimaryHttpMessageHandler(ConfigureFeedHttpMessageHandler);
                services
                    .AddHttpClient(HttpClients.FeedProxy, ConfigureFeedHttpClient)
                    .ConfigurePrimaryHttpMessageHandler((handler, sp) =>
                    {
                        ConfigureFeedHttpMessageHandler(handler, sp);

                        var options = sp.GetRequiredService<IOptions<ServiceOptions>>().Value;
                        if (options.ProxyAddress is not null)
                        {
                            var proxy = new WebProxy(options.ProxyAddress, BypassOnLocal: true)
                            {
                                UseDefaultCredentials = false,
                            };

                            if (handler is SocketsHttpHandler socketsHttpHandler)
                            {
                                socketsHttpHandler.UseProxy = true;
                                socketsHttpHandler.Proxy = proxy;
                                socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                            }
                            else if (handler is HttpClientHandler { SupportsProxy: true } httpClientHandler)
                            {
                                httpClientHandler.UseProxy = true;
                                httpClientHandler.Proxy = proxy;
                                httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                            }
                        }
                    });

                // post http client
                services.AddHttpClient(HttpClients.Post, (_, httpClient) =>
                {
                    httpClient.Timeout = HttpClients.Timeout;
                });

                // image download http client
                services.AddHttpClient(HttpClients.Image, (_, httpClient) =>
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

                    httpClient.Timeout = HttpClients.Timeout;
                });

                services.AddSingleton((Func<IServiceProvider, IQueueRequestDispatcher>)(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<ServiceOptions>>().Value;
                    var logging = sp.GetRequiredService<ILoggerFactory>();

                    var dispatcher = new QueueRequestDispatcher(options.UserAgentQueue, options.Endpoint,
                        logging.CreateLogger("News.Service.UserAgent"));
                    dispatcher.RecognizeTypesFrom(typeof(PageQuery).Assembly);

                    return dispatcher;
                }));
                services.AddSingleton(_ => SqlClientFactory.Instance.CreateDataSource(connectionString));
                services.AddSingleton<CommandStore>();
                services.AddHostedService<Worker>();
            })
            .UseWindowsService(options =>
            {
                options.ServiceName = ServiceOptions.ServiceName;
            })
            .Build();

        return host.RunAsync();
    }

    private static void ConfigureFeedHttpClient(IServiceProvider sp, HttpClient httpClient)
    {
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new("application/rss+xml"));
        httpClient.DefaultRequestHeaders.Accept.Add(new("application/atom+xml"));
        httpClient.DefaultRequestHeaders.Accept.Add(new("application/xml", 0.8));
        httpClient.DefaultRequestHeaders.Accept.Add(new("text/xml", 0.7));
        httpClient.DefaultRequestHeaders.Accept.Add(new("*/*", 0.5));

        httpClient.DefaultRequestHeaders.AcceptEncoding.Clear();
        httpClient.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip,deflate,br,*;q=0.1");

        httpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
        httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9,ru-RU,ru;q=0.8,*;q=0.5");

        httpClient.DefaultRequestHeaders.AcceptCharset.Clear();
        httpClient.DefaultRequestHeaders.AcceptCharset.ParseAdd("UTF-8");

        httpClient.Timeout = HttpClients.Timeout;
    }

    private static void ConfigureFeedHttpMessageHandler(HttpMessageHandler messageHandler, IServiceProvider sp)
    {
        if (messageHandler is SocketsHttpHandler socketsHttpHandler)
        {
            socketsHttpHandler.AutomaticDecompression = DecompressionMethods.All;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Build <= 17763)
            {
                socketsHttpHandler.SslOptions.EnabledSslProtocols = SslProtocols.Tls12;
            }
        }
        else if (messageHandler is HttpClientHandler { SupportsAutomaticDecompression: true } httpClientHandler)
        {
            httpClientHandler.AutomaticDecompression = DecompressionMethods.All;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version.Build <= 17763)
            {
                httpClientHandler.SslProtocols = SslProtocols.Tls12;
            }
        }
    }
}