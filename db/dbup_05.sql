use News;
go

-- v0.5.0

begin transaction;
go

alter table rss.Feeds add
    Scheduled datetimeoffset null; -- feed update next time
go

alter view rss.AppFeeds with schemabinding as
    select uf.UserId, uf.ChannelId, uf.FeedId, f.Source,
        isnull(uf.Title, f.Title) as Title, uf.Slug, 
        f.Description, f.Link, f.Updated, f.Scheduled, f.Error, f.Safeguards
    from rss.UserFeeds uf
    inner join rss.Feeds f on f.Id = uf.FeedId;
go

commit transaction;
go