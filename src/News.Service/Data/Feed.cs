namespace News.Service.Data;

using System;
using System.Diagnostics;
using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;

record DbFeed
{
    public Guid Id { get; init; }
    public string Source { get; init; } = "";
}

record FeedWrapper
{
    private readonly Feed feed;
    private readonly DbFeed db;
    private string? link;

    public FeedWrapper(Feed feed, DbFeed db)
    {
        this.feed = feed;
        this.db = db;
    }

    public string Title => string.IsNullOrWhiteSpace(this.feed.Title) ? "<no title>" : this.feed.Title;
    public string Description => string.IsNullOrWhiteSpace(this.feed.Description) ? "<no description>" : this.feed.Description;
    public string Link => this.link ??= GetLink();

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

record FeedItemWrapper
{
    private readonly FeedItem item;
    private readonly FeedWrapper feed;
    private DateTimeOffset? published;

    public FeedItemWrapper(FeedItem item, FeedWrapper feed)
    {
        this.item = item;
        this.feed = feed;
    }

    public string Id => string.IsNullOrWhiteSpace(this.item.Id) ?
        this.Published.ToString() :
        //todo: adding the pubDate to the ID really is needed for broken feeds which I don't use in prod, so it's commented out for now
        this.item.Id /*+ $"#{Uri.EscapeDataString(this.Published.ToString())}"*/;

    public string? PublishedDateStringSafe => this.item.PublishingDateString ??
        (this.item.SpecificItem as AtomFeedItem)?.UpdatedDateString ??
        (this.item.SpecificItem as Rss091FeedItem)?.PublishingDateString ??
        (this.item.SpecificItem as Rss20FeedItem)?.PublishingDateString;

    public string Link => EnsureAbsoluteUrl((this.item.Link ?? this.item.Id).Trim());

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
        var publishedStr = this.PublishedDateStringSafe;

        DateTimeOffset published;
        while (!DateTimeOffset.TryParse(publishedStr, out published))
        {
            if (publishedStr is null)
            {
                return DateTimeOffset.Now;
            }
            else if (publishedStr.EndsWith("CST", StringComparison.OrdinalIgnoreCase))
            {
                publishedStr = publishedStr.Replace("CST", "-06:00", StringComparison.OrdinalIgnoreCase);
            }
            else if (publishedStr.EndsWith("UTC", StringComparison.OrdinalIgnoreCase))
            {
                publishedStr = publishedStr.Replace("UTC", "+00:00", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Debug.WriteLine($"Unable to parse '{publishedStr}'");
                publishedStr = null;
            }
        }

        return published;
    }

    public string Title =>
        string.IsNullOrWhiteSpace(this.item.Title) ? this.Link :
        this.item.Title.Length >= 1000 ? this.item.Title[..1000] :
        this.item.Title;

    public string? Description => this.item.Content is not null ? this.item.Description : null;

    public string Author => this.item.Author;

    public string Content
    {
        get
        {
            // null checks in case the author is having a writer's block
            var content = this.item.Content ?? this.item.Description ?? this.Link;
            return string.IsNullOrWhiteSpace(content) ? "<no content>" : content;
        }
    }
}
