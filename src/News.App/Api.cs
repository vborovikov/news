namespace News.App;

using System;
using System.Data.Common;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Dodkin.Dispatch;
using Microsoft.AspNetCore.Mvc;
using Spryer;

static class Api
{
    private static readonly TimeSpan QueueTimeout = TimeSpan.FromSeconds(5);

    public static void Register(WebApplication app)
    {
        var feeds = app.MapGroup("/api/feed")
            .RequireAuthorization();

        feeds.MapPut("/{id:guid}", UpdateFeed)
            .WithName(nameof(UpdateFeed));
        feeds.MapDelete("/{id:guid}", DeleteFeed)
            .WithName(nameof(DeleteFeed));

        var posts = app.MapGroup("/api/post")
            .RequireAuthorization();

        posts.MapPut("/{id:guid}", UpdatePost)
            .WithName(nameof(UpdatePost));
        posts.MapPatch("/{id:guid}", MarkPost)
            .WithName(nameof(MarkPost));

        var slugs = app.MapGroup("/api/slug");

        slugs.MapGet("/", SlugifyUrl)
            .WithName(nameof(SlugifyUrl));
    }

    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var guid) ? guid : Guid.Empty;

    public static async Task<IResult> SlugifyUrl(string url, [FromServices] IQueueRequestDispatcher rq)
    {
        try
        {
            var result = await rq.RunAsync(new SlugifyFeedQuery(url), QueueTimeout);
            return Results.Text(result);
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            return Results.Problem(x.Message);
        }
    }

    public static async Task<IResult> UpdateFeed(Guid id,
        [FromServices] DbDataSource db, [FromServices] IQueueRequestDispatcher rq, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        try
        {
            await using var cnn = await db.OpenConnectionAsync(cancellationToken);
            var updated = await cnn.ExecuteScalarAsync<DateTimeOffset>(
                """
                select f.Updated
                from rss.UserFeeds uf
                inner join rss.Feeds f on f.Id = uf.FeedId
                where uf.UserId = @UserId and uf.FeedId = @FeedId;
                """, new { UserId = user.GetUserId(), FeedId = id });
#if !DEBUG
            if ((DateTimeOffset.Now - updated).TotalHours > 1)
#endif
            {
                await rq.ExecuteAsync(new UpdateFeedCommand(id) { CancellationToken = cancellationToken }, QueueTimeout);
            }
            return Results.Ok();
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            return Results.Problem(x.Message);
        }
    }

    public static async Task<IResult> DeleteFeed(Guid id, [FromServices] DbDataSource db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await using var cnn = await db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                delete from rss.UserFeeds 
                where UserId = @UserId and FeedId = @FeedId;

                declare @FeedUseCount int;
                select @FeedUseCount = count(uf.UserId) 
                from rss.UserFeeds uf 
                where uf.FeedId = @FeedId;

                if (@FeedUseCount = 0)
                begin
                    update rss.Feeds
                    set Status = 'SKIP', Error = null
                    where Id = @FeedId;
                end;
                """, new { UserId = user.GetUserId(), FeedId = id }, tx);

            await tx.CommitAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            return Results.Problem(x.Message);
        }
    }

    public static async Task<IResult> UpdatePost(Guid id, [FromServices] DbDataSource db, [FromServices] IQueueRequestDispatcher rq, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await using var cnn = await db.OpenConnectionAsync(cancellationToken);
        try
        {
            var postStatus = await cnn.ExecuteScalarAsync<DbEnum<PostStatus>?>(
                """
                select p.Status
                from rss.Posts p
                inner join rss.UserFeeds uf on uf.FeedId = p.FeedId
                where Id = @PostId and uf.UserId = @UserId;
                """,
                new { UserId = user.GetUserId(), PostId = id });

            if (postStatus.HasValue && !postStatus.Value.HasFlag(PostStatus.SkipUpdate))
            {
                await rq.ExecuteAsync(new UpdatePostCommand(id) { CancellationToken = cancellationToken }, QueueTimeout);
                return Results.Ok();
            }

            return Results.NotFound();
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            return Results.Problem(x.Message);
        }
    }

    public static async Task<IResult> MarkPost(Guid id, string mark, [FromServices] DbDataSource db, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        await using var cnn = await db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            if (mark == "read")
            {
                await cnn.ExecuteAsync(
                    """
                    update top (1) rss.UserPosts with (updlock, serializable)
                    set IsRead = 1
                    where UserId = @UserId and PostId = @PostId;

                    if (@@RowCount = 0)
                    begin
                        declare @FeedId uniqueidentifier;
                        select @FeedId = p.FeedId
                        from rss.Posts p
                        where p.Id = @PostId;

                        insert into rss.UserPosts (UserId, FeedId, PostId, IsRead, IsFavorite)
                        values (@UserId, @FeedId, @PostId, 1, 0);
                    end;
                    """, new { UserId = user.GetUserId(), PostId = id }, tx);
            }
            else if (mark == "unread")
            {
                await cnn.ExecuteAsync(
                    """
                    update rss.UserPosts
                    set IsRead = 0
                    where UserId = @UserId and PostId = @PostId;
                    """, new { UserId = user.GetUserId(), PostId = id }, tx);
            }
            else if (mark == "star" || mark == "favorite")
            {
                await cnn.ExecuteAsync(
                    """
                    update top (1) rss.UserPosts with (updlock, serializable)
                    set IsFavorite = 1
                    where UserId = @UserId and PostId = @PostId;

                    if (@@RowCount = 0)
                    begin
                        declare @FeedId uniqueidentifier;
                        select @FeedId = p.FeedId
                        from rss.Posts p
                        where p.Id = @PostId;

                        insert into rss.UserPosts (UserId, FeedId, PostId, IsRead, IsFavorite)
                        values (@UserId, @FeedId, @PostId, 1, 1);
                    end;
                    """, new { UserId = user.GetUserId(), PostId = id }, tx);
            }
            else if (mark == "unstar" || mark == "unfavorite")
            {
                await cnn.ExecuteAsync(
                    """
                    update rss.UserPosts
                    set IsFavorite = 0
                    where UserId = @UserId and PostId = @PostId;
                    """, new { UserId = user.GetUserId(), PostId = id }, tx);
            }
            else
            {
                return Results.BadRequest();
            }

            await tx.CommitAsync(cancellationToken);
            return Results.NoContent();
        }
        catch (Exception x) when (x is not OperationCanceledException)
        {
            await tx.RollbackAsync(cancellationToken);
            return Results.Problem(x.Message);
        }
    }
}
