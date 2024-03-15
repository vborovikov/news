namespace News.Service.Data;

using System;
using System.Diagnostics;
using Syndication;
using Syndication.Feeds;
using Spryer;

[Flags]
enum FeedUpdateStatus
{
    None = 0 << 0,
    OK = None,

    UniqueId = 1 << 0,
    UQID = UniqueId,

    DistinctId = 1 << 1,
    DSID = DistinctId,

    HtmlResponse = 1 << 2,
    HTML = HtmlResponse,

    HttpError = 1 << 3,
    HTTP = HttpError,

    UserAgent = 1 << 4,
    USER = UserAgent,

    SkipUpdate = 1 << 5,
    SKIP = SkipUpdate,
}

record DbFeed
{
    public Guid Id { get; init; }
    public required string Source { get; init; }
    public DbEnum<FeedUpdateStatus> Status { get; init; }
    public DbEnum<FeedSafeguard> Safeguards { get; init; }
}

[Flags]
enum PostStatus
{
    None        = 0 << 0,
    OK          = None,
    SkipUpdate  = 1 << 0,
    SKIP        = SkipUpdate,
}

record DbPostInfo
{
    public Guid Id { get; init; }
    public required string Link { get; init; }
    public required string Title { get; init; }
    public DbEnum<PostStatus> Status { get; init; }
}

record DbPost : DbPostInfo
{
    public string? Description { get; init; }
    public required string Content {get; init; }
}

abstract record WrapperBase
{
    protected static string GetNonEmpty(params string?[] strings)
    {
        foreach (var str in strings)
        {
            if (!string.IsNullOrWhiteSpace(str))
                return str;
        }

        return strings[^1] ?? string.Empty;
    }
}

record FeedWrapper : WrapperBase
{
    private readonly Feed feed;
    private readonly DbFeed db;
    private string? link;

    public FeedWrapper(Feed feed, DbFeed db)
    {
        this.feed = feed;
        this.db = db;
    }

    public string Title => GetNonEmpty(this.feed.Title, "<no title>");
    public string Description => GetNonEmpty(
        this.feed.Description,
        (this.feed.SpecificFeed as AtomFeed)?.Subtitle,
        "&lt;no description&gt;");
    public string Link => this.link ??= GetLink();

    internal bool ItemsRequireUniqueIds => this.db.Status.HasFlag(FeedUpdateStatus.UniqueId);

    private string GetLink()
    {
        var url = this.feed.Link;

        if (!Uri.IsWellFormedUriString(url, UriKind.Absolute) &&
            Uri.TryCreate(this.db.Source, UriKind.Absolute, out var feedUri))
        {
            var baseUrl = feedUri.GetLeftPart(UriPartial.Authority);
            if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) &&
                Uri.TryCreate(baseUri, url, out var absoluteUri))
            {
                url = absoluteUri.ToString();
            }
            else
            {
                url = baseUrl;
            }
        }

        return url;
    }
}

record FeedItemWrapper : WrapperBase
{
    private readonly FeedItem item;
    private readonly FeedWrapper feed;
    private DateTimeOffset? published;

    public FeedItemWrapper(FeedItem item, FeedWrapper feed)
    {
        this.item = item;
        this.feed = feed;
    }

    public string Id
    {
        get
        {
            var id = GetNonEmpty(this.item.Id,
                (this.item.SpecificItem as Rss20FeedItem)?.Comments, this.item.Link,
                this.PublishedDateString, this.Published.ToString());

            if (this.feed.ItemsRequireUniqueIds)
            {
                var uniquePart = this.item.Link.SlugifyPost() + (this.PublishedDateString ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(uniquePart))
                {
                    return $"{id}#{Uri.EscapeDataString(uniquePart)}";
                }
            }

            return id;
        }
    }

    //todo: handle this.item.SpecificItem.Element directly
    public string? PublishedDateString => GetNonEmpty(
        this.item.PublishingDateString,
        (this.item.SpecificItem as AtomFeedItem)?.PublishedDateString,
        (this.item.SpecificItem as AtomFeedItem)?.UpdatedDateString,
        (this.item.SpecificItem as Rss091FeedItem)?.PublishingDateString,
        (this.item.SpecificItem as Rss20FeedItem)?.PublishingDateString);

    public string Link => EnsureAbsoluteUrl(GetNonEmpty(this.item.Link, this.item.Id).Trim());

    private string EnsureAbsoluteUrl(string link)
    {
        if (Uri.IsWellFormedUriString(link, UriKind.Relative) &&
            Uri.TryCreate(this.feed.Link, UriKind.Absolute, out var feedUrl) &&
            Uri.TryCreate(feedUrl, link, out var absoluteUrl))
        {
            link = absoluteUrl.ToString();
        }

        return link;
    }

    public string Slug => this.Link.SlugifyPost();

    public DateTimeOffset Published => this.published ??= GetPublished();

    private DateTimeOffset GetPublished()
    {
        var publishedStr = this.PublishedDateString;

        if (DateTimeOffset.TryParse(publishedStr, out var published))
            return published;

        if (BrokenDateTimeOffset.TryParse(publishedStr, out published))
            return published;

        Debug.WriteLine($"Unable to parse '{publishedStr}'");
        return DateTimeOffset.Now;
    }

    public string Title =>
        string.IsNullOrWhiteSpace(this.item.Title) ? this.Link :
        this.item.Title.Length >= 1000 ? this.item.Title[..1000] :
        this.item.Title;

    public string? Description => this.item.Content is not null ?
        GetNonEmpty(this.item.Description, (this.item.SpecificItem as AtomFeedItem)?.Summary) :
        null;

    public string? Author => this.item.Author ??
        (this.item.SpecificItem as Rss20FeedItem)?.DC.Creator ??
        (this.item.SpecificItem as Rss10FeedItem)?.DC.Creator;

    // null checks in case the author is having a writer's block
    public string Content => GetNonEmpty(this.item.Content, this.item.Description, this.Link, "<no content>");
}
