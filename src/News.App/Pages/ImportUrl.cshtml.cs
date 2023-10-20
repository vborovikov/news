namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Threading;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using News.App.Data;

[Authorize]
public class ImportUrlModel : EditPageModel
{
    private readonly ILogger<ImportUrlModel> log;

    public ImportUrlModel(DbDataSource db, ILogger<ImportUrlModel> log)
        : base(db)
    {
        this.log = log;
        this.Input = new();
    }

    [BindProperty]
    public InputModel Input { get; init; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        if (!this.ModelState.IsValid || !this.Input.IsValid)
        {
            this.ModelState.AddModelError("", "Invalid feed properties");
            return Page();
        }

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);

        RssChannelInfo? channel = null;
        RssFeedInfo? feed = null;
        try
        {
            // insert user channel if it's new
            if (!Guid.TryParse(this.Input.ChannelId, out var channelId))
            {
                // new user channel
                channelId = Guid.NewGuid();
                channel = await cnn.QuerySingleAsync<RssChannelInfo>(
                    """
                    insert into rss.UserChannels (UserId, Id, Name, Slug)
                    output inserted.Id as ChannelId, inserted.Name, inserted.Slug
                    values (@UserId, @ChannelId, @ChannelName, @ChannelSlug);
                    """, new { this.UserId, ChannelId = channelId, this.Input.ChannelName, this.Input.ChannelSlug }, tx);

            }
            else
            {
                // existing user channel
                channel = await cnn.QueryFirstOrDefaultAsync<RssChannelInfo>(
                    """
                    select uc.Id as ChannelId, uc.Name, uc.Slug
                    from rss.UserChannels uc
                    where uc.UserId = @UserId and uc.Id = @ChannelId
                    """, new { this.UserId, ChannelId = channelId }, tx);
            }

            // insert feed if it's new, copy to user feeds, get feed slug
            var userFeed = await cnn.QueryFirstOrDefaultAsync<RssFeedInfo>(
                """
                select f.Id as FeedId, uf.Slug, isnull(uf.Title, f.Title) as Title
                from rss.UserFeeds uf
                right outer join rss.Feeds f on uf.FeedId = f.Id
                where uf.UserId = @UserId and f.Source = @FeedUrl
                """, new { this.UserId, this.Input.FeedUrl }, tx);
            if (userFeed is null)
            {
                // user feed not found
                feed = await cnn.QueryFirstOrDefaultAsync<RssFeedInfo>(
                    """
                    select f.Id as FeedId, f.Title
                    from rss.Feeds f
                    where f.Source = @FeedUrl;
                    """, new { this.Input.FeedUrl }, tx);
                if (feed is null)
                {
                    // completely new feed
                    feed = await cnn.QuerySingleAsync<RssFeedInfo>(
                        """
                        insert into rss.Feeds (Id, Source, Link, Title)
                        output inserted.Id as FeedId, inserted.Title
                        values (@FeedId, @FeedUrl, @FeedUrl, @FeedUrl);
                        """, new { FeedId = Guid.NewGuid(), this.Input.FeedUrl }, tx);
                }

                feed = await cnn.QuerySingleAsync<RssFeedInfo>(
                    """
                    insert into rss.UserFeeds (UserId, FeedId, ChannelId, Slug)
                    output inserted.FeedId as FeedId, inserted.Slug
                    values (@UserId, @FeedId, @ChannelId, @FeedSlug);
                    """, new { this.UserId, feed.FeedId, ChannelId = channelId, this.Input.FeedSlug }, tx);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.ModelState.AddModelError("", x.Message);
            this.log.LogError(x, "Import URL failed");

            return Page();
        }

        // redirect to the feed
        return RedirectToPage("Index", new { channel = channel?.Slug, feed = feed?.Slug });
    }

    public record InputModel
    {
        [Required, Url, Display(Name = "Feed URL")]
        public string FeedUrl { get; init; } = "";

        [Required, RegularExpression("^[a-z][a-z0-9-]+$"), MaxLength(50), Display(Name = "Feed slug")]
        public string FeedSlug { get; init; } = "";

        [RequiredIf(nameof(ChannelName), null), RequiredIf(nameof(ChannelSlug), null), Display(Name = "Existing channel")]
        public string? ChannelId { get; init; }

        [RequiredIf(nameof(ChannelId), null), Display(Name = "New channel name")]
        public string? ChannelName { get; init; }
        [RequiredIf(nameof(ChannelId), null), RegularExpression("^[a-z][a-z0-9-]*$"), MaxLength(50), Display(Name = "New channel slug")]
        public string? ChannelSlug { get; init; }

        public bool IsValid => Uri.IsWellFormedUriString(this.FeedUrl, UriKind.Absolute);
    }
}
