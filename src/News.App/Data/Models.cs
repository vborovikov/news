namespace News.App.Data;

public record RssChannelInfo
{
    public Guid ChannelId { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }
}

public record RssChannel : RssChannelInfo
{
    public IEnumerable<RssFeedInfo> Feeds { get; init; } = Enumerable.Empty<RssFeedInfo>();
}

public record RssFeedInfo
{
    public Guid FeedId { get; init; }
    public string Title { get; init; }
    public string Slug { get; init; }
}

public record RssFeed : RssFeedInfo
{
    public IEnumerable<RssPost> Posts { get; init; } = Enumerable.Empty<RssPost>();
}

public record RssPost
{
    public Guid PostId { get; init; }
    public string Title { get; init; }
    public string Content { get; init; }
}