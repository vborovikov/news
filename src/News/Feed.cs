namespace News;

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