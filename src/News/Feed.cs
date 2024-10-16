namespace News;

/// <summary>
/// Determines the type of a feed.
/// </summary>
public enum FeedType
{
    /// <summary>
    /// Unknown feed type.
    /// </summary>
    Unknown,
    ANY = Unknown,

    /// <summary>
    /// Personal blogs, news, articles, etc.
    /// </summary>
    Blog,
    LOG = Blog,

    /// <summary>
    /// Link aggregator or any feed that provides only links.
    /// </summary>
    LinkAggregator,
    LNK = LinkAggregator,

    /// <summary>
    /// Music blogs, podcasts, etc.
    /// </summary>
    Audioblog,
    AUD = Audioblog,

    /// <summary>
    /// Vlogs, videos, etc.
    /// </summary>
    Vlog,
    VID = Vlog,
}

[Flags]
public enum FeedStatus
{
    None = 0 << 0,
    OK = None,

    // Unique ID for each post required
    UniqueId = 1 << 0,
    UQID = UniqueId,

    // Filtering out duplicate posts required
    DistinctId = 1 << 1,
    DSID = DistinctId,

    // Marks the feed source as HTML
    HtmlResponse = 1 << 2,
    HTML = HtmlResponse,

    // The feed source returned an HTTP error
    HttpError = 1 << 3,
    HTTP = HttpError,

    // Accessing the feed source via user agent required
    UserAgent = 1 << 4,
    USER = UserAgent,

    // No update required
    SkipUpdate = 1 << 5,
    SKIP = SkipUpdate,

    // Accessing the feed source via proxy required
    UseProxy = 1 << 6,
    PRXY = UseProxy,

    // Feed updates scheduled, no periodic updates required
    UseSchedule = 1 << 7,
    WAIT = UseSchedule,
}

[Flags]
public enum FeedSafeguard
{
    // Does nothing to the feed posts
    None = 0 << 0,
    OK = None,

    // Downloads content from the post link
    ContentExtractor = 1 << 0,
    CODE = ContentExtractor,

    // Trims last paragraph or inline content in the description and the content
    LastParaTrimmer = 1 << 1,
    PARA = LastParaTrimmer,

    // Replaces the description with the content
    DescriptionReplacer = 1 << 2,
    DESC = DescriptionReplacer,

    // Removes description images
    DescriptionImageRemover = 1 << 3,
    DIMG = DescriptionImageRemover,

    // Downloads image files and replaces image links with local file links
    ImageLinkFixer = 1 << 4,
    FIMG = ImageLinkFixer,

    // Replaces external links to posts with internal links if possible
    PostLinkFixer = 1 << 5,
    FPST = PostLinkFixer,
}

[Flags]
public enum PostStatus
{
    None = 0 << 0,
    OK = None,
    
    SkipUpdate = 1 << 0,
    SKIP = SkipUpdate,
}
