namespace News;

using Dodkin;

public record AppOptions
{
    public const string AppName = "Newsreader";

    private DirectoryInfo? opmlDirectory;
    private DirectoryInfo? imageDirectory;

    public required string OpmlPath { get; init; } = @"C:\Tools\News\opml";
    public DirectoryInfo OpmlDirectory => this.opmlDirectory ??= new(this.OpmlPath);

    public required string ImagePath { get; init; } = @"C:\Tools\News\img";
    public DirectoryInfo ImageDirectory => this.imageDirectory ??= new(this.ImagePath);

    public required MessageEndpoint Endpoint { get; init; }
}

public record ServiceOptions : AppOptions
{
    public const string ServiceName = "Newsmaker";

    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromHours(3);
    public TimeSpan MinUpdateInterval { get; init;} = TimeSpan.FromMinutes(15);

    public string? UserAgent { get; init; }

    public required MessageQueueName UserAgentQueue { get; init; } = MessageQueueName.FromName("useragent");
    public required MessageQueueName SchedulerQueue { get; init; } = MessageQueueName.FromName("dodkin");

    public Uri? ProxyAddress { get; init; } = null;
}

