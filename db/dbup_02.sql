use News;
go

-- v0.4.4

begin transaction;
go

alter table rss.Feeds add
	Safeguards varchar(150) not null constraint DF_Feeds_Safeguards default 'OK';
go    

alter table rss.Posts add
	SafeDescription nvarchar(max) null,
    SafeContent nvarchar(max) null constraint CK_Posts_SafeContent check (SafeContent != N'');
go

alter view rss.AppFeeds with schemabinding as
    select uf.UserId, uf.ChannelId, uf.FeedId, f.Source,
        isnull(uf.Title, f.Title) as Title, uf.Slug, 
        f.Description, f.Link, f.Updated, f.Error, f.Safeguards
    from rss.UserFeeds uf
    inner join rss.Feeds f on f.Id = uf.FeedId;
go

alter view rss.AppPosts with schemabinding as
    select 
        up.UserId, p.FeedId, p.Id as PostId, up.IsRead, up.IsFavorite,
        p.Link, p.Slug, p.Published, p.Title, p.Author, 
        isnull(p.SafeDescription, p.Description) as Description, 
        isnull(p.SafeContent, p.Content) as Content
    from rss.Posts p
    left outer join rss.UserPosts up on up.PostId = p.Id;
go

commit transaction;
go