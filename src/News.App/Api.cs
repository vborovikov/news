namespace News.App;

using System;
using System.Data.Common;
using System.Security.Claims;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;

static class Api
{
    public static void Register(WebApplication app)
    {
        var feeds = app.MapGroup("/api/feed")
            .RequireAuthorization();

        feeds.MapDelete("/{id:guid}", DeleteFeed)
            .WithName(nameof(DeleteFeed));

        var posts = app.MapGroup("/api/post")
            .RequireAuthorization();

        posts.MapPatch("/{id:guid}", MarkPost)
            .WithName(nameof(MarkPost));
    }

    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var guid) ? guid : Guid.Empty;

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
