namespace News.Service.Data;

using System;
using System.Buffers;
using System.Diagnostics;
using System.Text;

static class SlugExtractor
{
    private const int MaxSlugLength = 100;
    private const string irrelevantChars = "!()+-@[]_{}~";
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

        var components = new UrlComponentEnumerator(url);
        while (components.MoveNext())
        {
            // get last part of the url
            slug = components.Current;

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
            slug = slug.Trim(irrelevantChars);

            // check for common words then discard
            if (slug.IsEmpty || slug.IsCommonWord())
            {
                continue;
            }

            break;
        }

        return SanitizeSlug(slug);
    }

    public static string SlugifyFeed(this string url) => url.AsSpan().SlugifyFeed();

    public static string SlugifyFeed(this ReadOnlySpan<char> url)
    {
        var slug = ReadOnlySpan<char>.Empty;
        var components = new UrlComponentEnumerator(url);
        while (components.MoveNext())
        {
            var component = components.Current;

            if (component.Type == UrlComponentType.PathSegment)
            {
                if (component.Name.Length < 3 || component.Name.HasCommonExtension() || component.Name.IsCommonWord())
                {
                    continue;
                }
            }

            slug = component;
            if (component.Type == UrlComponentType.Authority && component.Name.Contains('.'))
            {
                var hostDomains = new UrlHostDomainEnumerator(component);
                while (hostDomains.MoveNext())
                {
                SkipAdvancement:
                    var hostDomain = hostDomains.Current;
                    if (hostDomain.Name.Length <= 3 || hostDomain.Level == UrlHostDomainLevel.TopLevel)
                    {
                        // check for www
                        if (hostDomains.MoveNext())
                        {
                            if (hostDomains.Current.Name.Equals("www", StringComparison.OrdinalIgnoreCase))
                            {
                                slug = hostDomain;
                                break;
                            }

                            goto SkipAdvancement;
                        }

                        continue;
                    }

                    if (hostDomain.Level == UrlHostDomainLevel.SecondLevel && hostDomain.Name.IsKnownSldName())
                    {
                        continue;
                    }

                    slug = hostDomain.Name;
                    break;
                }
            }

            break;
        }

        return SanitizeSlug(slug);
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

    private static bool HasCommonExtension(this ReadOnlySpan<char> path)
    {
        foreach (var ext in commonExtensions)
        {
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
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
        "author",
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
        "page",
        "post",
        "privacy",
        "replies",
        "rss",
        "sitemap",
        "syndication",
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

    private static bool IsKnownSldName(this ReadOnlySpan<char> name)
    {
        foreach (var knownSldName in knownSldNames)
        {
            if (name.Equals(knownSldName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] knownSldNames = ["github", "hashnode"];

    private enum UrlComponentType
    {
        Unknown,
        Fragment,
        Query,
        Slug,
        PathSegment,
        Authority,
        Scheme,
    }

    [DebuggerDisplay("{Type,nq}: {Name}")]
    private readonly ref struct UrlComponent
    {
        public UrlComponent(ReadOnlySpan<char> name, UrlComponentType type)
        {
            this.Name = name;
            this.Type = type;
        }

        public ReadOnlySpan<char> Name { get; }
        public UrlComponentType Type { get; }

        public void Deconstruct(out ReadOnlySpan<char> name, out UrlComponentType type)
        {
            name = this.Name;
            type = this.Type;
        }

        public static implicit operator ReadOnlySpan<char>(UrlComponent component) => component.Name;
    }

    private ref struct UrlComponentEnumerator
    {
        private ReadOnlySpan<char> span;
        private UrlComponent current;

        public UrlComponentEnumerator(ReadOnlySpan<char> span)
        {
            this.span = span.TrimEnd('/');
            this.current = default;
        }

        public readonly UrlComponent Current => this.current;

        public readonly UrlComponentEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var remaining = this.span;
            if (remaining.IsEmpty)
                return false;

            var component = remaining;
            var pos = remaining.LastIndexOf('/');
            if (pos < 0)
            {
                this.span = default;
            }
            else
            {
                component = remaining[(pos + 1)..];
                this.span = remaining[..pos].TrimEnd('/');
            }

            this.current = new(component, SpecifyType(component, this.span));
            return true;
        }

        private static UrlComponentType SpecifyType(ReadOnlySpan<char> component, ReadOnlySpan<char> rest)
        {
            if (rest.IsEmpty)
            {
                if (component[^1] == ':')
                {
                    return UrlComponentType.Scheme;
                }

                return UrlComponentType.Authority;
            }

            if (rest[^1] == ':')
            {
                return UrlComponentType.Authority;
            }

            return UrlComponentType.PathSegment;
        }
    }

    private enum UrlHostDomainLevel
    {
        Unknown,
        TopLevel,
        SecondLevel,
        Subdomain,
    }

    [DebuggerDisplay("{Level,nq}: {Name}")]
    private readonly ref struct UrlHostDomain
    {
        public UrlHostDomain(ReadOnlySpan<char> name, UrlHostDomainLevel level)
        {
            this.Name = name;
            this.Level = level;
        }

        public ReadOnlySpan<char> Name { get; }
        public UrlHostDomainLevel Level { get; }

        public void Deconstruct(out ReadOnlySpan<char> name, out UrlHostDomainLevel level)
        {
            name = this.Name;
            level = this.Level;
        }

        public static implicit operator ReadOnlySpan<char>(UrlHostDomain domain) => domain.Name;
    }

    private ref struct UrlHostDomainEnumerator
    {
        private ReadOnlySpan<char> span;
        private UrlHostDomain current;
        private UrlHostDomainLevel level;

        public UrlHostDomainEnumerator(ReadOnlySpan<char> span)
        {
            this.span = GetHostname(span);
            this.current = default;
        }

        public readonly UrlHostDomain Current => this.current;
        public readonly UrlHostDomainEnumerator GetEnumerator() => this;

        public bool MoveNext()
        {
            var remaining = this.span;
            if (remaining.IsEmpty)
                return false;

            var domain = remaining;
            var pos = remaining.LastIndexOf('.');
            if (pos < 0)
            {
                this.span = default;
            }
            else
            {
                domain = remaining[(pos + 1)..];
                this.span = remaining[..pos];
            }

            if (this.level < UrlHostDomainLevel.Subdomain)
            {
                ++this.level;
            }

            this.current = new(domain, this.level);
            return true;
        }

        private static ReadOnlySpan<char> GetHostname(ReadOnlySpan<char> span)
        {
            var start = span.IndexOf('@');
            var end = span.LastIndexOf(':');

            if (start < 0 && end < 0)
                return span;
            if (end < 0)
                return span[(start + 1)..];
            if (start < 0)
                return span[..end];

            return span[(start + 1)..end];
        }
    }
}
