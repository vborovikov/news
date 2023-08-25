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
            slug = SlugifyUrl(url);
        }

        return slug;
    }

    private static string SlugifyUrl(string url)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var i = parts.Length - 1; i > 0; --i)
        {
            var part = parts[i];

            if (part.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                part.EndsWith(".rss", StringComparison.OrdinalIgnoreCase) ||
                part.EndsWith(".axd", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("index.", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("rss.", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("atom.", StringComparison.OrdinalIgnoreCase))
                continue;
            if (part.StartsWith("blog", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("feed", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("post", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("page", StringComparison.OrdinalIgnoreCase) ||
                part.StartsWith("default", StringComparison.OrdinalIgnoreCase))
                continue;

            if (part.Contains('.', StringComparison.OrdinalIgnoreCase))
            {
                var hostParts = part.Split('.', StringSplitOptions.RemoveEmptyEntries);
                if (hostParts.Length > 1)
                {
                    if (hostParts[^2] != "github" && hostParts[^2] != "hashnode")
                    {
                        part = hostParts[^2];
                    }
                    else
                    {
                        part = hostParts[0];
                    }
                }
            }

            return part
                .Replace(".github.io", "")
                .Replace(".hashnode.dev", "")
                .Trim('@')
                .ToLowerInvariant();
        }

        return parts[^1].ToLowerInvariant();
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
