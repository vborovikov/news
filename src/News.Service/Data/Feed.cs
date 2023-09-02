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

record FeedItemWrapper
{
    private readonly FeedItem item;

    public FeedItemWrapper(FeedItem item)
    {
        this.item = item;
    }

    public string Id => this.item.Id ?? this.item.PublishingDateString;

    public string Link => (this.item.Link ?? this.item.Id).Trim();

    public string Slug => this.Link.SlugifyPost();

    public DateTimeOffset Published
    {
        get
        {
            var publishedStr = this.item.PublishingDateString ??
                (this.item.SpecificItem as AtomFeedItem)?.UpdatedDateString;

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
