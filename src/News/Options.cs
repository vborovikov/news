namespace News;

using Dodkin;

public abstract record BaseOptions
{
    private DirectoryInfo? opmlDirectory;
    private DirectoryInfo? imageDirectory;

    public required string OpmlPath { get; init; } = @"C:\Tools\News\opml";
    public DirectoryInfo OpmlDirectory => this.opmlDirectory ??= new(this.OpmlPath);

    public required string ImagePath { get; init; } = @"C:\Tools\News\img";
    public DirectoryInfo ImageDirectory => this.imageDirectory ??= new(this.ImagePath);

    public required MessageEndpoint Endpoint { get; init; }
}

public sealed record AppOptions : BaseOptions
{
    public const string AppName = "Newsreader";

    public AppOptions()
    {
        this.Endpoint = MessageEndpoint.FromName("newsreader");
    }

    public required MessageQueueName ServiceQueue { get; init; } = MessageQueueName.FromName("newsmaker");
}

public sealed record ServiceOptions : BaseOptions
{
    public const string ServiceName = "Newsmaker";

    public ServiceOptions()
    {
        this.Endpoint = MessageEndpoint.FromName("newsmaker");
    }

    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromHours(8);
    public TimeSpan MinUpdateInterval { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaxUpdateInterval { get; init; } = TimeSpan.FromDays(30);

    public string? UserAgent { get; init; }

    public required MessageQueueName UserAgentQueue { get; init; } = MessageQueueName.FromName("useragent");
    public required MessageQueueName SchedulerQueue { get; init; } = MessageQueueName.FromName("dodkin");

    public Uri? ProxyAddress { get; init; } = null;
}

