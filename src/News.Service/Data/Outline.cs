using System.Xml;

namespace News.Service.Data;

abstract record Outline(string Text, string? Title)
{
    public virtual bool IsValid => !string.IsNullOrWhiteSpace(this.Text);

    public string Name => this.Title ?? this.Text;
}

record FeedOutline(string Text, string? Title, string XmlUrl, string? HtmlUrl) : Outline(Slugify(Text, XmlUrl), Title)
{
    public override bool IsValid => base.IsValid && Uri.IsWellFormedUriString(this.XmlUrl, UriKind.Absolute);

    public string Url => this.HtmlUrl ?? this.XmlUrl;

    public static FeedOutline? FromXml(XmlNode node)
    {
        if (node.Name != "outline" || node.NodeType != XmlNodeType.Element || node.HasChildNodes)
        {
            //throw new InvalidOperationException("Outline node is invalid or has child nodes");
            return null;
        }

        return new(
            node.Attributes?["text"]?.Value ?? "",
            node.Attributes?["title"]?.Value,
            node.Attributes?["xmlUrl"]?.Value ?? "",
            node.Attributes?["htmlUrl"]?.Value);
    }

    private static string Slugify(string text, string url)
    {
        var slug = text;
        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = url.SlugifyFeed();
        }

        return SanitizeSlug(slug);
    }

    private static string SanitizeSlug(string slug)
    {
        return slug
            .Trim('_', '-', '@')
            .Replace(' ', '-')
            .ToLowerInvariant();
    }
}

record ChannelOutline(string Text, string? Title, FeedOutline[] Feeds) : Outline(Text, Title)
{
    public override bool IsValid => base.IsValid && this.Feeds.Length > 0 && Array.TrueForAll(this.Feeds, x => x.IsValid);

    public static ChannelOutline? FromXml(XmlNode node)
    {
        if (node.Name != "outline" || node.NodeType != XmlNodeType.Element || !node.HasChildNodes)
        {
            //throw new InvalidOperationException("Outline node is invalid or has no child nodes");
            return null;
        }

        var feeds = new List<FeedOutline>();
        foreach (var child in node.ChildNodes)
        {
            if (child is not XmlNode childNode || childNode.HasChildNodes)
                continue;

            var feed = FeedOutline.FromXml(childNode);
            if (feed is not null)
            {
                feeds.Add(feed);
            }
        }

        if (feeds.Count == 0)
        {
            return null;
        }

        return new(
            node.Attributes?["text"]?.Value ?? "",
            node.Attributes?["title"]?.Value,
            feeds.ToArray());
    }
}
