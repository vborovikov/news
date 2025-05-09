namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Dapper;
using Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Spryer;

[Authorize]
public class FeedModel : EditPageModel
{
    private readonly ILogger<ImportUrlModel> log;

    public FeedModel(DbDataSource db, ILogger<ImportUrlModel> log)
        : base(db)
    {
        this.log = log;
        this.Input = new();
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public async Task OnGet(Guid id, CancellationToken cancellationToken = default)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        this.Input = await cnn.QuerySingleAsync<InputModel>(
            """
            select 
                af.FeedId, af.ChannelId, af.Source as FeedUrl, af.Type as FeedType,
                af.Title as FeedTitle, af.Slug as FeedSlug, af.Safeguards,
                f.TitlePath, f.AuthorPath, f.DescriptionPath, f.ContentPath
            from rss.AppFeeds af
            inner join rss.Feeds f on f.Id = af.FeedId
            where af.UserId = @UserId and af.FeedId = @FeedId;
            """, new { this.UserId, FeedId = id });
    }

    public async Task<IActionResult> OnPost(CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid || !this.Input.IsValid)
        {
            this.ModelState.AddModelError("", "Invalid feed properties");
            return Page();
        }

        var channelId = Guid.Empty;
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            var feedInfo = await cnn.QueryFirstAsync<FeedInfo>(
                """
                select f.Id, f.Status, f.Safeguards
                from rss.Feeds f
                where f.Id = @FeedId;
                """,
                new { this.Input.FeedId }, tx);

            channelId = this.Input.ChannelId ?? Guid.NewGuid();
            if (this.Input.ChannelId is null)
            {
                // create new channel
                await cnn.ExecuteAsync(
                    """
                    insert into rss.UserChannels (UserId, Id, Name, Slug)
                    values (@UserId, @ChannelId, @ChannelName, @ChannelSlug);
                    """, 
                    new
                    {
                        this.UserId,
                        ChannelId = channelId,
                        ChannelName = this.Input.ChannelName.AsNVarChar(100),
                        ChannelSlug = this.Input.ChannelSlug.AsVarChar(100),
                    }, tx);
            }

            await cnn.ExecuteAsync(
                """
                update rss.Feeds
                set Source = @FeedUrl, Type = @FeedType, Status = @FeedStatus, Error = null, Safeguards = @Safeguards,
                    TitlePath = @TitlePath, AuthorPath = @AuthorPath, DescriptionPath = @DescriptionPath, ContentPath = @ContentPath
                where Id = @FeedId;

                update rss.UserFeeds
                set ChannelId = @ChannelId, Slug = @FeedSlug, Title = @FeedTitle
                where UserId = @UserId and FeedId = @FeedId;
                """,
                new
                {
                    this.UserId,
                    this.Input.FeedId,
                    ChannelId = channelId,
                    FeedUrl = this.Input.FeedUrl.AsNVarChar(850),
                    this.Input.FeedType,
                    FeedSlug = this.Input.FeedSlug.AsVarChar(100),
                    FeedTitle = this.Input.FeedTitle.AsNVarChar(200),
                    this.Input.Safeguards,
                    TitlePath = this.Input.TitlePath.AsNVarChar(200),
                    AuthorPath = this.Input.AuthorPath.AsNVarChar(200),
                    DescriptionPath = this.Input.DescriptionPath.AsNVarChar(200),
                    ContentPath = this.Input.ContentPath.AsNVarChar(200),
                    FeedStatus = (feedInfo.Status & ~FeedStatus.SkipUpdate & ~FeedStatus.HttpError).AsDbEnum(),
                }, tx);

            if (this.Input.Safeguards != (FeedSafeguard)feedInfo.Safeguards)
            {
                await cnn.ExecuteAsync(
                    """
                    update rss.Posts
                    set SafeDescription = null, SafeContent = null
                    where FeedId = @FeedId;
                    """, new { this.Input.FeedId }, tx);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.ModelState.AddModelError("", x.Message);
            this.log.LogError(x, "Update feed failed");

            return Page();
        }

        // redirect to the feed
        var feedPath = await cnn.QuerySingleAsync<FeedPath>(
            """
            select uf.FeedId, uf.ChannelId, uf.Slug as FeedSlug, uc.Slug as ChannelSlug
            from rss.UserFeeds uf
            inner join rss.UserChannels uc on uf.ChannelId = uc.Id
            where uf.UserId = @UserId and uf.FeedId = @FeedId and uf.ChannelId = @ChannelId;
            """, new { this.Input.FeedId, ChannelId = channelId, this.UserId });

        return RedirectToPage("Index", new { channel = feedPath.ChannelSlug, feed = feedPath.FeedSlug });
    }

    public record InputModel
    {
        public Guid FeedId { get; init; }

        [Required, Url, Display(Name = "Feed URL")]
        public string FeedUrl { get; init; } = "";

        [Display(Name = "Feed title")]
        public string? FeedTitle { get; init; }

        [Required, RegularExpression("^[a-z][a-z0-9-]+$"), MaxLength(50), Display(Name = "Feed slug")]
        public string FeedSlug { get; init; } = "";

        [RequiredIf(nameof(ChannelName), null), RequiredIf(nameof(ChannelSlug), null), Display(Name = "Feed channel")]
        public Guid? ChannelId { get; init; }

        [RequiredIf(nameof(ChannelId), null), Display(Name = "New channel name")]
        public string? ChannelName { get; init; }
        [RequiredIf(nameof(ChannelId), null), RegularExpression("^[a-z][a-z0-9-]*$"), MaxLength(50), Display(Name = "New channel slug")]
        [DeniedValues("daily", "feed", "import", "index", "img", "search", "error", "privacy")]
        public string? ChannelSlug { get; init; }

        public DbEnum<FeedType> FeedType { get; init; }

        public string? TitlePath { get; init; }

        public string? AuthorPath { get; init; }

        public string? DescriptionPath { get; init; }

        public string? ContentPath { get; init; }

        [Display(Name = "Safety measures")]
        public DbEnum<FeedSafeguard> Safeguards { get; set; }

        public bool SafeguardContentExtractor
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.ContentExtractor);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.ContentExtractor : this.Safeguards & ~FeedSafeguard.ContentExtractor;
        }

        public bool SafeguardDescriptionReplacer
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.DescriptionReplacer);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.DescriptionReplacer : this.Safeguards & ~FeedSafeguard.DescriptionReplacer;
        }

        public bool SafeguardLastParaTrimmer
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.LastParaTrimmer);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.LastParaTrimmer : this.Safeguards & ~FeedSafeguard.LastParaTrimmer;
        }

        public bool SafeguardDescriptionImageRemover
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.DescriptionImageRemover);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.DescriptionImageRemover : this.Safeguards & ~FeedSafeguard.DescriptionImageRemover;
        }

        public bool SafeguardImageLinkFixer
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.ImageLinkFixer);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.ImageLinkFixer : this.Safeguards & ~FeedSafeguard.ImageLinkFixer;
        }

        public bool SafeguardDescriptionShortener
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.DescriptionShortener);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.DescriptionShortener : this.Safeguards & ~FeedSafeguard.DescriptionShortener;
        }

        public bool IsValid => Uri.IsWellFormedUriString(this.FeedUrl, UriKind.Absolute) && this.ChannelId != Guid.Empty;
    }

    record FeedInfo
    {
        public Guid Id { get; init; }
        public DbEnum<FeedStatus> Status { get; init; }
        public DbEnum<FeedSafeguard> Safeguards { get; init; }
    }

    record FeedPath
    {
        public Guid ChannelId { get; init; }
        public string ChannelSlug { get; init; } = "";
        public Guid FeedId { get; init; }
        public string FeedSlug { get; init; } = "";
    }
}
