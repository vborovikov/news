namespace News.Service;

using System.Collections.Generic;
using System.Data.Common;
using CodeHollow.FeedReader;
using Dapper;
using FastMember;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using News.Service.Data;

sealed class Worker : BackgroundService
{
    private readonly DbDataSource db;
    private readonly ServiceOptions options;
    private readonly ILogger<Worker> log;

    public Worker(DbDataSource db, IOptions<ServiceOptions> options, ILogger<Worker> log)
    {
        this.db = db;
        this.options = options.Value;
        this.log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.options.UpdateInterval);
        do
        {
            try
            {
                this.log.LogInformation("Updating feeds at: {time}", DateTimeOffset.Now);
                
                await UpdateFeedsAsync(stoppingToken);
            }
            catch (Exception x)
            {
                this.log.LogError(x, "Error updating feeds");
            }
            finally
            {
                this.log.LogInformation("Finished updating feeds at: {time}", DateTimeOffset.Now);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task UpdateFeedsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var feeds = await GetFeedsAsync(cancellationToken);
            foreach (var feed in feeds)
            {
                await UpdateFeedAsync(feed, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            this.log.LogError(ex, "Error updating feeds");
        }
    }

    private async Task<IEnumerable<DbFeed>> GetFeedsAsync(CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        var feeds = await cnn.QueryAsync<DbFeed>(
            """
            select f.Id, f.Source
            from rss.Feeds f
            order by f.Updated;
            """);

        return feeds;
    }

    private async Task UpdateFeedAsync(DbFeed feed, CancellationToken cancellationToken)
    {
        try
        {
            var update = await FeedReader.ReadAsync(feed.Source, cancellationToken);
            await MergeFeedUpdateAsync(feed, update, cancellationToken);
        }
        catch (Exception x)
        {
            this.log.LogError(x, "Error updating feed {FeedUrl}", feed.Source);
        }
    }

    private async Task MergeFeedUpdateAsync(DbFeed feed, Feed update, CancellationToken cancellationToken)
    {
        await using var cnn = await this.db.OpenConnectionAsync(cancellationToken);
        await using var tx = await cnn.BeginTransactionAsync(cancellationToken);
        try
        {
            await cnn.ExecuteAsync(
                """
                create table #Posts (
                    Id varchar(850) not null primary key,
                    Link nvarchar(850) not null,
                    Published datetimeoffset not null,
                    Title nvarchar(100) not null,
                    Description nvarchar(500) null,
                    Author nvarchar(100) null,
                    Content nvarchar(max) not null
                );
                """, transaction: tx);

            using (var bulkCopy = new SqlBulkCopy((SqlConnection)cnn, SqlBulkCopyOptions.Default, (SqlTransaction)tx)
            {
                DestinationTableName = "#Posts",
            })
            {
                using var postReader = ObjectReader.Create(update.Items,
                    "Id", "Link", "PublishingDateString", "Title", "Description", "Author", "Content");
                await bulkCopy.WriteToServerAsync(postReader, cancellationToken);
            }

            await cnn.ExecuteAsync(
                """
                merge rss.Posts as tgt
                using #Posts as src on tgt.ExternalId = src.Id
                when matched and tgt.FeedId = @FeedId then
                    update set
                        Link = src.Link,
                        Published = src.Published,
                        Title = src.Title,
                        Description = src.Description,
                        Author = src.Author,
                        Content = src.Content
                when not matched then
                    insert (FeedId, ExternalId, Link, Published, Title, Description, Author, Content)
                    values (@FeedId, src.Id, src.Link, src.Published, src.Title, src.Description, src.Author, src.Content);
                """, new { FeedId = feed.Id }, tx);

            await cnn.ExecuteAsync(
                """
                drop table #Posts;
                """, transaction: tx);

            await cnn.ExecuteAsync(
                """
                update rss.Feeds
                set
                    Updated = @Updated,
                    Title = @Title,
                    Description = @Description,
                    Link = @Link
                where Id = @FeedId;
                """, new
                {
                    FeedId = feed.Id,
                    Updated = DateTimeOffset.Now,
                    update.Title,
                    update.Description,
                    update.Link
                }, tx);

            await tx.CommitAsync(cancellationToken);
        }
        catch (Exception x)
        {
            await tx.RollbackAsync(cancellationToken);
            this.log.LogError(x, "Error merging feed {FeedUrl}", feed.Source);
        }
    }
}
