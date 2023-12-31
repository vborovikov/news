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
}

public record ServiceOptions : AppOptions
{
    public const string ServiceName = "Newsmaker";

    public TimeSpan UpdateInterval { get; init; } = TimeSpan.FromHours(3);

    public string? UserAgent { get; init; }

    public required MessageQueueName UserAgentQueue {  get; init; }
}

