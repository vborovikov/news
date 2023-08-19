namespace News.App.Data;

public record RssChannel
{
    public Guid ChannelId { get; init; }
    public string Name { get; init; }
    public string Slug { get; init; }

    public IEnumerable<RssFeed> Feeds { get; init; } = Enumerable.Empty<RssFeed>();
}

public record RssFeed
{
    public Guid FeedId { get; init; }
    public string Title { get; init; }
    public string Slug { get; init; }
}