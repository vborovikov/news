use News;
go

-- v0.4.4

begin transaction;
go

alter view rss.AppPosts with schemabinding as
    select 
        up.UserId, p.FeedId, p.Id as PostId, up.IsRead, up.IsFavorite,
        p.Link, p.Slug, p.Published, p.Title, p.Description, p.Content, p.Author
    from rss.Posts p
    left outer join rss.UserPosts up on up.PostId = p.Id;
go

alter view rss.AppFeeds with schemabinding as
    select uf.UserId, uf.ChannelId, uf.FeedId, f.Source,
        isnull(uf.Title, f.Title) as Title, uf.Slug, 
        f.Description, f.Link, f.Updated, f.Error
    from rss.UserFeeds uf
    inner join rss.Feeds f on f.Id = uf.FeedId;
go

alter table rss.Posts drop
    constraint CK_Posts_SafeContent,
	column SafeDescription,
    column SafeContent;
go

alter table rss.Feeds drop
    constraint DF_Feeds_Safeguards,
	column Safeguards;
go

commit transaction;
go