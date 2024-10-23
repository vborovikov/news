-- v0.5.0-beta.35

begin transaction;

alter table rss.Feeds add
    Type char(3) null,
    LastPublished datetimeoffset null,
    EntityTag varchar(100) null,
    LastModified varchar(32) null,
    LocalSource nvarchar(max) null;
go

alter view rss.AppFeeds with schemabinding as
    select uf.UserId, uf.ChannelId, uf.FeedId, f.Source, f.Type,
        isnull(uf.Title, f.Title) as Title, f.Description, uf.Slug, 
        f.Link, f.Updated, f.Scheduled, f.LastPublished, f.Error, f.Safeguards
    from rss.UserFeeds uf
    inner join rss.Feeds f on f.Id = uf.FeedId;
go

commit transaction;
go