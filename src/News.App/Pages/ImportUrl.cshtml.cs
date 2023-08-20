namespace News.App.Pages;

using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Net;
using System.Threading;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using News.App.Data;

[Authorize]
public class ImportUrlModel : PageModel
{
    private readonly UserManager<AppUser> userManager;
    private readonly DbDataSource db;
    private readonly ILogger<ImportUrlModel> log;

    public ImportUrlModel(UserManager<AppUser> userManager, DbDataSource db, ILogger<ImportUrlModel> log)
    {
        this.userManager = userManager;
        this.db = db;
        this.log = log;
        this.Feed = new();
    }

    [BindProperty]
    public FeedImport Feed { get; set; }

    public IEnumerable<RssChannelInfo> Channels { get; private set; } = Array.Empty<RssChannelInfo>();
    public SelectList ChannelList => new(this.Channels, nameof(RssChannelInfo.ChannelId), nameof(RssChannelInfo.Name), this.Feed.ChannelId);

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var userIdStr = this.userManager.GetUserId(this.User);
        if (!Guid.TryParse(userIdStr, out var userId))
        {
            // user not logged in or whatever
            this.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            return;
        }

        await using var cnn = await this.db.OpenConnectionAsync(this.HttpContext.RequestAborted);
        this.Channels = await cnn.QueryAsync<RssChannelInfo>(
            """
            select ch.Id as ChannelId, isnull(uc.Name, ch.Name) as Name, isnull(uc.Slug, ch.Slug) as Slug
            from rss.UserChannels uc
            right outer join rss.Channels ch on uc.ChannelId = ch.Id
            where uc.UserId = @userId
            order by Name;
            """, new { userId });

        await base.OnPageHandlerExecutionAsync(context, next);
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken = default)
    {
        var userIdStr = this.userManager.GetUserId(this.User);
        if (!this.ModelState.IsValid || this.Feed?.IsValid != true || !Guid.TryParse(userIdStr, out var userId))
        {
            this.ModelState.AddModelError(nameof(this.Feed.FeedUrl), "Invalid URL");
            return Page();
        }

        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            // insert channel if it's new, generate slug for it, copy to user channels
            if (!Guid.TryParse(this.Feed.ChannelId, out var channelId))
            {
                channelId = Guid.NewGuid();

                // new feed channel
                await cnn.ExecuteAsync(
                    """
                    insert into rss.Channels (Id, Name, Slug)
                    values (@ChannelId, @ChannelName, @ChannelSlug);

                    insert into rss.UserChannels (UserId, ChannelId, Name, Slug)
                    values (@UserId, @ChannelId, @ChannelName, @ChannelSlug);
                    """, new { UserId = userId, ChannelId = channelId, this.Feed.ChannelName, this.Feed.ChannelSlug }, tx);

            }
            else 
            {
                // existing feed channel, check if user has it
                var channel = await cnn.QueryFirstOrDefaultAsync<RssChannelInfo>(
                    """
                    select ch.Id as ChannelId, isnull(uc.Name, ch.Name) as Name, isnull(uc.Slug, ch.Slug) as Slug
                    from rss.UserChannels uc
                    right outer join rss.Channels ch on uc.ChannelId = ch.Id
                    where uc.UserId = @UserId and uc.ChannelId = @ChannelId
                    """, new { UserId = userId, ChannelId = channelId }, tx);
                if (channel is null)
                {
                    await cnn.ExecuteAsync(
                        """
                        declare @ChannelName nvarchar(100);
                        declare @ChannelSlug varchar(100);

                        select @ChannelName = Name, @ChannelSlug = Slug
                        from rss.Channels
                        where Id = @ChannelId;

                        insert into rss.UserChannels (UserId, ChannelId, Name, Slug)
                        output inserted
                        values (@UserId, @ChannelId, @ChannelName, @ChannelSlug);
                        """, new { UserId = userId, ChannelId = channelId }, tx);
                }
            }
            // insert feed if it's new, copy to user feeds, get feed slug
            var userFeed = await cnn.QueryFirstOrDefaultAsync<RssFeedInfo>(
                """
                select f.Id as FeedId, uf.Slug, isnull(uf.Title, f.Title) as Title
                from rss.UserFeeds uf
                right outer join rss.Feeds f on uf.FeedId = f.Id
                where uf.UserId = @UserId and f.Source = @FeedUrl
                """, new { UserId = userId, this.Feed.FeedUrl }, tx);
            if (userFeed is null)
            {
                // user feed not found
                var feed = await cnn.QueryFirstOrDefaultAsync<RssFeedInfo>(
                    """
                    select f.Id as FeedId, f.Title, f.Slug
                    from rss.Feeds f
                    where f.Source = @FeedUrl;
                    """, new { this.Feed.FeedUrl }, tx);
                if (feed is null)
                {
                    // completely new feed
                    feed = await cnn.QuerySingleAsync<RssFeedInfo>(
                        """
                        insert into rss.Feeds (Source, Link, Title)
                        output inserted.Id as FeedId, inserted.Title
                        values (@FeedUrl, @FeedUrl, @FeedUrl);
                        """, new { this.Feed.FeedUrl }, tx);
                }

                await cnn.ExecuteAsync(
                    """
                    declare @FeedTitle nvarchar(100);

                    select @FeedTitle = Title
                    from rss.Feeds
                    where Id = @FeedId;

                    insert into rss.UserFeeds (UserId, FeedId, Title, Slug)
                    values (@UserId, @FeedId, @FeedTitle, @FeedSlug);
                    """, new { UserId = userId, feed.FeedId, this.Feed.FeedSlug }, tx);
            }

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.ModelState.AddModelError(nameof(this.Feed.FeedUrl), x.Message);
            this.log.LogError(x, "Import URL failed");

            return Page();
        }

        // redirect to the feed
        return RedirectToPage("Feed");
    }

    public record FeedImport
    {
        [Required, Url]
        public string? FeedUrl { get; init; }

        [Required]
        public string? FeedSlug { get; init; }

        [RequiredIf(nameof(ChannelName), null), RequiredIf(nameof(ChannelSlug), null)]
        public string? ChannelId { get; init; }

        [RequiredIf(nameof(ChannelId), null)]
        public string? ChannelName { get; init; }
        [RequiredIf(nameof(ChannelId), null)]
        public string? ChannelSlug { get; init; }

        public bool IsValid => Uri.IsWellFormedUriString(this.FeedUrl, UriKind.Absolute);
    }
}
