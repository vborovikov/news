namespace News.Service.Data;

using System;

static class SlugGenerator
{
    public static string SlugifyPost(this string url)
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

            // check for # then get the rest after the #
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
            var dotIdx = slug.LastIndexOf('.');
            if (dotIdx > 0)
            {
                slug = slug[..dotIdx];
            }

            // check for common words then discard
            if (slug.IsCommonWord())
            {
                continue;
            }

            break;
        }

        return slug.ToString();
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

    private static bool IsCommonWord(this ReadOnlySpan<char> word)
    {
        if (word.Length < 3)
        {
            return false;
        }

        foreach (var commonWord in commonWords)
        {
            if (word.StartsWith(commonWord, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] commonWords =
    {
        "news",
        "blog",
        "article",
        "feed",
        "rss",
        "atom",
        "comment",
        "default",
        "index",
        "about",
        "contact",
        "terms",
        "privacy",
        "cookie",
        "sitemap",
    };

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

        public UrlPathEnumerator GetEnumerator() => this;

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
            if (pos > 0)
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
