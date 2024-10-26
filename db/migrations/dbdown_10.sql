-- v0.5.0-beta.35

begin transaction;
go

alter view rss.AppFeeds with schemabinding as
    select uf.UserId, uf.ChannelId, uf.FeedId, f.Source,
        isnull(uf.Title, f.Title) as Title, uf.Slug, 
        f.Description, f.Link, f.Updated, f.Scheduled, f.Error, f.Safeguards
    from rss.UserFeeds uf
    inner join rss.Feeds f on f.Id = uf.FeedId;
go

alter table rss.Feeds drop
    column Type,
    column Published,
    column EntityTag,
    column LastModified,
    column LocalSource;
go

commit transaction;
go