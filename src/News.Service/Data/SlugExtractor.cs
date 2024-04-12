namespace News.Service.Data;

using System;
using System.Buffers;
using System.Text;

static class SlugExtractor
{
    private const int MaxSlugLength = 100;
    private static readonly SearchValues<char> reservedChars = SearchValues.Create(";/?:@&=+$,");
    private static readonly Dictionary<string, string> reservedCharReplacements = new()
    {
        {";", "-semicolon"},
        {"/", "-slash"},
        {"?", "-question"},
        {":", "-colon"},
        {"@", "-at"},
        {"&", "-amp"},
        {"=", "-eq"},
        {"+", "-plus"},
        {"$", "-dollar"},
        {",", "-comma"},
    };

    public static string SlugifyPost(this string url) => url.AsSpan().SlugifyPost();

    public static string SlugifyPost(this ReadOnlySpan<char> url)
    {
        var slug = ReadOnlySpan<char>.Empty;

        var parts = new UrlPathEnumerator(url);
        while (parts.MoveNext())
        {
            // get last part of the url
            slug = parts.Current;

            // check for ? then get the first parameter value or skip the part entirely
            var queryIdx = slug.IndexOf('?');
            if (queryIdx == 0)
            {
                slug = slug[1..];
                var eqSignIdx = slug.IndexOf('=');
                if (eqSignIdx > 0)
                {
                    if (slug.StartsWith("utm_"))
                    {
                        // utm parameters, skip entirely
                        continue;
                    }

                    slug = slug[(eqSignIdx + 1)..];
                    var ampIdx = slug.IndexOf('&');
                    if (ampIdx > 0)
                    {
                        slug = slug[..ampIdx];
                    }
                }
            }
            else if (queryIdx > 0)
            {
                slug = slug[..queryIdx];
                if (slug.Contains('='))
                {
                    continue;
                }
            }

            // check for # then get the rest after the # or before depending on the length
            var hashIdx = slug.IndexOf('#');
            if (hashIdx >= 0)
            {
                var rest = slug[(hashIdx + 1)..];
                if (hashIdx >= 0 && (rest.Contains(':') || hashIdx > rest.Length))
                {
                    if (hashIdx == 0)
                    {
                        continue;
                    }

                    slug = slug[..hashIdx];
                }
                else
                {
                    slug = rest;
                }
            }

            // check for file extension then remove it
            slug = slug.MaybeRemoveExtension();
            // trim irrelevant characters
            slug = slug.Trim("!()+-@[]_{}~");

            // check for common words then discard
            if (slug.IsEmpty || slug.IsCommonWord())
            {
                continue;
            }

            break;
        }

        return SanitizeSlug(slug);
    }

    public static string SlugifyFeed(this string url)
    {
        var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (var i = parts.Length - 1; i >= 0; --i)
        {
            var part = parts[i];

            if (i > 1 || (i == 1 && parts.Length == 2))
            {
                if (part.Length < 3 ||
                    part.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                    part.EndsWith(".rss", StringComparison.OrdinalIgnoreCase) ||
                    part.EndsWith(".axd", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("index.", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("rss", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("atom", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("author", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("blog", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("feed", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("post", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("page", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("default", StringComparison.OrdinalIgnoreCase) ||
                    part.StartsWith("syndication", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            if (i <= 1 && part.Contains('.', StringComparison.OrdinalIgnoreCase))
            {
                var hostParts = part.Split('.', StringSplitOptions.RemoveEmptyEntries);
                var j = hostParts.Length - 1;
                while (j >= 0)
                {
                    if (hostParts[j].Length <= 3 || j == (hostParts.Length - 1))
                    {
                        if (j == 0)
                        {
                            if (hostParts[0] == "www")
                            {
                                ++j;
                            }
                            break;
                        }

                        --j;
                        continue;
                    }

                    break;
                }

                part = hostParts[j];
                if (part == "github" || part == "hashnode")
                {
                    part = hostParts[j - 1];
                }
            }

            return part;
        }

        return parts[^1];
    }

    private static string SanitizeSlug(ReadOnlySpan<char> slug)
    {
        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength];
        }

        if (slug.ContainsAny(reservedChars))
        {
            var slugBuilder = new StringBuilder(MaxSlugLength);
            slugBuilder.Append(slug);
            foreach (var (reservedChar, replacement) in reservedCharReplacements)
            {
                slugBuilder.Replace(reservedChar, replacement);
            }
            if (slugBuilder.Length > MaxSlugLength)
            {
                slugBuilder.Length = MaxSlugLength;
            }

            return slugBuilder.ToString();
        }

        return slug.ToString();
    }

    private static ReadOnlySpan<char> MaybeRemoveExtension(this ReadOnlySpan<char> path)
    {
        foreach (var ext in commonExtensions)
        {
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                path = path[..^ext.Length];
                break;
            }
        }

        return path;
    }

    private static bool IsCommonWord(this ReadOnlySpan<char> word)
    {
        if (word.Length < 3)
        {
            return false;
        }

        foreach (var commonWord in commonWords)
        {
            if (word.StartsWith(commonWord, StringComparison.OrdinalIgnoreCase) &&
                (word.Length / commonWord.Length) < 3)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] commonWords =
    [
        "about",
        "archive",
        "article",
        "atom",
        "blog",
        "comment",
        "contact",
        "cookie",
        "default",
        "feed",
        "index",
        "like",
        "link",
        "news",
        "note",
        "post",
        "privacy",
        "replies",
        "rss",
        "sitemap",
        "terms",
    ];

    private static readonly string[] commonExtensions =
    [
        ".aspx",
        ".axd",
        ".email",
        ".fyi",
        ".htm",
        ".html",
        ".js",
        ".md",
        ".mdx",
        ".page",
        ".pdf",
        ".php",
        ".py",
        ".rss",
        ".txt",
        ".webm",
        ".xml",
        ".yml",
    ];

    private ref struct UrlPathEnumerator
    {
        private ReadOnlySpan<char> span;
        private ReadOnlySpan<char> current;

        public UrlPathEnumerator(ReadOnlySpan<char> span)
        {
            this.span = span;
            this.current = default;
        }

        public readonly ReadOnlySpan<char> Current => this.current;

        public readonly UrlPathEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var remaining = this.span;
            while (remaining.Length > 0 && remaining[^1] == '/')
            {
                remaining = remaining[..^1];
            }
            if (remaining.IsEmpty)
                return false;

            var pos = remaining.LastIndexOf('/');
            if (pos >= 0)
            {
                this.current = remaining[(pos + 1)..];
                this.span = remaining[..pos];
                return true;
            }

            this.current = remaining;
            this.span = default;
            return true;
        }
    }
}
