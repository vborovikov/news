namespace News.App.Data;

using Spryer;

public record RssChannelInfo
{
    public Guid ChannelId { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
}

public record RssFeedInfo
{
    private static readonly TimeSpan OutdatedThreshold = TimeSpan.FromDays(30);

    private DateTimeOffset? updatedLocal;
    private DateTimeOffset? scheduledLocal;
    private DateTimeOffset? lastPublished;

    public Guid FeedId { get; init; }
    public string Title { get; init; } = "";
    public string Slug { get; init; } = "";
    public string? Description { get; init; }
    public string Link { get; init; } = "";
    public DbEnum<FeedType> Type { get; init; }
    
    public DateTimeOffset Updated { get; init; }
    public DateTimeOffset UpdatedLocal => this.updatedLocal ??= this.Updated.ToLocalTime();
    public DateTimeOffset? Scheduled { get; init; }
    public DateTimeOffset? ScheduledLocal => this.scheduledLocal ??= this.Scheduled?.ToLocalTime();

    public DateTimeOffset? LastPublished { get; init; }
    public DateTimeOffset? LastPublishedLocal => this.lastPublished ??= this.LastPublished?.ToLocalTime();
    public bool HasPosts => this.LastPublished is not null;
    public bool IsOutdated => 
        this.LastPublished is not null &&
        (this.Updated - this.LastPublished.Value > OutdatedThreshold);
    
    public string? Error { get; init; }
    public bool HasError => !string.IsNullOrWhiteSpace(this.Error);
}

public record RssPostInfo
{
    private DateTimeOffset? publishedLocal;

    public required Guid PostId { get; init; }
    public required string Title { get; init; }
    public required string Slug { get; init; }
    public required string Link { get; init; }
    public required DateTimeOffset Published { get; init; }
    public DateTimeOffset PublishedLocal => this.publishedLocal ??= this.Published.ToLocalTime();
    public string? Description { get; init; }
    public bool IsRead { get; init; }
    public bool IsFavorite { get; init; }
    public string? Author { get; init; }
}

