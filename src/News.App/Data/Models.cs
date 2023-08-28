namespace News.App.Data;

public record RssChannelInfo
{
    public Guid ChannelId { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
}

public record RssChannel : RssChannelInfo
{
    public IEnumerable<RssFeed> Feeds { get; init; } = Enumerable.Empty<RssFeed>();
}

public record RssFeedInfo
{
    private static TimeSpan OutdatedThreshold = TimeSpan.FromDays(30);

    private DateTimeOffset? updateLocal;
    private DateTimeOffset? lastPublished;

    public Guid FeedId { get; init; }
    public string Title { get; init; }
    public string Slug { get; init; }
    
    public DateTimeOffset Updated { get; init; }
    public DateTimeOffset UpdatedLocal => this.updateLocal ??= this.Updated.ToLocalTime();

    public DateTimeOffset? LastPublished { get; init; }
    public DateTimeOffset? LastPublishedLocal => this.lastPublished ??= this.LastPublished?.ToLocalTime();
    public bool HasPosts => this.LastPublished is not null;
    public bool IsOutdated => 
        this.LastPublished is not null &&
        (this.Updated - this.LastPublished.Value > OutdatedThreshold);
    
    public string? Error { get; init; }
    public bool HasError => !string.IsNullOrWhiteSpace(this.Error);
}

public record RssFeed : RssFeedInfo
{
    public IEnumerable<RssPost> Posts { get; init; } = Enumerable.Empty<RssPost>();
}

public record RssPostInfo
{
    private DateTimeOffset? publishedLocal;

    public Guid PostId { get; init; }
    public DateTimeOffset Published { get; init; }
    public DateTimeOffset PublishedLocal => this.publishedLocal ??= this.Published.ToLocalTime();
    public Uri Link { get; init; }
    public string Title { get; init; }
    public string Description { get; init; }
    public bool IsRead { get; init; }
}

public record RssPost : RssPostInfo
{
    public string Content { get; init; }
}