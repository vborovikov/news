namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Data;
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
                af.FeedId, af.ChannelId, af.Source as FeedUrl, 
                af.Title as FeedTitle, af.Slug as FeedSlug,
                af.Safeguards
            from rss.AppFeeds af
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

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                update rss.Feeds
                set Source = @FeedUrl, Status = 'OK', Error = null, Safeguards = @Safeguards
                where Id = @FeedId;

                update rss.UserFeeds
                set ChannelId = @ChannelId, Slug = @FeedSlug, Title = @FeedTitle
                where UserId = @UserId and FeedId = @FeedId;
                """, new
                {
                    this.UserId,
                    this.Input.FeedId,
                    this.Input.ChannelId,
                    this.Input.FeedUrl,
                    this.Input.FeedSlug,
                    this.Input.FeedTitle,
                    this.Input.Safeguards
                }, tx);

            if (this.Input.Safeguards == FeedSafeguard.None)
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
            """, new { this.Input.FeedId, this.Input.ChannelId, this.UserId });

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

        [Required, Display(Name = "Feed channel")]
        public Guid ChannelId { get; init; }

        [Display(Name = "Safety measures")]
        public DbEnum<FeedSafeguard> Safeguards { get; set; }

        public bool SafeguardCodeBlockEncoder
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.CodeBlockEncoder);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.CodeBlockEncoder : this.Safeguards & ~FeedSafeguard.CodeBlockEncoder;
        }

        public bool SafeguardDescriptionRemover
        {
            get => this.Safeguards.HasFlag(FeedSafeguard.DescriptionRemover);
            set => this.Safeguards = value ? this.Safeguards | FeedSafeguard.DescriptionRemover : this.Safeguards & ~FeedSafeguard.DescriptionRemover;
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

        public bool IsValid => Uri.IsWellFormedUriString(this.FeedUrl, UriKind.Absolute) && this.ChannelId != Guid.Empty;
    }
}
