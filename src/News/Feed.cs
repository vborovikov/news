namespace News;

using System.ComponentModel;

/// <summary>
/// Determines the type of a feed.
/// </summary>
public enum FeedType
{
    /// <summary>
    /// Unknown feed type.
    /// </summary>
    [AmbientValue("ANY")]
    Unknown,

    /// <summary>
    /// Personal blogs, slow news, articles, etc.
    /// </summary>
    [AmbientValue("LOG")]
    Blog,

    /// <summary>
    /// Blogs containing posts with links to other blogs.
    /// </summary>
    [AmbientValue("DIG")]
    Digest,

    /// <summary>
    /// Link aggregator or any feed that provides only links and maybe comments.
    /// </summary>
    [AmbientValue("LNK")]
    Forum,

    /// <summary>
    /// News aggregator or any feed that provides only news.
    /// </summary>
    [AmbientValue("INF")]
    News,

    /// <summary>
    /// Music blogs, podcasts, etc.
    /// </summary>
    [AmbientValue("AUD")]
    Audio,

    /// <summary>
    /// Vlogs, videos, etc.
    /// </summary>
    [AmbientValue("VID")]
    Video,

    /// <summary>
    /// Images, photos, comics, graphics, etc.
    /// </summary>
    [AmbientValue("IMG")]
    Image,
}

[Flags]
public enum FeedStatus
{
    [AmbientValue("OK")]
    None = 0 << 0,

    // Unique ID for each post required
    [AmbientValue("UQID")]
    UniqueId = 1 << 0,

    // Filtering out duplicate posts required
    [AmbientValue("DSID")]
    DistinctId = 1 << 1,

    // Marks the feed source as HTML
    [AmbientValue("HTML")]
    HtmlResponse = 1 << 2,

    // The feed source returned an HTTP error
    [AmbientValue("HTTP")]
    HttpError = 1 << 3,

    // Accessing the feed source via user agent required
    [AmbientValue("USER")]
    UserAgent = 1 << 4,

    // No update required
    [AmbientValue("SKIP")]
    SkipUpdate = 1 << 5,

    // Accessing the feed source via proxy required
    [AmbientValue("PRXY")]
    UseProxy = 1 << 6,

    // Feed updates scheduled, no periodic updates required
    [AmbientValue("WAIT")]
    UseSchedule = 1 << 7,
}

[Flags]
public enum FeedSafeguard
{
    // Does nothing to the feed posts
    [AmbientValue("OK")]
    None = 0 << 0,

    // Downloads content from the post link
    [AmbientValue("CODE")]
    ContentExtractor = 1 << 0,

    // Trims last paragraph or inline content in the description and the content
    [AmbientValue("PARA")]
    LastParaTrimmer = 1 << 1,

    // Replaces the description with the content
    [AmbientValue("DESC")]
    DescriptionReplacer = 1 << 2,

    // Removes description images
    [AmbientValue("DIMG")]
    DescriptionImageRemover = 1 << 3,

    // Downloads image files and replaces image links with local file links
    [AmbientValue("FIMG")]
    ImageLinkFixer = 1 << 4,

    // Replaces external links to posts with internal links if possible
    [AmbientValue("FPST")]
    PostLinkFixer = 1 << 5,
}

[Flags]
public enum PostStatus
{
    [AmbientValue("OK")]
    None = 0 << 0,

    [AmbientValue("SKIP")]
    SkipUpdate = 1 << 0,
}
